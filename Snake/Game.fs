namespace rec SnakeGame

open System
open System.Timers

type CommandMessage =
    | Cmd of Command
    | Flush

type SnakeStateMessage =
    | Commands of Command list
    | GrowUp
    | Tick

[<Struct>]
type TimerCommand =
    | Start
    | Pause
    | Stop

module GameField =
    let private isSnakeCrossingItself (snakeCoordinates: struct(int*int) seq) =
        let uniqueCoordinates = System.Collections.Generic.HashSet(snakeCoordinates)
        uniqueCoordinates.Count < Seq.length snakeCoordinates

    let private isSnakeCrossedObstacle (field:field) snakeCoordinates =
        let predicate point =
            match field.TryGetCell point with
            | None -> false
            | Some cell ->  cell.content = Obstacle
        List.exists predicate snakeCoordinates

    let private isSnakeCrossedByEaters snakeCoordinates eaters =
        let snakeSet = Set.ofSeq snakeCoordinates
        let eaterSet = Set.ofSeq eaters
        Set.intersect snakeSet eaterSet
        |> Seq.length > 0

    let private buildNextFrame (field:field) snake eaters =
        let updateCell i j cell =
            let point = struct(i,j)
            let setContent content = field.cellMap.[i,j] <- {cell with content = content}
            let newSnakeCell = Seq.contains point snake
            let newEaterCell = Seq.contains point eaters
            match cell.content with
            | Obstacle | Exit -> ()
            | SnakeCell when not newSnakeCell -> setContent Empty
            | Eater when not newEaterCell -> setContent Empty
            | _ when newSnakeCell -> setContent SnakeCell
            | _ when newEaterCell -> setContent Eater
            | _ -> ()
        Array2D.iteri updateCell field.cellMap
        Frame field

    let private evaluateNextEaterCoordinates eaters (struct(i,j) as snakeHead) =
        let getCloser a b =
            if b > a then b - 1
            elif b < a then b + 1
            else b
        let nextCoordinate (struct(x,y) as eater) =
            if eater = snakeHead then eater
            elif x = i then struct (x, getCloser j y)
            elif y = j then struct (getCloser i x, j)
            elif x % 2 = 0 then struct(x, getCloser j y)
            else struct (getCloser i x, y)
        Seq.map (fun e -> (e, nextCoordinate e)) eaters

    let private getNextEatersStep (field:field) snake =
        let eaters = field.cellMap |> Seq.cast<Cell> |> Seq.filter (fun c -> c.content = Eater) |> Seq.map (fun c -> struct(c.x, c.y))
        let nextEaters = evaluateNextEaterCoordinates eaters snake.headPoint
        let select ((struct(a,b) as eater), (struct(c,d) as nextEater)) =
            match field.TryGetCell nextEater with
            | None -> eater
            | Some cell ->
                match cell.content with
                | Empty | Food -> nextEater
                | Obstacle | Exit | Eater -> eater
                | SnakeCell -> if snake.HasPerk Armor then eater else nextEater
        Seq.map select nextEaters

    let buildNextGameFrame field snake =
        let eaters = getNextEatersStep field snake
        let (snakeAgent: Agent<SnakeStateMessage, SnakeState>) = Game.snakeAgent
        let snakeCoordinates = Field.getSnakeCoordinates snake.body snake.headPoint
        let isPointOutside p = Field.isPointInside struct(field.width, field.height) p |> not
        let isSnakeOutside = List.fold (fun acc point -> acc && isPointOutside point) true snakeCoordinates
        let buildNextFrame() = buildNextFrame field snakeCoordinates eaters
        if isSnakeOutside
        then Win
        elif isSnakeCrossingItself snakeCoordinates || isSnakeCrossedObstacle field snakeCoordinates
        then Loss "snake crossed itself or obstacle"
        else
        match field.TryGetCell snake.headPoint with
        | None -> buildNextFrame()
        | Some cell ->
            match cell.content with
            | Obstacle -> Loss "snake crossed obstacle"
            | Empty | SnakeCell | Exit-> buildNextFrame()
            | Food -> snakeAgent.Post GrowUp
                      buildNextFrame()
            | Eater ->
                if not <| snake.HasPerk AttackMode then Loss "eater ate your snake"
                else snakeAgent.Post GrowUp
                     buildNextFrame()

module Game =

    let print =
        let (timerAgent: Agent<TimerCommand, Timer>) = Game.timerAgent
        let printWithColor color (text:string) =
            let oldColor = Console.ForegroundColor
            Console.ForegroundColor <- color
            Console.Write(text)
            Console.ForegroundColor <- oldColor

        let printCell =
            function
            | SnakeCell -> printWithColor ConsoleColor.Green "s"
            | Eater -> printWithColor ConsoleColor.Red "e"
            | Obstacle -> printWithColor ConsoleColor.DarkYellow "X"
            | Exit -> printWithColor ConsoleColor.Blue "O"
            | Empty -> Console.Write(" ")
            | Food -> printWithColor ConsoleColor.Magenta "o"

        function
        | Loss str as loss -> 
            timerAgent.Post Stop
            printfn "%A" loss
        | Win -> 
            timerAgent.Post Stop
            printfn "Win"
        | Frame field ->
            System.Console.Clear()
            for j in field.height - 1..-1..0 do
                for i in 0..field.width - 1 do
                    field.cellMap.[i,j].content |> printCell
                Console.WriteLine()

    let fieldAgentFn gameState snake =
        //printfn "Field agent: %A command recieved" snake
        let gameState =
            match gameState with
            | Frame field -> snake |> GameField.buildNextGameFrame field
            | state -> state
        print gameState
        gameState

    let fieldAgent = Field.getStartField() |> Frame |> Mailbox.buildAgent fieldAgentFn |> Agent

    let snakeAgentFn snake message =
        //printfn "Snake agent: %A command recieved" message
        let snake =
            match message with
            | Commands commands -> Snake.applyCommands snake commands
            | GrowUp -> Snake.growUp snake
            | Tick -> Snake.tick snake
        fieldAgent.Post snake
        //printfn "Snake agent: post command to fieldAgent"
        snake

    let snakeAgent = Snake.getDefaultSnakeState struct(20,15) |> Mailbox.buildAgent snakeAgentFn |> Agent

    let commandAgentFn commands msg =
        //printfn "Command agent: %A command recieved" msg
        match msg with
        | Cmd cmd -> cmd::commands
        | Flush ->
            Snake.mergeCommands commands
                |> Commands
                |> snakeAgent.Post
            []

    let commandAgent = [] |> Mailbox.buildAgent commandAgentFn |> Agent

    let timerAgentFn (timer: System.Timers.Timer) cmd =
        // printfn "Timer agent: %A command recieved" cmd
        match cmd with
        | Start -> timer.Start()
        | Pause -> timer.Stop()
        | Stop -> timer.Stop(); timer.Dispose()
        timer

    let setUpTimer() =
        let timer = new Timer()
        timer.Interval <- 500.0
        timer.Elapsed.Add (fun x ->
            commandAgent.Post Flush;
            snakeAgent.Post Tick;
            )
        timer

    let timerAgent = setUpTimer() |> Mailbox.buildAgent timerAgentFn |> Agent

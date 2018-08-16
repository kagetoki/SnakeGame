namespace SnakeGame

open System
open System.Timers
open PostOffice

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
    | Subscribe of (unit -> unit)

type SnakeMailboxSystem =
    {
        timerAgent: Agent<TimerCommand, Timer>
        snakeAgent: Agent<SnakeStateMessage, SnakeState>
        fieldAgent: Agent<SnakeState, GameFrame>
        commandAgent: Agent<CommandMessage, Command list>
        mailboxSystem: MailboxSystem
    }

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

    let buildNextGameFrame growSnakeUp field snake =
        let eaters = getNextEatersStep field snake
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
            | Food -> growSnakeUp()
                      buildNextFrame()
            | Eater ->
                if not <| snake.HasPerk AttackMode then Loss "eater ate your snake"
                else growSnakeUp()
                     buildNextFrame()

[<RequireQualifiedAccess>]
module Game =

    let [<Literal>] fieldAddress = "field"
    let [<Literal>] snakeAddress = "snake"
    let [<Literal>] commandAddress = "command"
    let [<Literal>] timerAddress = "timer"

    let snakeAgent (mailboxSystem: MailboxSystem) = mailboxSystem.Box<SnakeStateMessage, SnakeState>(snakeAddress)
    let timerAgent (mailboxSystem: MailboxSystem) = mailboxSystem.Box<TimerCommand, Timer>(timerAddress)
    let fieldAgent (mailboxSystem: MailboxSystem) = mailboxSystem.Box<SnakeState, GameFrame>(fieldAddress)
    let commandAgent (mailboxSystem: MailboxSystem) = mailboxSystem.Box<CommandMessage, Command list>(commandAddress)

    let fieldAgentFn (mailboxSystem: MailboxSystem) updateUi gameState snake =
        let timerAgent = timerAgent mailboxSystem
        let snakeAgent = snakeAgent mailboxSystem
        let gameState =
            match gameState with
            | Frame field -> snake |> GameField.buildNextGameFrame (fun () -> snakeAgent.Post GrowUp) field
            | state -> 
                timerAgent.Post Stop
                state
        updateUi gameState
        gameState

    let snakeAgentFn (mailboxSystem: MailboxSystem) snake message =
        let fieldAgent = fieldAgent mailboxSystem
        let snake =
            match message with
            | Commands commands -> Snake.applyCommands snake commands
            | GrowUp -> Snake.growUp snake
            | Tick -> Snake.tick snake
        fieldAgent.Post snake
        snake

    let commandAgentFn (mailboxSystem: MailboxSystem) commands msg =
        let snakeAgent = snakeAgent mailboxSystem
        match msg with
        | Cmd cmd -> cmd::commands
        | Flush ->
            Snake.mergeCommands commands
                |> Commands
                |> snakeAgent.Post
            []

    let timerAgentFn (timer: Timer) cmd =
        match cmd with
        | Start -> timer.Start()
        | Pause -> timer.Stop()
        | Stop -> timer.Stop(); timer.Dispose()
        | Subscribe fn -> timer.Elapsed.Add (fun x -> fn())
        timer

    let setUpTimer (mailboxSystem: MailboxSystem) =
        let timer = new Timer()
        timer.Interval <- 500.0
        timer.Elapsed.Add (fun x ->
            (commandAgent mailboxSystem).Post Flush;
            (snakeAgent mailboxSystem).Post Tick;
            )
        timer

    let buildSnakeGame (mailboxSystem: MailboxSystem) updateUi =
        let fieldAgentFn = fieldAgentFn mailboxSystem updateUi
        let commandAgentFn = commandAgentFn mailboxSystem
        let snakeAgentFn = snakeAgentFn mailboxSystem
        
        let fieldAgent = (fieldAddress, Field.getStartField() |> Frame |> Mailbox.buildAgent fieldAgentFn) |> MailAgent
        let snakeAgent = (snakeAddress, Snake.getDefaultSnakeState struct(4, 5) |> Mailbox.buildAgent snakeAgentFn) |> MailAgent 
        let commandAgent = (commandAddress, []|> Mailbox.buildAgent commandAgentFn) |> MailAgent 
        let timerAgent = (timerAddress, setUpTimer mailboxSystem |> Mailbox.buildAgent timerAgentFn) |> MailAgent 
        mailboxSystem.RespawnBox fieldAgent
        mailboxSystem.RespawnBox snakeAgent
        mailboxSystem.RespawnBox commandAgent
        mailboxSystem.RespawnBox timerAgent
        {
            fieldAgent = Box fieldAgent
            snakeAgent = Box snakeAgent
            commandAgent = Box commandAgent
            timerAgent = Box timerAgent
            mailboxSystem = mailboxSystem
        }

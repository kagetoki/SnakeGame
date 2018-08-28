namespace SnakeGame

type GameState =
    {
        snake: SnakeState
        gameFrame: GameFrame
        eaters: struct(int*int) list
    }

[<RequireQualifiedAccess>]
module Game =
    open System

    let private random = new Random()
    let private getRandomCoordinate width height =
        struct (random.Next(0, width - 1), random.Next(0, height - 1))

    let private isSnakeCrossingItself (snakeCoordinates: struct(int*int) seq) =
        let uniqueCoordinates = System.Collections.Generic.HashSet(snakeCoordinates)
        uniqueCoordinates.Count < Seq.length snakeCoordinates

    let private isSnakeCrossedObstacle (field:field) snakeCoordinates =
        let predicate point =
            match field.TryGetCell point with
            | None -> false
            | Some cell ->  cell.content = Obstacle
        List.exists predicate snakeCoordinates

    let private getSnakeEaterIntersection snakeCoordinates eaters =
        let snakeSet = Set.ofSeq snakeCoordinates
        let eaterSet = Set.ofSeq eaters
        Set.intersect snakeSet eaterSet
        //|> Seq.length > 0

    let private updateCellMap (field:field) eaters snake =
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
        field

    let private suggestNextEaterCoordinates eaters (struct(i,j) as snakeHead) =
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
        List.map (fun e -> (e, nextCoordinate e)) eaters

    let private getNextEatersStep (field:field) snake eaters =
        let nextEaters = suggestNextEaterCoordinates eaters snake.headPoint
        let select ((struct(a,b) as eater), (struct(c,d) as nextEater)) =
            match field.TryGetCell nextEater with
            | None -> eater
            | Some cell ->
                match cell.content with
                | Empty | Food -> nextEater
                | Obstacle | Exit | Eater -> eater
                | SnakeCell -> if snake.HasPerk Armor then eater else nextEater
        let res = List.map select nextEaters
        //printfn "%A" res
        res

    let private spawnFoodInRandomCell (field:field) =
        let getRandomCoordinate() = getRandomCoordinate field.width field.height
        let rec spawn () =
            let struct (x,y) = getRandomCoordinate()
            match field.cellMap.[x, y].content with
            | Empty -> field.cellMap.[x, y] <- {x = x; y = y; content = Food}
            | _ -> spawn()
        spawn()

    let isEatersWin snakeCoordinates (snake: SnakeState) eaters =
        let eatersIntersection = getSnakeEaterIntersection snakeCoordinates eaters
        if Set.count eatersIntersection = 0 then false
        elif snake.HasPerk Armor then false
        elif Set.count eatersIntersection = 1 
             && Set.contains snake.headPoint eatersIntersection
             && snake.HasPerk AttackMode then false
        else true

    let updateGameState gameState commands =
        match gameState.gameFrame with
        | Win _ -> gameState
        | Loss _ -> gameState
        | Frame field ->
            let isPointOutside p = Field.isPointInside struct(field.width, field.height) p |> not
            let snake = commands |> Snake.applyCommands gameState.snake |> Snake.tick
            let snakeCoordinates = Field.getSnakeCoordinates snake.body snake.headPoint
            let isSnakeOutside = List.fold (fun acc point -> acc && isPointOutside point) true snakeCoordinates
            let eaters = getNextEatersStep field snake gameState.eaters
            let updateCellMap snakeCoordinates = updateCellMap field eaters snakeCoordinates
            if isSnakeOutside
            then { gameFrame = Win snake.length; snake = snake; eaters = eaters }
            elif isSnakeCrossingItself snakeCoordinates || isSnakeCrossedObstacle field snakeCoordinates
            then { gameFrame = Loss "snake crossed itself or obstacle"; snake = snake; eaters = eaters}
            elif isEatersWin snakeCoordinates snake eaters
            then {gameFrame = Loss "eater eate your snake"; snake = snake; eaters = eaters}
            else
            match field.TryGetCell snake.headPoint with
            | None -> { gameFrame = updateCellMap snakeCoordinates |> Frame; snake = snake; eaters = eaters}
            | Some cell ->
                match cell.content with
                | Obstacle -> { gameFrame = Loss "snake crossed obstacle"; snake = snake ; eaters = eaters}
                | Empty | SnakeCell | Exit-> { gameFrame = updateCellMap snakeCoordinates |> Frame; snake = snake; eaters = eaters}
                | Food -> let snake = Snake.growUp snake
                          let snakeCoordinates = Field.getSnakeCoordinates snake.body snake.headPoint
                          spawnFoodInRandomCell field
                          { gameFrame = updateCellMap snakeCoordinates |> Frame; snake = snake; eaters = eaters}
                | Eater ->
                    let snake = Snake.growUp snake
                    let snakeCoordinates = Field.getSnakeCoordinates snake.body snake.headPoint
                    let eaters = List.filter (compareStructTuple snake.headPoint) eaters
                    { gameFrame = updateCellMap snakeCoordinates |> Frame; 
                      snake = snake; 
                      eaters = eaters}

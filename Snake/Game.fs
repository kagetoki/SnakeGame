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
            elif DateTimeOffset.UtcNow.Second % 2 = 0 then struct(x, getCloser j y)
            else struct (getCloser i x, y)
        List.map (fun e -> (e, nextCoordinate e)) eaters

    let private getNextEatersStep (field:field) snake eaters =
        let nextEaters = suggestNextEaterCoordinates eaters snake.headPoint
        let select ((struct(a,b) as eater), (struct(c,d) as nextEater)) =
            match field.TryGetCell nextEater with
            | None -> eater
            | Some cell ->
                match cell.content with
                | Empty | Food | SnakeCell -> nextEater
                | Obstacle | Exit | Eater -> eater
        let res = List.map select nextEaters
        res

    let private getEaterEatenBySnake (snake: SnakeState) eaters =
        if snake.HasPerk Attack |> not then None
        else
        let nextSnakePoint = Field.nextCoordinate snake.direction.Opposite snake.headPoint
        orElse {
            return! Seq.tryFind (compareStructTuple snake.headPoint) eaters
            return! Seq.tryFind (compareStructTuple nextSnakePoint) eaters
        }
    
    let private isEatersWin snakeCoordinates eaters =
        let eatersIntersection = getSnakeEaterIntersection snakeCoordinates eaters
        if Set.count eatersIntersection = 0 then false
        else true

    let private filterCommands field snake commands =
        let predicate cmd =
            match cmd with
            | Perk (Add perk) -> 
                match field.perksAvailabilityMap.TryFind perk with
                | None -> true
                | Some threshold -> snake.length >= threshold
            | _ -> true
        List.filter predicate commands

    let updateGameState gameState commands =
        match gameState.gameFrame with
        | Win _ -> gameState
        | Loss _ -> gameState
        | Frame field ->
            let field =
                if not field.isExitOpen && gameState.snake.length >= field.minimumWinLength
                then Field.openExit field
                else field
            let commands = filterCommands field gameState.snake commands
            let isPointOutside p = Field.isPointInside struct(field.width, field.height) p |> not
            let snake = commands |> Snake.applyCommands gameState.snake |> Snake.tick
            let eaters = getNextEatersStep field snake gameState.eaters
            let snakeAteEater = getEaterEatenBySnake snake eaters
            let (snake,newEaters) = 
                match snakeAteEater with
                | Some e -> (Snake.growUp snake, List.filter ((compareStructTuple e)>>not) eaters)
                | None -> (snake, eaters)
            let snakeCoordinates = Field.getSnakeCoordinates snake.body snake.headPoint
            let isSnakeOutside = List.fold (fun acc point -> acc && isPointOutside point) true snakeCoordinates

            let updateCellMap snakeCoordinates = updateCellMap field newEaters snakeCoordinates

            if isSnakeOutside
            then { gameFrame = Win snake.length; snake = snake; eaters = newEaters }
            elif isSnakeCrossingItself snakeCoordinates || isSnakeCrossedObstacle field snakeCoordinates
            then { gameFrame = Loss "snake crossed itself or obstacle"; snake = snake; eaters = newEaters}
            elif isEatersWin snakeCoordinates newEaters
            then {gameFrame = Loss "eater ate your snake"; snake = snake; eaters = newEaters}
            else
            match field.TryGetCell snake.headPoint with
            | None -> { gameFrame = updateCellMap snakeCoordinates |> Frame; snake = snake; eaters = newEaters}
            | Some cell ->
                match cell.content with
                | Obstacle -> { gameFrame = Loss "snake crossed obstacle"; snake = snake ; eaters = newEaters}
                | Empty | SnakeCell | Exit-> { gameFrame = updateCellMap snakeCoordinates |> Frame; snake = snake; eaters = newEaters}
                | Food -> let snake = Snake.growUp snake
                          let snakeCoordinates = Field.getSnakeCoordinates snake.body snake.headPoint
                          Field.spawnFoodInRandomCell field
                          { gameFrame = updateCellMap snakeCoordinates |> Frame; snake = snake; eaters = newEaters}
                | Eater ->
                    let snake = Snake.growUp snake
                    let snakeCoordinates = Field.getSnakeCoordinates snake.body snake.headPoint
                    let aliveEaters = List.filter ((compareStructTuple snake.headPoint)>>not) newEaters
                    { gameFrame = updateCellMap snakeCoordinates |> Frame; 
                      snake = snake; 
                      eaters = aliveEaters}

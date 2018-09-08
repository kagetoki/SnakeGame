namespace SneakySnake

[<Struct>]
type GameResult =
    | Win of points:uint16
    | Loss of string

type GameFrame =
    | Frame of field
    | End of GameResult

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
                | Empty | Food | SnakeCell -> 
                    let otherCandidates = List.filter (fun (_, n) -> areEqualStructTuples n nextEater) nextEaters |> List.length
                    if otherCandidates > 1 then eater else nextEater
                | Obstacle | Exit | Eater -> eater
        let res = List.map select nextEaters
        res

    let private getEaterEatenBySnake (snake: SnakeState) eaters =
        if snake.HasPerk Attack |> not then None
        else
        let nextSnakePoint = Field.nextCoordinate snake.direction.Opposite snake.headPoint
        orElse {
            return! Seq.tryFind (areEqualStructTuples snake.headPoint) eaters
            return! Seq.tryFind (areEqualStructTuples nextSnakePoint) eaters
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

    let private openExitIfNeeded field snake =
        if not field.isExitOpen && snake.length >= field.minimumWinLength
        then Field.openExit field
        else field

    let private tryEndGame field snakeCoordinates eaters =
        let isPointOutside p = Field.isPointInside struct(field.width, field.height) p |> not
        let isSnakeOutside = List.fold (fun acc point -> acc && isPointOutside point) true snakeCoordinates
        if isSnakeOutside
        then Seq.length snakeCoordinates |> uint16 |> Win |> Some
        elif isSnakeCrossingItself snakeCoordinates
        then Loss "snake bit itself" |> Some
        elif isSnakeCrossedObstacle field snakeCoordinates
        then Loss "snake hit obstacle" |> Some
        elif isEatersWin snakeCoordinates eaters
        then Loss "eater ate your snake" |> Some
        else None

    let updateGameState gameState commands =
        match gameState.gameFrame with
        | End _ -> gameState
        | Frame field ->
            let field = openExitIfNeeded field gameState.snake
            let commands = filterCommands field gameState.snake commands
            let snake = commands |> Snake.applyCommands gameState.snake |> Snake.tick
            let eaters = getNextEatersStep field snake gameState.eaters
            let snakeAteEater = getEaterEatenBySnake snake eaters
            let (snake,newEaters) = 
                match snakeAteEater with
                | Some e -> (Snake.growUp snake, List.filter ((areEqualStructTuples e)>>not) eaters)
                | None -> (snake, eaters)

            let snakeCoordinates = Field.getSnakeCoordinates snake.body snake.headPoint

            let updateCellMap = updateCellMap field newEaters

            match tryEndGame field snakeCoordinates newEaters with
            | Some result -> {gameState with gameFrame = End result}
            | None ->
            let nextFrame snake field = { gameFrame = Frame field; snake = snake; eaters = newEaters }
            match field.TryGetCell snake.headPoint with
            | None -> updateCellMap snakeCoordinates |> nextFrame snake
            | Some cell ->
                match cell.content with
                | Obstacle ->
                    { gameFrame = Loss "snake crossed obstacle" |> End; snake = snake ; eaters = newEaters }
                | Empty | SnakeCell | Exit ->
                    updateCellMap snakeCoordinates |> nextFrame snake
                | Food -> 
                    let snake = Snake.growUp snake
                    let snakeCoordinates = Field.getSnakeCoordinates snake.body snake.headPoint
                    Field.spawnFoodInRandomCell field
                    updateCellMap snakeCoordinates |> nextFrame snake
                | Eater ->
                    let snake = Snake.growUp snake
                    let snakeCoordinates = Field.getSnakeCoordinates snake.body snake.headPoint
                    let aliveEaters = List.filter ((areEqualStructTuples snake.headPoint)>>not) newEaters
                    { gameFrame = updateCellMap snakeCoordinates |> Frame; 
                      snake = snake; 
                      eaters = aliveEaters }

namespace SnakeGame

module GameRules =

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

    let buildNextGameFrame sendGrowUpMessage field snake eaters =
        let snakeCoordinates = Field.getSnakeCoordinates snake.body snake.headPoint
        let isPointOutside p = Field.isPointInside struct(field.width, field.height) p |> not
        let isSnakeOutside = List.fold (fun acc point -> acc && isPointOutside point) true snakeCoordinates
        let buildNextFrame() = buildNextFrame field snakeCoordinates eaters
        if isSnakeOutside
        then Win
        elif isSnakeCrossingItself snakeCoordinates || isSnakeCrossedObstacle field snakeCoordinates
        then Loss
        else
        match field.TryGetCell snake.headPoint with
        | Some cell when cell.content = Obstacle -> Loss
        | None -> buildNextFrame()
        | Some cell ->
            match cell.content with
            | Obstacle | SnakeCell -> Loss
            | Empty | Exit-> buildNextFrame()
            | Food -> sendGrowUpMessage()
                      buildNextFrame()
            | Eater -> 
                if not <| snake.HasPerk AttackMode then Loss
                else sendGrowUpMessage()
                     buildNextFrame()

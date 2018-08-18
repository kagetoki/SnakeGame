namespace SnakeGame

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

    let private isSnakeCrossedByEaters snakeCoordinates eaters =
        let snakeSet = Set.ofSeq snakeCoordinates
        let eaterSet = Set.ofSeq eaters
        Set.intersect snakeSet eaterSet
        |> Seq.length > 0

    let private updateCellMap (field:field) snake eaters =
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

    let evaluateNextEaterCoordinates eaters (struct(i,j) as snakeHead) =
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

    let getNextEatersStep (field:field) snake eaters=
        //let eaters = field.cellMap |> Seq.cast<Cell> |> Seq.filter (fun c -> c.content = Eater) |> Seq.map (fun c -> struct(c.x, c.y))
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

    let private spawnFoodInRandomCell (field:field) =
        let getRandomCoordinate() = getRandomCoordinate field.width field.height
        let rec spawn () =
            let struct (x,y) = getRandomCoordinate()
            match field.cellMap.[x, y].content with
            | Empty -> field.cellMap.[x, y] <- {x = x; y = y; content = Food}
            | _ -> spawn()
        spawn()

    let buildNextGameFrame growSnakeUp field snake eaters =
        //let eaters = getNextEatersStep field snake
        let snakeCoordinates = Field.getSnakeCoordinates snake.body snake.headPoint
        let isPointOutside p = Field.isPointInside struct(field.width, field.height) p |> not
        let isSnakeOutside = List.fold (fun acc point -> acc && isPointOutside point) true snakeCoordinates
        let updateCellMap() = updateCellMap field snakeCoordinates eaters
        if isSnakeOutside
        then Win snake.length
        elif isSnakeCrossingItself snakeCoordinates || isSnakeCrossedObstacle field snakeCoordinates
        then Loss "snake crossed itself or obstacle"
        else
        match field.TryGetCell snake.headPoint with
        | None -> updateCellMap()
        | Some cell ->
            match cell.content with
            | Obstacle -> Loss "snake crossed obstacle"
            | Empty | SnakeCell | Exit-> updateCellMap()
            | Food -> growSnakeUp()
                      spawnFoodInRandomCell field
                      updateCellMap()
            | Eater ->
                if not <| snake.HasPerk AttackMode then Loss "eater ate your snake"
                else growSnakeUp()
                     updateCellMap()

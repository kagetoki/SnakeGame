namespace SneakySnake

[<Struct>]
type CellContent =
    | Empty
    | Food
    | Obstacle
    | SnakeCell
    | Eater
    | Exit

[<Struct>]
type Cell =
    {
        x:int
        y:int
        content: CellContent
    }

type field = 
    {
        cellMap: Cell [,]
        width: int
        height: int
        snakeStart: struct(int*int)
        eaters: struct(int*int) seq
        perksTtl:uint16
        minimumWinLength: uint16
        exitPoint: struct(int*int)
        perksAvailabilityMap: Map<SnakePerk, uint16>
        isExitOpen: bool
    } with member this.TryGetCell struct(i, j) =
            if i < 0 || j < 0 || i >= this.width || j >= this.height
            then None
            else this.cellMap.[i,j] |> Some

module Field =

    let private random = new System.Random()

    let isPointInside struct(width, height) struct(x,y) =
        x >= 0 && x < width && y >= 0 && y < height
    let nextCoordinate dir struct(x, y) =
        match dir with
        | Up -> struct(x, y + 1)
        | Down -> struct(x, y - 1)
        | Right -> struct(x + 1, y)
        | Left -> struct(x - 1, y)

    let private getBodyPartCoordinates (dir: Direction) len struct(x, y) =
        let rec collect len struct(x', y') acc =
            match len with
            | 0us -> acc
            | len ->
                let next = nextCoordinate dir.Opposite struct(x', y')
                next::acc |> collect (len - 1us) next
        collect len struct(x, y) []

    let getSnakeCoordinates snakeBody headPoint =
        let rec collect body head acc =
            match body with
            | Tail -> acc
            | Segment (dir, len, next) ->
                let partCoordinates = getBodyPartCoordinates dir len head
                let nextCoord = partCoordinates.Head
                partCoordinates @ acc |> collect next nextCoord
        let result = collect snakeBody headPoint [] |> List.rev
        headPoint::result

    let init width height cellMap =
        let mapCell i j =
            match Map.tryFind struct(i,j) cellMap with
            | None ->
                if i = 0 || j = 0 || i = width - 1 || j = height - 1
                then {x = i; y = j; content = Obstacle}
                else {x = i; y = j; content = Empty}
            | Some cell -> cell
        Array2D.init width height mapCell

    let private getRandomCoordinate width height =
        struct (random.Next(1, width - 2), random.Next(1, height - 2))

    let spawnFoodInRandomCell (field:field) =
        let getRandomCoordinate() = getRandomCoordinate field.width field.height
        let rec spawn () =
            let struct (x,y) = getRandomCoordinate()
            match field.cellMap.[x, y].content with
            | Empty -> field.cellMap.[x, y] <- {x = x; y = y; content = Food}
            | _ -> spawn()
        spawn()

    let openExit (field:field) =
        match field.TryGetCell field.exitPoint with
        | None -> field
        | Some cell ->
            field.cellMap.[cell.x, cell.y] <- {cell with content = Exit}
            {field with isExitOpen = true}


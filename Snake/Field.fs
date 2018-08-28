namespace SnakeGame

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

[<Struct>]
type FieldInfo =
    {
        width:int
        height:int
    }

type field = 
    {
        cellMap: Cell [,]
        width: int
        height: int
    } with member this.TryGetCell struct(i, j) =
            if i < 0 || j < 0 || i >= this.width || j >= this.height
            then None
            else this.cellMap.[i,j] |> Some

type GameFrame =
    | Frame of field
    | Win of points:uint16
    | Loss of string


module Field =

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

    let nextFrame (field:field) fieldUpdates =
        let isCellInside cell = isPointInside struct(field.width, field.height) struct(cell.x, cell.y)
        let fieldUpdates = Seq.filter isCellInside fieldUpdates
        for cell in fieldUpdates do
            field.cellMap.[cell.x,cell.y] <- cell
        field

    let getStartField() =
        let width = 50
        let height = 20
        let cells = 
            [
                {x = width - 1; y = height - 2; content = Exit}
                {x = 5; y = 8; content = Food}
                {x = 12; y = height - 4; content = Food}
                {x = 15; y = 7; content = Food}
                {x = 2; y = 10; content = Food}
            ] |> List.map (fun c -> (struct(c.x, c.y),c)) |> Map.ofList

        {
            width = width;
            height = height;
            cellMap = init width height cells
        }

namespace SneakySnake

open System
open System.Reflection
open Microsoft.FSharpLu.Json

type LevelInfo =
    {
        snakeStart: struct(int*int)
        eaters: struct(int*int) list
        perksTtl:uint16
        minimumWinLength: uint16
        exitPoint: struct(int*int)
        perksMap: (SnakePerk* uint16) list
    }

module LevelSource =
    open System.IO

    let levelWidth = 50
    let levelHeight = 20

    let [<Literal>] ObstacleChar = 'X'
    let [<Literal>] EmptyChar = ' '

    let private parseCellMap (mapString: string) =
        let lines = mapString.Split([|Environment.NewLine|], StringSplitOptions.RemoveEmptyEntries)
        let cellMap = Array2D.init levelWidth levelHeight (fun x y -> {x = x; y = y; content = Empty})
        let parseCellContent =
            function
            | ObstacleChar -> Obstacle
            | EmptyChar -> Empty
            | c -> invalidArg "char" "unknown cell content"
        for y in levelHeight - 1..-1..0 do
            for x in 0..levelWidth - 1 do
                let line = lines.[y]
                let c = line.[x]
                let content = parseCellContent c
                cellMap.[x,y] <- {cellMap.[x,y] with content = content }
        cellMap

    let private parseLevelInfo levelString =
        Compact.deserialize<LevelInfo> levelString

    let private parseField (levelString, mapString) =
        let level = parseLevelInfo levelString
        let cellMap = parseCellMap mapString
        let field =
            {
                perksAvailabilityMap = level.perksMap |> Map.ofList
                snakeStart = level.snakeStart
                eaters = level.eaters
                perksTtl = level.perksTtl
                cellMap = cellMap
                width = levelWidth
                height = levelHeight
                minimumWinLength = level.minimumWinLength
                exitPoint = level.exitPoint
                isExitOpen = false
            }
        for _ in 0..4 do
            Field.spawnFoodInRandomCell field
        field

    let private splitSerializedLevels (levels:string) =
        let levels = levels.Split([|"###"|], StringSplitOptions.RemoveEmptyEntries)
        let splitLevel (level:string) =
            let parts = level.Split([|"@"|], StringSplitOptions.RemoveEmptyEntries)
            (parts.[0], parts.[1])
        Array.map splitLevel levels

    let private parseAllFields levelsString =
        let levels = splitSerializedLevels levelsString
        Array.map parseField levels

    let private readEmbeddedLevels() =
        let assembly = Assembly.GetExecutingAssembly()
        use stream = assembly.GetManifestResourceStream "Snake.Resources.Levels.txt"
        use streamReader = new StreamReader(stream)
        streamReader.ReadToEnd()

    let loadAllLevels = readEmbeddedLevels>>parseAllFields

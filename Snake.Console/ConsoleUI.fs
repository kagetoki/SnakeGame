namespace SnakeGame
open System

[<RequireQualifiedAccess>]
module ConsoleUI =

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

    let print =
        function
        | Loss str as loss -> printfn "%A" loss
        | Win -> printfn "Win"
        | Frame field ->
            System.Console.Clear()
            for j in field.height - 1..-1..0 do
                for i in 0..field.width - 1 do
                    field.cellMap.[i,j].content |> printCell
                Console.WriteLine()


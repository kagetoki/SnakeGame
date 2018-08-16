namespace SnakeGame
open System
open System.Text

[<RequireQualifiedAccess>]
module ConsoleUI =

    let printInColor color (text:string) =
        let oldColor = Console.ForegroundColor
        Console.ForegroundColor <- color
        Console.Write(text)
        Console.ForegroundColor <- oldColor

    let printCell =
        function
        | SnakeCell -> printInColor ConsoleColor.Green "s"
        | Eater -> printInColor ConsoleColor.Red "e"
        | Obstacle -> printInColor ConsoleColor.DarkYellow "X"
        | Exit -> printInColor ConsoleColor.Blue "O"
        | Empty -> Console.Write(" ")
        | Food -> printInColor ConsoleColor.Magenta "o"

    let toChar =
        function
        | SnakeCell -> 's'
        | Eater -> 'e'
        | Obstacle -> 'X'
        | Exit -> 'O'
        | Empty -> ' '
        | Food -> 'o'

    let print =
        function
        | Loss str as loss -> printfn "%A" loss
        | Win -> printfn "Win"
        | Frame field ->
            System.Console.Clear()
            let sb = StringBuilder()
            for j in field.height - 1..-1..0 do
                for i in 0..field.width - 1 do
                    //field.cellMap.[i,j].content |> printCell
                    sb.Append(toChar field.cellMap.[i,j].content) |> ignore
                //Console.WriteLine()
                sb.AppendLine() |> ignore
            sb.ToString() |> Console.WriteLine


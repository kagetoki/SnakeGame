namespace SnakeGame

open System
open System.Text
open System.Collections.Generic
open System.Linq

type PrintString =
    {
        color: ConsoleColor
        stringBuilder: StringBuilder
        symbol: char
    }

[<RequireQualifiedAccess>]
module ConsoleUI =

    let private printInColor color (text:string) =
        let oldColor = Console.ForegroundColor
        Console.ForegroundColor <- color
        Console.Write(text)
        Console.ForegroundColor <- oldColor

    let private toChar =
        function
        | SnakeCell -> 's'
        | Eater -> 'e'
        | Obstacle -> 'X'
        | Exit -> 'O'
        | Empty -> ' '
        | Food -> 'o'

    let private toColor =
        function
        | SnakeCell -> (ConsoleColor.Green)
        | Eater -> (ConsoleColor.Red)
        | Obstacle -> (ConsoleColor.DarkYellow)
        | Exit -> (ConsoleColor.Blue)
        | Empty -> (ConsoleColor.Black)
        | Food -> (ConsoleColor.Magenta)

    let private printField field =
        let buffer = new List<PrintString>()
        let getSymbol i j = field.cellMap.[i,j].content |> toChar
        let c = getSymbol 0 0
        buffer.Add({symbol = c; stringBuilder = StringBuilder(); color = field.cellMap.[0,0].content |> toColor})
        for j in field.height - 1..-1..0 do
            for i in 0..field.width - 1 do
                let content = field.cellMap.[i,j].content
                let symbol = toChar content
                if buffer.Last().symbol = symbol || symbol = ' ' then
                    buffer.Last().stringBuilder.Append symbol |> ignore
                else buffer.Add({symbol = symbol; stringBuilder = StringBuilder().Append(symbol); color = toColor content})
            buffer.Last().stringBuilder.AppendLine() |> ignore

        for str in buffer do
            str.stringBuilder.ToString()
            |> printInColor str.color

    let print =
        function
        | Loss str as loss -> sprintf "%A" loss |> printInColor ConsoleColor.Red
        | Win points -> sprintf "Congratulations! You've got %i points!\n" points |> printInColor ConsoleColor.Green 
        | Frame field ->
            System.Console.Clear()
            printField field


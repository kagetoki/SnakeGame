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
        //let oldColor = Console.ForegroundColor
        Console.ForegroundColor <- color
        Console.Write(text)
        //Console.ForegroundColor <- oldColor

    let private toChar =
        function
        | SnakeCell -> 's'
        | Eater -> 'e'
        | Obstacle -> 'X'
        | Exit -> 'O'
        | Empty -> ' '
        | Food -> 'o'

    let private toColor (snake: SnakeState) =
        let snakeColor =
            if snake.HasPerk Attack then ConsoleColor.DarkRed
            else ConsoleColor.Green
        function
        | SnakeCell -> snakeColor
        | Eater -> (ConsoleColor.Red)
        | Obstacle -> (ConsoleColor.DarkYellow)
        | Exit -> (ConsoleColor.Blue)
        | Empty -> (ConsoleColor.Black)
        | Food -> (ConsoleColor.Magenta)

    let private printHead ((field:field), snake) =
        let headColor = ConsoleColor.Cyan
        let print perk threashold =
            let pointsLeft = if threashold > snake.length then threashold - snake.length else 0us
            match pointsLeft with
            | 0us -> sprintf "You can use %A mode!\n\n" perk |> printInColor headColor
            | _ ->
                sprintf "%i points untill you can use %A mode!\n\n" pointsLeft perk
                |> printInColor headColor
        Map.iter print field.perksAvailabilityMap
        if field.minimumWinLength > snake.length then
            sprintf "Exit will be open in %i points\n" (field.minimumWinLength - snake.length)
            |> printInColor headColor

    let private printField ((field, snake) as data) =
        printHead data
        let toColor = toColor snake
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

    let printGameResult =
        function
        | Loss str as loss -> sprintf "%A\n" loss |> printInColor ConsoleColor.Red
        | Win points -> sprintf "Congratulations! You've got %i points!\n" points |> printInColor ConsoleColor.Green 


    let print gameState =
        match gameState.gameFrame with
        | End result -> printGameResult result
        | Frame field ->
            System.Console.Clear()
            printField (field, gameState.snake)


// Learn more about F# at http://fsharp.org

open System
open SnakeGame
open SnakeGame.Game

let printSnake snake =
    printfn "snake:\n%A" snake

let printCoordinates coordinates =
    printfn "coordinates:"
    for c in coordinates do
        printf "%A " c
    printfn ""

let rec readCommand() =
    let input = Console.ReadKey()

    match input.Key with
    | ConsoleKey.Q -> ()
    | ConsoleKey.A ->
        Add AttackMode |> Perk |> Cmd
        |> commandAgent.Post
        readCommand()
    | ConsoleKey.S ->
        Add Speed |> Perk |> Cmd
        |> commandAgent.Post
        readCommand()
    | ConsoleKey.D ->
        Add Armor |> Perk |> Cmd
        |> commandAgent.Post
        readCommand()
    | ConsoleKey.UpArrow ->
        Move Up |> Cmd |> commandAgent.Post
        readCommand()
    | ConsoleKey.DownArrow ->
        Move Down |> Cmd |> commandAgent.Post
        readCommand()
    | ConsoleKey.LeftArrow ->
        Move Left |> Cmd |> commandAgent.Post
        readCommand()
    | ConsoleKey.RightArrow ->
        Move Right |> Cmd |> commandAgent.Post
        readCommand()
    | _ -> readCommand()

[<EntryPoint>]
let main argv =
    Game.timerAgent.Post Start

    readCommand()
    Game.timerAgent.Post Stop
    //Console.ReadLine() |> ignore
    0 // return an integer exit code

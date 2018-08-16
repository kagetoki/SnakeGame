// Learn more about F# at http://fsharp.org

open System
open SnakeGame
open PostOffice

let printSnake snake =
    printfn "snake:\n%A" snake

let printCoordinates coordinates =
    printfn "coordinates:"
    for c in coordinates do
        printf "%A " c
    printfn ""

let rec readCommand (commandAgent: Agent<CommandMessage, Command list>) =
    let input = Console.ReadKey()

    match input.Key with
    | ConsoleKey.Q -> ()
    | ConsoleKey.A ->
        Add AttackMode |> Perk |> Cmd
        |> commandAgent.Post
        readCommand commandAgent
    | ConsoleKey.S ->
        Add Speed |> Perk |> Cmd
        |> commandAgent.Post
        readCommand commandAgent
    | ConsoleKey.D ->
        Add Armor |> Perk |> Cmd
        |> commandAgent.Post
        readCommand commandAgent
    | ConsoleKey.UpArrow ->
        Move Up |> Cmd |> commandAgent.Post
        readCommand commandAgent
    | ConsoleKey.DownArrow ->
        Move Down |> Cmd |> commandAgent.Post
        readCommand commandAgent
    | ConsoleKey.LeftArrow ->
        Move Left |> Cmd |> commandAgent.Post
        readCommand commandAgent
    | ConsoleKey.RightArrow ->
        Move Right |> Cmd |> commandAgent.Post
        readCommand commandAgent
    | _ -> readCommand commandAgent

[<EntryPoint>]
let main argv =
    let system = MailboxSystem()
    let gameSystem = GameBuilder.buildSnakeGame system ConsoleUI.print

    //gameSystem.timerAgent.Post Start
    gameSystem.timerAgent.Post Start
    readCommand gameSystem.commandAgent
    //gameSystem.timerAgent.Post Stop
    0 // return an integer exit code

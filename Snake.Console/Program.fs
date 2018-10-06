// Learn more about F# at http://fsharp.org

open System
open SneakySnake
open PostOffice

let buildGame startLevel =
    let system = new MailboxNetwork()
    let game = GameBuilder.buildSnakeGame system startLevel
    game.AddSubscriber ConsoleUI.print
    game

let readUserCommand key =
    match key with
    | ConsoleKey.A -> Add Attack |> Perk |> SnakeCommand |> Some
    | ConsoleKey.R -> Restart |> Some
    | ConsoleKey.S -> Add Speed |> Perk |> SnakeCommand |> Some
    | ConsoleKey.DownArrow -> Move Down |> SnakeCommand |> Some
    | ConsoleKey.UpArrow -> Move Up |> SnakeCommand |> Some
    | ConsoleKey.LeftArrow -> Move Left |> SnakeCommand |> Some
    | ConsoleKey.RightArrow -> Move Right |> SnakeCommand |> Some
    | ConsoleKey.Q -> Quit |> Some
    | ConsoleKey.Spacebar -> StartPause |> Some
    | _ -> None

let rec read (snakeMailboxSystem:SnakemailboxNetwork) =
    let input = Console.ReadKey()
    let command = readUserCommand input.Key
    match command with
    | None -> read snakeMailboxSystem
    | Some Quit -> snakeMailboxSystem.Kill()
    | Some cmd ->
        let game = GameInterface.passCommand ConsoleUI.print snakeMailboxSystem cmd
        read game

[<EntryPoint>]
let main argv =
    let gameSystem = buildGame 0
    ConsoleUI.printHelp()
    gameSystem.timerAgent.Post Start
    read gameSystem
    0 // return an integer exit code

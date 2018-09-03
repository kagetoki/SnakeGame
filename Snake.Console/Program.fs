// Learn more about F# at http://fsharp.org

open System
open SnakeGame
open PostOffice

let buildGame() =
    let system = MailboxNetwork()
    GameBuilder.buildSnakeGame system ConsoleUI.print

let rec readCommand (snakeMailboxSystem) =
    let commandAgent = snakeMailboxSystem.commandAgent
    let input = Console.ReadKey()

    match input.Key with
    | ConsoleKey.Q -> 
        snakeMailboxSystem.gameAgent.Kill()
        snakeMailboxSystem.commandAgent.Kill()
    | ConsoleKey.A ->
        Add Attack |> Perk |> Cmd
        |> commandAgent.Post
        readCommand snakeMailboxSystem
    | ConsoleKey.S ->
        Add Speed |> Perk |> Cmd
        |> commandAgent.Post
        readCommand snakeMailboxSystem
    | ConsoleKey.UpArrow ->
        Move Up |> Cmd |> commandAgent.Post
        readCommand snakeMailboxSystem
    | ConsoleKey.DownArrow ->
        Move Down |> Cmd |> commandAgent.Post
        readCommand snakeMailboxSystem
    | ConsoleKey.LeftArrow ->
        Move Left |> Cmd |> commandAgent.Post
        readCommand snakeMailboxSystem
    | ConsoleKey.RightArrow ->
        Move Right |> Cmd |> commandAgent.Post
        readCommand snakeMailboxSystem
    | ConsoleKey.R -> 
        commandAgent.Kill()
        snakeMailboxSystem.gameAgent.Kill()
        let newSystem = buildGame()
        newSystem.timerAgent.Post Start
        readCommand newSystem
    | ConsoleKey.Spacebar -> 
        snakeMailboxSystem.timerAgent.Post PauseOrResume
        readCommand snakeMailboxSystem
    | _ -> readCommand snakeMailboxSystem



[<EntryPoint>]
let main argv =
    let gameSystem = buildGame()

    gameSystem.timerAgent.Post Start
    readCommand gameSystem
    0 // return an integer exit code

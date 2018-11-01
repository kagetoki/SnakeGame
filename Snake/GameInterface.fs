namespace SneakySnake
open System
open PostOffice

[<Struct>]
type UserCommand =
    | StartPause
    | IncreaseGameSpeed
    | Restart
    | Quit
    | SnakeCommand of Command

module GameInterface =

    let buildGame updateUI startLevel =
        let system = new MailboxNetwork()
        let game = GameBuilder.buildSnakeGame system startLevel
        game.AddSubscriber updateUI
        game

    let passCommand updateUi snakeMailboxSystem =
        function
        | StartPause ->
            snakeMailboxSystem.timerAgent.Post PauseOrResume
            snakeMailboxSystem
        | SnakeCommand cmd ->
            AppendCommand cmd
            |> snakeMailboxSystem.commandAgent.Post
            snakeMailboxSystem
        | IncreaseGameSpeed ->
            IncreaseDelay -10
            |> snakeMailboxSystem.timerAgent.Post
            snakeMailboxSystem
        | Restart ->
            let startLevel =
                match snakeMailboxSystem.gameAgent with
                | Box m ->
                    m.GetState().startLevel
                | _ -> 0
            snakeMailboxSystem.Kill()
            let newSystem = buildGame updateUi startLevel
            newSystem.timerAgent.Post Start
            newSystem
        | Quit ->
            snakeMailboxSystem.Kill();
            snakeMailboxSystem

    let passCommandInterop (action:Action<GameState>) snakeMailboxNetwork cmd =
        let updateUi g = action.Invoke(g)
        passCommand updateUi snakeMailboxNetwork cmd

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
        | ConsoleKey.G -> IncreaseGameSpeed |> Some
        | _ -> None

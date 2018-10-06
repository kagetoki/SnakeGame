namespace SneakySnake
open PostOffice

[<Struct>]
type UserCommand =
    | StartPause
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

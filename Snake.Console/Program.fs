// Learn more about F# at http://fsharp.org

open System
open SneakySnake
open PostOffice

let buildGame startLevel =
    let system = new MailboxNetwork()
    let game = GameBuilder.buildSnakeGame system startLevel
    game.AddSubscriber ConsoleUI.print
    game

let rec executeUserCommand (snakeMailboxSystem:SnakeMailboxNetwork) =
    let input = Console.ReadKey()
    let command = GameInterface.readUserCommand input.Key
    match command with
    | None -> executeUserCommand snakeMailboxSystem
    | Some Quit -> snakeMailboxSystem.Kill()
    | Some cmd ->
        let game = GameInterface.passCommand ConsoleUI.print snakeMailboxSystem cmd
        executeUserCommand game

[<EntryPoint>]
let main argv =
    let gameSystem = buildGame 0
    ConsoleUI.printHelp()
    gameSystem.timerAgent.Post Start
    executeUserCommand gameSystem
    0 // return an integer exit code

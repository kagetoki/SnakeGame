namespace SnakeGame

open System
open System.Timers
open PostOffice

type CommandMessage =
    | Cmd of Command
    | Flush

type SnakeStateMessage =
    | Commands of Command list
    | GrowUp
    | Tick

[<Struct>]
type TimerCommand =
    | Start
    | Pause
    | Stop
    | Subscribe of (unit -> unit)

type SnakeMailboxSystem =
    {
        timerAgent: Agent<TimerCommand, Timer>
        snakeAgent: Agent<SnakeStateMessage, SnakeState>
        fieldAgent: Agent<SnakeState, GameFrame>
        commandAgent: Agent<CommandMessage, Command list>
        mailboxSystem: MailboxSystem
    }

[<RequireQualifiedAccess>]
module GameBuilder =

    let [<Literal>] fieldAddress = "field"
    let [<Literal>] snakeAddress = "snake"
    let [<Literal>] commandAddress = "command"
    let [<Literal>] timerAddress = "timer"

    let snakeAgent (mailboxSystem: MailboxSystem) = mailboxSystem.Box<SnakeStateMessage, SnakeState>(snakeAddress)
    let timerAgent (mailboxSystem: MailboxSystem) = mailboxSystem.Box<TimerCommand, Timer>(timerAddress)
    let fieldAgent (mailboxSystem: MailboxSystem) = mailboxSystem.Box<SnakeState, GameFrame>(fieldAddress)
    let commandAgent (mailboxSystem: MailboxSystem) = mailboxSystem.Box<CommandMessage, Command list>(commandAddress)

    let fieldAgentFn (mailboxSystem: MailboxSystem) updateUi gameState snake =
        let timerAgent = timerAgent mailboxSystem
        let snakeAgent = snakeAgent mailboxSystem
        let gameState =
            match gameState with
            | Frame field -> snake |> Game.buildNextGameFrame (fun () -> snakeAgent.Post GrowUp) field
            | state -> 
                timerAgent.Post Stop
                state
        updateUi gameState
        gameState

    let snakeAgentFn (mailboxSystem: MailboxSystem) snake message =
        let fieldAgent = fieldAgent mailboxSystem
        let snake =
            match message with
            | Commands commands -> Snake.applyCommands snake commands
            | GrowUp -> Snake.growUp snake
            | Tick -> Snake.tick snake
        fieldAgent.Post snake
        snake

    let commandAgentFn (mailboxSystem: MailboxSystem) commands msg =
        let snakeAgent = snakeAgent mailboxSystem
        match msg with
        | Cmd cmd -> cmd::commands
        | Flush ->
            Snake.mergeCommands commands
                |> Commands
                |> snakeAgent.Post
            []

    let timerAgentFn (timer: Timer) cmd =
        match cmd with
        | Start -> timer.Start()
        | Pause -> timer.Stop()
        | Stop -> timer.Stop(); timer.Dispose()
        | Subscribe fn -> timer.Elapsed.Add (fun x -> fn())
        timer

    let setUpTimer (mailboxSystem: MailboxSystem) =
        let timer = new Timer()
        timer.Interval <- 500.0
        timer.Elapsed.Add (fun x ->
            (commandAgent mailboxSystem).Post Flush;
            (snakeAgent mailboxSystem).Post Tick;
            )
        timer

    let buildSnakeGame (mailboxSystem: MailboxSystem) updateUi =
        let fieldAgentFn = fieldAgentFn mailboxSystem updateUi
        let commandAgentFn = commandAgentFn mailboxSystem
        let snakeAgentFn = snakeAgentFn mailboxSystem
        
        let fieldAgent = (fieldAddress, Field.getStartField() |> Frame |> Mailbox.buildAgent fieldAgentFn) |> MailAgent
        let snakeAgent = (snakeAddress, Snake.getDefaultSnakeState struct(4, 5) |> Mailbox.buildAgent snakeAgentFn) |> MailAgent 
        let commandAgent = (commandAddress, []|> Mailbox.buildAgent commandAgentFn) |> MailAgent 
        let timerAgent = (timerAddress, setUpTimer mailboxSystem |> Mailbox.buildAgent timerAgentFn) |> MailAgent 
        mailboxSystem.RespawnBox fieldAgent
        mailboxSystem.RespawnBox snakeAgent
        mailboxSystem.RespawnBox commandAgent
        mailboxSystem.RespawnBox timerAgent
        {
            fieldAgent = Box fieldAgent
            snakeAgent = Box snakeAgent
            commandAgent = Box commandAgent
            timerAgent = Box timerAgent
            mailboxSystem = mailboxSystem
        }

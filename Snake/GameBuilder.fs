namespace SnakeGame

open System
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
    | Next
    | Stop
    | SetDelay of int

[<Struct>]
type TimerState =
    {
        active: bool
        delay: int
    }

type SnakeMailboxSystem =
    {
        snakeAgent: Agent<SnakeStateMessage, SnakeState>
        fieldAgent: Agent<SnakeState, GameFrame>
        commandAgent: Agent<CommandMessage, Command list>
        timerAgent: Agent<TimerCommand, TimerState>
        mailboxSystem: MailboxSystem
    }

[<RequireQualifiedAccess>]
module GameBuilder =

    let [<Literal>] fieldAddress = "field"
    let [<Literal>] snakeAddress = "snake"
    let [<Literal>] commandAddress = "command"
    let [<Literal>] timerAddress = "timer"

    let snakeAgent (mailboxSystem: MailboxSystem) = mailboxSystem.Box<SnakeStateMessage, SnakeState>(snakeAddress)
    let fieldAgent (mailboxSystem: MailboxSystem) = mailboxSystem.Box<SnakeState, GameFrame>(fieldAddress)
    let commandAgent (mailboxSystem: MailboxSystem) = mailboxSystem.Box<CommandMessage, Command list>(commandAddress)
    let timerAgent (mailboxSystem: MailboxSystem) = mailboxSystem.Box<TimerCommand, TimerState>(timerAddress)

    let fieldAgentFn (mailboxSystem: MailboxSystem) updateUi gameState snake =
        let snakeAgent = snakeAgent mailboxSystem
        let timerAgent = timerAgent mailboxSystem
        let gameState =
            match gameState with
            | Frame field -> 
                let state = snake |> Game.buildNextGameFrame (fun () -> snakeAgent.Post GrowUp) field
                timerAgent.Post Next
                state
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
            commands
                |> Commands
                |> snakeAgent.Post
            snakeAgent.Post Tick
            []

    let timerAgentFn (mailboxSystem: MailboxSystem) (state: TimerState) cmd =
        let commandAgent = commandAgent mailboxSystem
        match cmd with
        | Start -> commandAgent.Post Flush; {state with active = true}
        | Next -> 
            if state.active then
                Threading.Thread.Sleep(state.delay)
                commandAgent.Post Flush; 
            state
        | Stop -> { state with active = false }
        | SetDelay delay -> 
            Threading.Thread.Sleep(delay)
            if state.active then
                commandAgent.Post Flush
            {state with delay = delay}

    let buildSnakeGame (mailboxSystem: MailboxSystem) updateUi =
        let fieldAgentFn = fieldAgentFn mailboxSystem updateUi
        let commandAgentFn = commandAgentFn mailboxSystem
        let snakeAgentFn = snakeAgentFn mailboxSystem
        let timerAgentFn = timerAgentFn mailboxSystem
        
        let fieldAgent = (fieldAddress, Field.getStartField() |> Frame |> Mailbox.buildAgent fieldAgentFn) |> MailAgent
        let snakeAgent = (snakeAddress, Snake.getDefaultSnakeState struct(4, 5) |> Mailbox.buildAgent snakeAgentFn) |> MailAgent 
        let commandAgent = (commandAddress, []|> Mailbox.buildAgent commandAgentFn) |> MailAgent 
        let timerAgent = (timerAddress, {active=false; delay = 500} |> Mailbox.buildAgent timerAgentFn) |> MailAgent

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

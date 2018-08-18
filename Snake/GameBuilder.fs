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

type GameState =
    {
        eaters: struct(int*int) seq
        snake: SnakeState
        gameFrame: GameFrame
    }

type SnakeMailboxSystem =
    {
        commandAgent: Agent<CommandMessage, Command list>
        timerAgent: Agent<TimerCommand, TimerState>
        gameAgent: Agent<SnakeStateMessage, GameState>
        mailboxSystem: MailboxSystem
    }

[<RequireQualifiedAccess>]
module GameBuilder =

    let [<Literal>] commandAddress = "command"
    let [<Literal>] timerAddress = "timer"
    let [<Literal>] gameAddress = "game"

    let commandAgent (mailboxSystem: MailboxSystem) = mailboxSystem.Box<CommandMessage, Command list>(commandAddress)
    let timerAgent (mailboxSystem: MailboxSystem) = mailboxSystem.Box<TimerCommand, TimerState>(timerAddress)
    let gameAgent (mailboxSystem: MailboxSystem) = mailboxSystem.Box<SnakeStateMessage, GameState>(gameAddress)

    let gameAgentFn (mailboxSystem: MailboxSystem) updateUi gameState cmd =
        let timerAgent = timerAgent mailboxSystem
        let self = gameAgent mailboxSystem
        match gameState.gameFrame with
        | Frame field ->
            let snake =
                match cmd with
                | Tick -> Snake.tick gameState.snake
                | GrowUp -> Snake.growUp gameState.snake
                | Commands commands -> Snake.applyCommands gameState.snake commands
            let gameState = { gameState with snake = snake }
            let eaters = gameState.eaters |> Game.getNextEatersStep field snake
            let frame = eaters |> Game.buildNextGameFrame (fun () -> self.Post GrowUp) field gameState.snake
            timerAgent.Post Next
            updateUi frame
            {gameState with gameFrame = frame; eaters = eaters}
        | frame -> 
            timerAgent.Post Stop
            {gameState with gameFrame = frame}

    let commandAgentFn (mailboxSystem: MailboxSystem) commands msg =
        let gameAgent = gameAgent mailboxSystem
        match msg with
        | Cmd cmd -> cmd::commands
        | Flush ->
            commands
                |> Commands
                |> gameAgent.Post
            gameAgent.Post Tick
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
        | Stop -> printfn "Stop received"; { state with active = false }
        | SetDelay delay -> 
            Threading.Thread.Sleep(delay)
            if state.active then
                commandAgent.Post Flush
            {state with delay = delay}

    let buildSnakeGame (mailboxSystem: MailboxSystem) updateUi =
        let commandAgentFn = commandAgentFn mailboxSystem
        let timerAgentFn = timerAgentFn mailboxSystem
        let gameAgentFn = gameAgentFn mailboxSystem updateUi

        let commandAgent = (commandAddress, []|> Mailbox.buildAgent commandAgentFn) |> MailAgent 
        let timerAgent = (timerAddress, {active = false; delay = 1000} |> Mailbox.buildAgent timerAgentFn) |> MailAgent
        let zeroState =
            { 
                gameFrame = Field.getStartField() |> Frame; 
                snake = Snake.getDefaultSnakeState struct(4, 5)
                eaters = seq {yield struct(16,3); }
            }
        let gameAgent = (gameAddress, zeroState |> Mailbox.buildAgent gameAgentFn) |> MailAgent

        mailboxSystem.RespawnBox commandAgent
        mailboxSystem.RespawnBox timerAgent
        mailboxSystem.RespawnBox gameAgent
        {
            commandAgent = Box commandAgent
            timerAgent = Box timerAgent
            gameAgent = Box gameAgent
            mailboxSystem = mailboxSystem
        }

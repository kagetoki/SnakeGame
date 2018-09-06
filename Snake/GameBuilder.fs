namespace SnakeGame

open System
open PostOffice

type CommandMessage =
    | Cmd of Command
    | Flush

[<Struct>]
type TimerCommand =
    | Start
    | Next
    | PauseOrResume
    | Stop
    | SetDelay of int

[<Struct>]
type TimerState =
    {
        active: bool
        delay: int
    }

type SnakemailboxNetwork =
    {
        commandAgent: Agent<CommandMessage, Command list>
        timerAgent: Agent<TimerCommand, TimerState>
        gameAgent: Agent<Command list, GameState>
        mailboxNetwork: MailboxNetwork
    }

[<RequireQualifiedAccess>]
module GameBuilder =

    let [<Literal>] commandAddress = "command"
    let [<Literal>] timerAddress = "timer"
    let [<Literal>] gameAddress = "game"

    let commandAgent (mailboxNetwork: MailboxNetwork) = mailboxNetwork.Box<CommandMessage, Command list>(commandAddress)
    let timerAgent (mailboxNetwork: MailboxNetwork) = mailboxNetwork.Box<TimerCommand, TimerState>(timerAddress)
    let gameAgent (mailboxNetwork: MailboxNetwork) = mailboxNetwork.Box<Command list, GameState>(gameAddress)

    let gameAgentFn (mailboxNetwork: MailboxNetwork) updateUi gameState cmd =
        let timerAgent = timerAgent mailboxNetwork
        match gameState.gameFrame with
        | Frame field ->
            let gameState = Game.updateGameState gameState cmd
            timerAgent.Post Next
            updateUi gameState
            gameState
        | _ -> 
            timerAgent.Post Stop
            gameState

    let commandAgentFn (mailboxNetwork: MailboxNetwork) commands msg =
        let gameAgent = gameAgent mailboxNetwork
        match msg with
        | Cmd cmd -> cmd::commands
        | Flush ->
            commands |> gameAgent.Post
            []

    let timerAgentFn (mailboxNetwork: MailboxNetwork) (state: TimerState) cmd =
        let commandAgent = commandAgent mailboxNetwork
        match cmd with
        | Start -> commandAgent.Post Flush; {state with active = true}
        | Next -> 
            if state.active then
                Threading.Thread.Sleep(state.delay)
                commandAgent.Post Flush; 
            state
        | Stop -> printfn "Stop received"; { state with active = false }
        | PauseOrResume -> 
            if not state.active then
                commandAgent.Post Flush
            { state with active = not state.active }
        | SetDelay delay -> 
            Threading.Thread.Sleep(delay)
            if state.active then
                commandAgent.Post Flush
            {state with delay = delay}

    let buildSnakeGame (mailboxNetwork: MailboxNetwork) updateUi =
        let commandAgentFn = commandAgentFn mailboxNetwork
        let timerAgentFn = timerAgentFn mailboxNetwork
        let gameAgentFn = gameAgentFn mailboxNetwork updateUi

        let commandAgent = (commandAddress, []|> Mailbox.buildAgent commandAgentFn) |> MailAgent 
        let timerAgent = (timerAddress, {active=false; delay = 200} |> Mailbox.buildAgent timerAgentFn) |> MailAgent
        let eaters = [struct(20,15);struct(28,10)]
        let zeroState =
            { gameFrame = Field.getStartField [(Attack, 6us); (Speed, 2us)] eaters |> Frame;
              snake = Snake.getDefaultSnakeState struct(4, 5);
              eaters = eaters }
        let gameAgent = (gameAddress, zeroState |> Mailbox.buildAgent gameAgentFn) |> MailAgent

        mailboxNetwork.RespawnBox commandAgent
        mailboxNetwork.RespawnBox timerAgent
        mailboxNetwork.RespawnBox gameAgent
        {
            commandAgent = Box commandAgent
            timerAgent = Box timerAgent
            gameAgent = Box gameAgent
            mailboxNetwork = mailboxNetwork
        }

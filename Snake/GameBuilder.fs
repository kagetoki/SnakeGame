namespace SneakySnake

open System
open PostOffice
open System.Collections.Concurrent

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
        subscribers: ConcurrentBag<(GameState -> unit)>
        commandAgent: Agent<CommandMessage, Command list>
        timerAgent: Agent<TimerCommand, TimerState>
        gameAgent: Agent<Command list, GameState>
        mailboxNetwork: MailboxNetwork
    } with 
        member this.Kill() =
            this.commandAgent.Kill()
            this.timerAgent.Kill()
            this.gameAgent.Kill()
        member this.AddSubscriber = this.subscribers.Add


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
        | End (Win _) ->
            timerAgent.Post PauseOrResume
            Game.updateGameState gameState cmd
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

    let buildSnakeGame (mailboxNetwork: MailboxNetwork) startLevel =
        let subscribers = ConcurrentBag()
        let updateUi gameState =
            for sub in subscribers do
                sub gameState
        let commandAgentFn = commandAgentFn mailboxNetwork
        let timerAgentFn = timerAgentFn mailboxNetwork
        let gameAgentFn = gameAgentFn mailboxNetwork updateUi

        let commandAgent = { address = commandAddress; mailbox = []|> Mailbox.buildAgent commandAgentFn} 
        let timerAgent = {address = timerAddress; mailbox = {active=false; delay = 200} |> Mailbox.buildAgent timerAgentFn}
        let levels = LevelSource.loadAllLevels() |> List.ofArray |> List.skip startLevel
        let zeroState =
            { gameFrame = Frame levels.Head;
              snake = Snake.getDefaultSnakeState levels.Head.snakeStart;
              eaters = levels.Head.eaters |> List.ofSeq 
              startLevel = startLevel
              nextLevels = levels.Tail }
        let gameAgent = {address = gameAddress; mailbox = zeroState |> Mailbox.buildAgent gameAgentFn}
        mailboxNetwork.RespawnBox commandAgent
        mailboxNetwork.RespawnBox timerAgent
        mailboxNetwork.RespawnBox gameAgent
        {
            subscribers = subscribers
            commandAgent = Box commandAgent
            timerAgent = Box timerAgent
            gameAgent = Box gameAgent
            mailboxNetwork = mailboxNetwork
        }

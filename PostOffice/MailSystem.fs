namespace PostOffice

open System.Collections.Concurrent
open System

type Agent<'message,'state> =
    | Box of MailAgent<'message,'state>
    | DeadBox of string * MailAgent<string * obj, Map<string,obj list>>
    with member this.Post msg =
            match this with
            | Box box -> box.Post msg
            | DeadBox (address, deadbox) -> (address, box msg) |> deadbox.Post
         member this.Kill() =
            match this with
            | Box b -> b.Kill()
            | DeadBox _ -> ()
         interface IDisposable with
            member this.Dispose() =
                match this with
                | Box agent -> agent.Dispose()
                | DeadBox (_,agent) -> agent.Dispose()

type MailboxNetwork() as this =

    [<DefaultValue>]
    val mutable agentRegister: ConcurrentDictionary<string, obj>
    do this.agentRegister <- ConcurrentDictionary<string, obj>()

    let deadLettersFn deadLetters (address:string, msg:obj) =
        printfn "Deadletter: %s-%A" address msg
        match Map.tryFind address deadLetters with
        | None -> Map.add address [msg] deadLetters
        | Some letters -> 
            Map.remove address deadLetters
            |> Map.add address (msg::letters)

    let deadLettersAgent() = ("deadLetters", Map.empty |> Mailbox.buildAgent deadLettersFn) |> MailAgent

    member this.DeadLetters = deadLettersAgent()
    member this.Box<'message,'state>(address) =
        match this.agentRegister.TryGetValue address with
        | (true, agent) when (agent :? MailAgent<'message,'state>) ->
            let agent = agent :?> MailAgent<'message, 'state>
            Box agent
        | _ -> DeadBox (address, this.DeadLetters)

    member this.KillBox address =
        this.agentRegister.TryRemove(address) |> ignore
    
    member this.RespawnBox (agent: MailAgent<'a,'b>) =
        this.KillBox agent.Address
        this.agentRegister.TryAdd (agent.Address, agent) |> ignore

    interface IDisposable with
        member this.Dispose() =
                for agent in this.agentRegister.Values do
                    match agent with
                    | :? IDisposable as agent -> agent.Dispose()
                    | _ -> ()


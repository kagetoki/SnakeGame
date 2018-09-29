namespace PostOffice

open Microsoft.FSharp.Control
open System

type Mail<'msg, 'state> =
    | Post of 'msg
    | Get of AsyncReplyChannel<'state>
    | Kill

module Mailbox =

    let buildAgent applyMessage zeroState =
        MailboxProcessor.Start(fun inbox ->
        let rec loop state = async{
            let! msg = inbox.Receive()
            match msg with
            | Post msg ->
                let newState = applyMessage state msg
                return! loop newState
            | Get channel ->
                channel.Reply state
                return! loop state
            | Kill -> return ()
        }
        loop zeroState
        )

    let post (agent: MailboxProcessor<_>) msg = Post msg |> agent.Post

    let kill (agent: MailboxProcessor<_>) = agent.Post Kill

    let getState (agent: MailboxProcessor<_>) = agent.PostAndReply Get

    let getStateAsync (agent: MailboxProcessor<_>) = agent.PostAndAsyncReply Get

type MailAgent<'msg, 'state> = MailAgent of address:string * mailbox:MailboxProcessor<Mail<'msg, 'state>>
    with member this.Post msg =
            let (MailAgent (address,this)) = this
            Mailbox.post this msg
         member this.GetState() =
            let (MailAgent (address,this)) = this
            Mailbox.getState this
         member this.GetStateAsync() =
            let (MailAgent (address,this)) = this
            Mailbox.getStateAsync this
         member this.Kill() =
            let (MailAgent (address,this)) = this
            Mailbox.kill this
         member this.Address =
            let (MailAgent (address, _)) = this
            address
         member this.Dispose() =
            let (MailAgent (_, this)) = this
            (this:>IDisposable).Dispose()
         interface IDisposable with
          member this.Dispose() = this.Dispose()

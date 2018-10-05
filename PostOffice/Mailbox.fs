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

type MailAgent<'msg, 'state> =
    { address:string
      mailbox: MailboxProcessor<Mail<'msg, 'state>> }
       with member this.Post msg =
             Mailbox.post this.mailbox msg
            member this.GetState() =
                Mailbox.getState this.mailbox
             member this.GetStateAsync() =
                Mailbox.getStateAsync this.mailbox
             member this.Kill() =
                Mailbox.kill this.mailbox
             member this.Dispose() =
                (this.mailbox:>IDisposable).Dispose()
             interface IDisposable with
              member this.Dispose() = this.Dispose()


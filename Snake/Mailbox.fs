namespace SnakeGame

open Microsoft.FSharp.Control

type Mail<'msg, 'state> =
    | Post of 'msg
    | Get of AsyncReplyChannel<'state>

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
        }
        loop zeroState
        )

    let post (agent: MailboxProcessor<_>) msg = Post msg |> agent.Post

    let getState (agent: MailboxProcessor<_>) = agent.PostAndReply Get

    let getStateAsync (agent: MailboxProcessor<_>) = agent.PostAndAsyncReply Get

type Agent<'msg, 'state> = Agent of MailboxProcessor<Mail<'msg, 'state>>
    with member this.Post msg =
            let (Agent this) = this
            Mailbox.post this msg
         member this.GetState() =
            let (Agent this) = this
            Mailbox.getState this
         member this.GetStateAsync() =
            let (Agent this) = this
            Mailbox.getStateAsync this

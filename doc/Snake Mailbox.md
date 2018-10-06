# Змея в почтовом ящике и при чем тут F#

#### О чем это все?
Это все про змейку. Все прекрасно помнят, что такое змейка: на прямоугольном поле движется змея. Находит еду - вырастает в длине, находит себя или границу поля - умирает. А пользователь может только слать команды: влево, вправо, вверх, вниз.
Я решил добавить сюда немного экшна и заставить змею убегать от пакманов. И все это на акторах!

Поэтому сегодня я на примере змейки расскажу о том, как построить модель акторов с помощью `MailboxProcessor` из стандартной библиотеки, на какие моменты обратить внимание, и какие подводные камни вас могут ожидать.

 Код, написанный здесь, не идеален, может нарушать какие-то принципы и может быть написан лучше. Но если вы новичок и хотите разобраться с мейлбоксами -- надеюсь, эта статья вам поможет.
 Если вы про мейлбоксы все знаете и без меня -- вам тут может быть скучно.
#### Почему акторы?
Ради практики. Про модель акторов я читал, смотрел видео, мне все понравилось, но сам не пробовал. Теперь попробовал.
Несмотря на то, что по сути выбрал технологию ради технологии, концепция очень удачно легла на эту задачу.

#### Почему MailboxProcessor, а не, например, Akka.net?
Для моей задачи акка -- это из орбитальной станции по воробьям, `MailboxProcessor` гораздо проще, да и входит в стандартную библиотеку, так что никаких пакетов подключать не нужно.
<cut/>
#### О мейлбокс процессорах и сопутсвующем бойлерплейте
Суть проста. Мейлбокс внутри имеет message loop и некое состояние. Ваш message loop будет это состояние обновлять в соответствии с новым пришедшим сообщением. 

```fsharp
let actor = MailboxProcessor.Start(fun inbox -> 

    // функция, обрабатывающая входящее сообщение
    // и обновляющая состояние. inbox -- это как раз MailboxProcessor
    let rec messageLoop oldState = async {

        // читаем сообщение
        let! msg = inbox.Receive()

        // применяем нашу логику
        let newState = updateState oldState msg

        // и повторяем все по кругу
        return! messageLoop newState 
        }

    // запускаем всю эту красоту с начальным состоянием. В данном случае -- тапла из двух интов
    messageLoop (0,0)
    )
```
Обратите внимание, `messageLoop` рекурсивен, и в конце он должен быть вызван снова, иначе будет обработано только одно сообщение, после чего этот актор "умрет". Также `messageLoop` асинхронен, и каждая следующая итерация выполняется при получении нового сообщения: `let! msg = inbox.Receive()`. 
Таким образом вся логическая нагрузка уходит в функцию `updateState`, а это значит, что для создания мейлбокс процессора мы можем сделать функцию-конструктор, которая принимает функцию обновления состояния и нулевое состояние:
```fsharp
// принимаем функцию applyMessage и нулевое состояние в качестве аргументов
// и замыкаем их в лямбде (fun inbox -> ...)
let buildActor applyMessage zeroState =
    MailboxProcessor.Start(fun inbox ->
    let rec loop state = async{
        let! msg = inbox.Receive()
        let newState = applyMessage state msg
        return! loop newState
    }
    loop zeroState
    )

```
Круто! Теперь нам не нужно постоянно следить за тем, чтоб не забыть `return! loop newState`. Как известно, актор хранит состояние, однако сейчас совершенно не очевидно, как это состояние получить извне. У мейлбокс процессора есть метод `PostAndReply`, который принимает на вход функцию `AsyncReplyChannel<'Reply> -> 'Msg`. Сначала меня это ввело в ступор -- совершенно непонятно, откуда эту функцию взять. Но на деле все оказалось проще: все сообщения надо завернуть в DU-обертку, поскольку у нас теперь получается 2 операции над нашим актором: послать само сообщение и попросить текущее состояние. Вот как это выглядит:
```fsharp
// Сейчас этот тип является ссылочным.
// Mail<_,_> это абстрактный класс, а Post & Get -- его наследники.
// F# компилятор все это генерирует под капотом самостоятельно,  
// вместе с compare & equals бойлерплейтом. 
// Если же вы хотите сделать это значимым типом -- нет ничего проще. 
// Просто добавьте [<Struct>] аттрибут. Компилятор сделает за вас все остальное
type Mail<'msg, 'state> =
    | Post of 'msg
    | Get of AsyncReplyChannel<'state>
```
Наша функция-конструктор теперь выглядит вот так:
```fsharp
let buildActor applyMessage zeroState =
    MailboxProcessor.Start(fun inbox ->
    let rec loop state = async{
        let! msg = inbox.Receive()
        // здесь мы теперь проверяем, какого типа нам пришло
        // сообщение. Если это пост -- значит, нам нужно как раньше
        // обновить состояние согласно содержимому поста.
        // если же это гет -- нам нужно запихнуть текущее состояние
        // в канал для ответа. И не забыть запустить следующую итерацию!
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

```
Теперь для работы с мейлбоксом нам нужно все наши сообщения заворачивать в этот `Mail.Post`. Чтобы не писать это каждый раз, лучше это завернуть в небольшую апишку:
```fsharp
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

// Это называется Single Case Discriminated Union.
// MailboxProcessor имеет широкий API. Нам оттуда нужно далеко не все,
// а что-то из того, что нужно, предоставляется в не совсем удобном
// для нас виде. Поэтому мы прячем его за наш фасад, в то же время
// оставляя возможность добраться до кишок в случае необходимости.
type MailAgent<'msg, 'state> = MailAgent of address:string * mailbox:MailboxProcessor<Mail<'msg, 'state>>
    // Добавляем удобные нам методы API
    with member this.Post msg =
            // а вот как при необходимости можно достучаться до внутренностей нашего фасада
            let (MailAgent (address,this)) = this
            Mailbox.post this msg
         member this.GetState() =
            let (MailAgent (address,this)) = this
            Mailbox.getState this
         member this.GetStateAsync() =
            let (MailAgent (address,this)) = this
            Mailbox.getStateAsync this
         member this.Address =
            let (MailAgent (address, _)) = this
            address
         member this.Dispose() =
            let (MailAgent (_, this)) = this
            (this:>IDisposable).Dispose()
         interface IDisposable with
          member this.Dispose() = this.Dispose()
```
О том, что это за `address:string` я расскажу чуть позже, а пока наш бойлерплейт готов.
#### Собственно, змейка
В змейке есть змейка, юзер с его командами, поле и регулярный переход к следующему кадру. 
Вот все вот это вместе и нужно размазать по нашим акторам.
Изначальная раскладка у меня была такая:
- Актор с таймером. Принимает сообщения старт/стоп/пауза. Раз в n милисекунд отправляет сообщение `Flush` актору комманд. В качестве состояния хранит `System.Timers.Timer`
- Актор комманд. Принимает сообщения от юзера `Move Up/Down/Left/Right`, `AddPerk Speed/Attack` (да, моя змейка умеет быстро ползать и атаковать негодяев)  и `Flush` от таймера. В качестве состояния хранит список команд, а при флаше этот список обнуляет.
- Актор змейки. Хранит состояние змейки - перки, длину, направление, изгибы и координаты.
Принимает список сообщений от актора комманд, сообщение `Tick` (чтобы сдвинуть змейку на 1 ячейку вперед), и сообщение `GrowUp` от актора поля, когда она находит еду.
- Актор поля. Хранит карту ячеек, принимает состояние змейки в сообщении и натягивает координаты на существующую картину. А также отправляет `GrowUp` актору змейки и команду `Stop` таймеру, если игра окончена.

Как видите, даже при таком небольшом количестве сущностей карта сообщений уже получается нетривиальная. И уже на этом этапе возникли трудности: дело в том, что по умолчанию F# не позволяет циклические зависимости. В текущей строке кода вы можете использовать только код, написанный выше, и то же самое применимо к файлам в проекте. Это не баг, а фича, и я ее очень люблю, поскольку она помогает держать код в чистоте, но что делать, когда циклические ссылки нужны по дизайну? Конечно же, можно использовать `rec namespace` -- и тогда внутри одного файла можно будет ссылаться на все подряд, что есть в этом файле, чем я и воспользовался. 
Код ожидаемо испортился, но тогда это казалось единственным вариантом. И все заработало. 
#### Проблема внешнего мира
Все работало до тех пор, пока вся эта система акторов была изолирована от внешнего мира, и я только дебажил и выводил строчки в консоль. Когда пришло время внедрить зависимость в виде функции `updateUI`, которая на каждый тик должна была перерисовывать, я не смог решить эту задачу в текущей реализации. Ни уродливо, ни красиво -- никак. И тут я вспомнил про акку -- там ведь можно порождать акторов прямо по ходу, а у меня все мои акторы описаны на стадии компиляции. 
    Выход очевиден -- использовать акку! Нет конечно, акка все еще оверкилл, но я решил слизать оттуда определенные моменты -- а именно, сделать систему акторов, в которую можно динамически добавлять новые акторы и запрашивать по адресу существующие акторы.
Поскольку акторы теперь добавляются и удаляются в рантайме, а получаются по адресу, а не прямой ссылке, нужно предусмотреть сценарий, когда адрес смотрит в никуда, и актора там нет. По примеру той же акки, я добавил ящик для мертвых писем, а задизайнил я это через мои любимые DU:
```fsharp
// Agent<_,_> -- это то, что вы получите, запросив у системы акторов
// почтовый ящик с определенным адресом, типом состояния и типом входящих сообщений.
// Если по указанному адресу нашлось то, что вы искали -- вы получите кейс Box (mailagent),
// однако, вы так же можете указать неверный адрес, или тип сообщения, или тип состояния,
// и на этот случай вы получите Deadbox. Внутри него лежит такой же MailAgent, созданный заранее.
// Туда отправится переданное вами письмо в паре с указанным вами адресом.
// По большому счету -- это логи ошибок маршрутизации.
type Agent<'message,'state> =
    | Box of MailAgent<'message,'state>
    | DeadBox of string * MailAgent<string * obj, Map<string,obj list>>
    with member this.Post msg =
            match this with
            | Box box -> box.Post msg
            | DeadBox (address, deadbox) -> (address, box msg) |> deadbox.Post
         interface IDisposable with
            member this.Dispose() =
                match this with
                | Box agent -> agent.Dispose()
                | DeadBox (_,agent) -> agent.Dispose()
```
А сама система выглядит так:
```fsharp
// Это самый обычный класс. Со внутренним мутабельным состоянием -- иногда без этого никак.
type MailboxNetwork() as this =

    // объявляем словарь с нашими будующими агентами. Дешево и сердито!
    [<DefaultValue>]
    val mutable agentRegister: ConcurrentDictionary<string, obj>
    // этот код выполнится при инициализации объекта
    do this.agentRegister <- ConcurrentDictionary<string, obj>()

    // вот и наша шкатулка с логами,
    // в ней хранится Map -- это функциональная реализация словаря
    let deadLettersFn deadLetters (address:string, msg:obj) =
        printfn "Deadletter: %s-%A" address msg
        match Map.tryFind address deadLetters with // ищем адрес
        | None -> Map.add address [msg] deadLetters //  Не нашли -- добавляем структуру
        | Some letters -> //  нашли -- добавляем в массив новый элемент
            Map.remove address deadLetters
            |> Map.add address (msg::letters)

    let deadLettersAgent() = ("deadLetters", Map.empty |> Mailbox.buildAgent deadLettersFn) |> MailAgent

    member this.DeadLetters = deadLettersAgent()
    // метод-геттер для ящика с указанными адресом, типом сообщения и типом состояния
    member this.Box<'message,'state>(address) =
        match this.agentRegister.TryGetValue address with
        | (true, agent) when (agent :? MailAgent<'message,'state>) -> //если то, что лежит по адресу имеет нужный тип, вернем бокс
            let agent = agent :?> MailAgent<'message, 'state>
            Box agent
        | _ -> DeadBox (address, this.DeadLetters) // иначе -- привет мертвый ящик

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
```
Вот тут нам и пригодился тот самый `address:string`, о котором я писал выше. И снова все заработало, внешнюю зависимость теперь было легко прокинуть куда надо. Функции-конструкторы акторов теперь принимали аргументом систему акторов и доставали оттуда необходимых адресатов:
```fsharp
    // функция получения нашего гейм-актора (который хранит поле) из переданной актор-системы
    let gameAgent (mailboxNetwork: MailboxNetwork) = mailboxNetwork.Box<Command list, GameState>(gameAddress)

    // а это наш message loop для обработки юзер команд и передачи их дальше по цепи
    let commandAgentFn (mailboxNetwork: MailboxNetwork) commands msg =
        let gameAgent = gameAgent mailboxNetwork
        match msg with
        | Cmd cmd -> cmd::commands
        | Flush ->
            commands |> gameAgent.Post
            []

```
#### Медленно
По понятным причинам во время отладки я поставил низкую скорость игры: дилей между тиками был больше 500 милисекунд. Если же снизить дилей до 200, то сообщения начинали приходить с опозданием, и команды от юзера срабатывали с задержкой, что портило всю игру. Дополнительной ложкой дегтя был тот факт, что команду стоп в случае проигрыша таймер получал несколько раз. Для пользователя это никак не проявлялось, но тем не менее, был какой-то баг.
Неприятная правда заключалась в том, что акторы -- это, конечно, удобно здорово, но прямой вызов метода гораздо быстрее. Поэтому несмотря на то, что хранить саму змею в отдельном акторе было удобно с точки зрения организации кода, от этой идеи пришлось отказаться во имя быстродействия, поскольку на 1 такт игры обмен сообщениями был слишком интенсивный:
1. Пользователь отправляет произвольное количество команд напрямую в актор команд.
1. Таймер отправляет тик актору команд и в ранней реализации еще и актору змеи, чтобы тот передвинул змейку на следующую клетку
1. Актор команд отправляет список команд для змеи, когда соответствующее сообщение приходит от таймера.
1. Актор змеи, обновив свое состояние согласно 2 верхним сообщениям, отправляет состояние актору поля.
1. Актор поля все перерисовывает. Если змея нашла еду, то он отправляет актору змеи сообщение `GrowUp`, после чего тот отправляет новое состояние обратно актору поля.

И на все это есть 1 такт, которого не хватает, с учетом синхронизации в недрах `MailboxProcessor`. Более того, в текущей реализации таймер посылает следующее сообщение каждые n милисекунд независимо ни от чего, так что если мы 1 раз не влезли в такт, сообщения начинают скапливаться, и ситуация усугубляется. Гораздо лучше было бы "растянуть" этот конкретный такт, обработать все, что накопилось, и пойти дальше.

#### Финальная версия
Очевидно, что схему сообщений надо упрощать, при этом очень желательно оставить код максимально простым и доступным -- условно говоря, не хочется все пихать в 1 god actor, да и смысла в акторах тогда не очень много получается.
Поэтому взглянув на свой список акторов я понял, что первым лучше всего пожертвовать актором-змеей. Таймер нужен, буфер команд пользователя тоже нужен, чтобы в реальном времени их накапливать, но выливать только раз в такт, а змею держать в отдельном акторе объективной необходимости нет, это было сделано просто для удобства. К тому же, смержив его с актором поля можно будет без задержки обработать `GrowUp` сценарий. `Tick` сообщение для змеи тоже большого смысла не имеет, поскольку когда мы получаем сообщение от актора команд, это уже означает, что новый такт случился. Добавив к этому растягивание такта в случае задержки, имеем следующие изменения:
1. Убираем `Tick` & `GrowUp` сообщения.
1. Мержим актор змеи в актор поля -- он теперь будет хранить "тапл" этих состояний.
1. Убираем `System.Timers.Timer` из актора таймера. Вместо этого схема работы будет следующая: получив команду `Start`, он отправляет `Flush` актору команд. Тот отправляет список команд актору поля+змейки, последний актор все это обрабатывает и отправляет таймеру сообщение `Next`, тем самыс запрашивая у него новый тик. Таймер же, получив `Next` ждет `Thread.Sleep(delay)` и начинает весь круг сначала. Все просто.

Подведем итог.
- В предыдущей реализации 500 мс были минимально допустимым дилеем. В текущей дилей можно вообще убрать -- актор поля затребует новый такт, когда будет готов. Скапливание необработанных сообщений с предыдущих тактов теперь невозможно.
- Карта обмена сообщениями сильно упрощена -- вместо сложного графа имеем самый простой цикл.
- Это упрощение решило тот баг, когда таймер несколько раз получал `Stop` в случае проигрыша.
- Список сообщений уменьшился. Меньше кода -- меньше зла!

Выглядит это так:
```fsharp
    let [<Literal>] commandAddress = "command"
    let [<Literal>] timerAddress = "timer"
    let [<Literal>] gameAddress = "game"

    // функции-шорткаты для нужных нам акторов
    let commandAgent (mailboxNetwork: MailboxNetwork) = mailboxNetwork.Box<CommandMessage, Command list>(commandAddress)
    let timerAgent (mailboxNetwork: MailboxNetwork) = mailboxNetwork.Box<TimerCommand, TimerState>(timerAddress)
    let gameAgent (mailboxNetwork: MailboxNetwork) = mailboxNetwork.Box<Command list, GameState>(gameAddress)

    // message loop актора поля
    let gameAgentFn (mailboxNetwork: MailboxNetwork) updateUi gameState cmd =
        let timerAgent = timerAgent mailboxNetwork // выкупаем актор таймера
        match gameState.gameFrame with // проверяем текущее состояние игры 
        | Frame field ->
            // обновляем состояние в соответствии с командой
            let gameState = Game.updateGameState gameState cmd
            timerAgent.Post Next // запрашиваем следующий кадр
            updateUi gameState // не забываем про юай
            gameState // профит!
        | End (Win _) ->
            timerAgent.Post PauseOrResume
            Game.updateGameState gameState cmd // переход на следующий левел в случае победы
        | _ -> 
            timerAgent.Post Stop // следующие фреймы не нужны
            gameState

    // message loop актора комманд
    let commandAgentFn (mailboxNetwork: MailboxNetwork) commands msg =
        let gameAgent = gameAgent mailboxNetwork
        match msg with
        | Cmd cmd -> cmd::commands // команды от юзера собираем в пачку
        | Flush ->
            commands |> gameAgent.Post // и отправляем актору поля
            []

    // message loop актора таймера
    let timerAgentFn (mailboxNetwork: MailboxNetwork) (state: TimerState) cmd =
        let commandAgent = commandAgent mailboxNetwork
        match cmd with
        | Start -> commandAgent.Post Flush; {state with active = true}
        | Next -> 
            if state.active then // посылаем сообщение актору команд, только если таймер включен
                Threading.Thread.Sleep(state.delay)
                commandAgent.Post Flush; 
            state
        | Stop -> printfn "Stop received"; { state with active = false }
        | PauseOrResume -> 
            if not state.active then // если таймер сейчас включили -- флашим команды
                commandAgent.Post Flush
            { state with active = not state.active }
        | SetDelay delay -> 
            Threading.Thread.Sleep(delay)
            if state.active then
                commandAgent.Post Flush
            {state with delay = delay}
```
#### Ссылки
- [Введение в мейлбоксы](https://fsharpforfunandprofit.com/posts/concurrency-actor-model/)
- [Исходники мейлбокса для самых любознательных](https://github.com/fsharp/fsharp/blob/master/src/fsharp/FSharp.Core/control.fs#L2103)
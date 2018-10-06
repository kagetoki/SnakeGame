namespace SneakySnake

[<RequireQualifiedAccess>]
module Body =
    let defaultHead = Segment (Right, 2us, Tail)

    let length body =
        let rec lengthImpl body length =
            match body with
            | Tail -> length
            | Segment (_, l, next) -> l + length |> lengthImpl next
        lengthImpl body 0us

    let rec private tickTail body =
        match body with
        | Tail -> Tail
        | Segment(d, 0us, Tail) -> Tail
        | Segment(d, 1us, Tail) -> Tail
        | Segment(d, len, Tail) -> Segment(d, len - 1us, Tail)
        | Segment(d, len, next) -> Segment(d, len, tickTail next)

    let private tickHead direction body =
        match body with
        | Tail -> Tail
        | Segment(d, len, tail) when d = direction || d = direction.Opposite
            -> Segment(d, len + 1us, tail)
        | Segment(_) as part -> Segment(direction, 1us, part)

    let tick direction = tickHead direction >> tickTail

    let rec grow =
        function
        | Tail -> Tail
        | Segment(dir, len, Tail) -> Segment(dir, len + 1us, Tail)
        | Segment(dir, len, next) -> Segment(dir, len, grow next)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Snake =

    let defaultPerkTicks = 6us

    let getDefaultSnakeState struct(i,j) =
        { direction = Right;
          perks = Map.empty;
          length = 3us;
          headPoint = struct(i,j)
          body = Segment (Right, 2us, Tail)}

    let tick snake =
        let downTick t = if t > 0us then t - 1us else 0us
        let downTickPerks perk =
            match perk with
            | Cooldown ticks -> downTick ticks |> Cooldown
            | Active 0us -> Cooldown defaultPerkTicks
            | Active ticks -> downTick ticks |> Active

        let tickBody = Body.tick snake.direction
        let hasSpeed = snake.HasPerk Speed
        let body =
            if hasSpeed
            then snake.body |> (tickBody >> tickBody)
            else tickBody snake.body
        let nextCoordinate = Field.nextCoordinate snake.direction
        let headPoint =
            if hasSpeed
            then snake.headPoint |> (nextCoordinate >> nextCoordinate)
            else snake.headPoint |> nextCoordinate
        {snake with
            perks = Map.map (fun _ -> downTickPerks) snake.perks;
            body = body;
            headPoint = headPoint }

    let addPerk (snake: SnakeState) perk =
        if snake.CanApply perk
        then { snake with perks = snake.perks.AddOrReplace (perk, Active defaultPerkTicks)}
        else snake

    let removePerk (snake: SnakeState) perk =
        match snake.perks.TryFind perk with
        | None -> snake
        | Some perkState ->
            match perkState with
            | Cooldown _ -> snake
            | Active ticks -> { snake with perks = snake.perks.AddOrReplace (perk, Cooldown (defaultPerkTicks - ticks))}

    let applyCommand snake command =
        match command with
        | Move dir when dir = snake.direction.Opposite -> snake
        | Move dir -> {snake with direction = dir}
        | Perk (Add perk) -> addPerk snake perk
        | Perk (Remove perk) -> removePerk snake perk

    let mergeCommands commands =
        let mutable moveCommand = None
        let perks = System.Collections.Generic.HashSet()
        let getOpposite =
            function
            | Add perk -> Remove perk
            | Remove perk -> Add perk
        let merge cmd =
            match cmd with
            | Move _ as move ->
                moveCommand <- Some move

            | Perk perkCmd ->
                if not <| perks.Remove(getOpposite perkCmd)
                then perks.Add(perkCmd) |> ignore
        Seq.iter merge commands
        let perks = perks |> Seq.map Perk |> List.ofSeq
        match moveCommand with
        | None -> perks
        | Some m -> m::perks

    let applyCommands = List.fold applyCommand

    let growUp snake =
        { snake with
                length = snake.length + 1us;
                body = Body.grow snake.body;
        }

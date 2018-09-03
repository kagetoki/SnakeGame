namespace SnakeGame

[<Struct>]
type Direction =
    | Up | Right | Down | Left
    with member this.Opposite =
            match this with
            | Up -> Down
            | Down -> Up
            | Right -> Left
            | Left -> Right

[<Struct>]
type SnakePerk =
    | Speed
    | Attack

type PerkCommand =
    | Add of SnakePerk
    | Remove of SnakePerk

type Command =
    | Move of Direction
    | Perk of PerkCommand

type SnakeBody =
    | Segment of direction: Direction * length: uint16 * next: SnakeBody
    | Tail

type SnakeState =
    {
        direction: Direction
        perks: Map<SnakePerk,uint16>
        length: uint16
        headPoint: struct(int*int)
        body: SnakeBody
    } with member this.HasPerk perk = 
            match Map.tryFind perk this.perks with
            | None -> false
            | Some perkTicks -> perkTicks > 0us

[<AutoOpen>]
module CollectionUtils =
    type Map<'a,'b when 'a: comparison> with 
        member this.AddOrReplace (key, value) =
         match Map.tryFind key this with
         | None -> this.Add (key,value)
         | Some _ -> this |> Map.remove key |> Map.add key value

    let compareStructTuple (struct(x,y)) (struct(x1,y1)) =
        x = x1 && y = y1

    type MaybeBuilder() =
        member __.Bind (x,f) =
            match x with
            | None -> None
            | Some x -> f x
        member __.Return x = Some x
        member __.ReturnFrom x = x

    let maybe = MaybeBuilder()

    type OrElseBuilder() =
        member this.ReturnFrom(x) = x
        member this.Combine (a,b) = 
            match a with
            | Some _ -> a
            | None -> b
        member this.Delay(f) = f()

    let orElse = new OrElseBuilder()



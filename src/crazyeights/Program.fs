module CrazyEights
// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System

type Rank =
    | Ace
    | Two
    | Three
    | Four
    | Five
    | Six
    | Seven
    | Eight
    | Nine
    | Ten
    | Jack
    | Queen
    | King

type Suit = Club | Spade | Diamond | Heart

type Card =
    { Rank: Rank
      Suit: Suit }

let (^) rank suit = { Rank = rank; Suit = suit }

exception TooFewPlayers
exception AlreadyStarted
exception NotYetStarted

[<Struct>]
type Player = Player of int

module Player =
    let value (Player p) = p

type Effect =
    | Next
    | Skip
    | Back
    | Interrupt
    | BreakingInterrupt of Player

[<Struct>]
type Players = Players of int



let tryPlayers n =
    if n < 2 then
        Error TooFewPlayers
    else
        Ok (Players n)

let raiseOnError result =
    match result with
    | Ok v -> v
    | Error ex -> raise ex

let failOnError result =
    match result with
    | Ok v -> v
    | Error ex -> failwith ex

let players = tryPlayers >> raiseOnError


module Players =
    let value (Players n) = n

type Direction =
    | Clockwise
    | CounterClockwise


type Table =
    { Players : Players 
      Player: Player
      Direction: Direction }

module Direction =
    let flip dir =
        match dir with
        | Clockwise -> CounterClockwise
        | CounterClockwise -> Clockwise

module Table =
    let start players =
        { Players = players
          Player = Player 0
          Direction = Clockwise }
    
    let nextPlayer table =
        let n = Players.value table.Players
        let p  = Player.value table.Player
        let nextPlayer = 
            match table.Direction with
            | Clockwise -> (p+1) % n
            | CounterClockwise -> (p-1+n) % n
        { table with Player = Player nextPlayer }
    
    let skip = nextPlayer >> nextPlayer

    let flip table = { table with Direction = Direction.flip table.Direction }

    let back = flip >> nextPlayer

    let setPlayer p table =  {table with Player = p }

    let breakingInterrupt p table = 
        table
        |> setPlayer p
        |> nextPlayer

    let nextTable effect table =
        match effect with
        | Skip -> skip table
        | Back -> back table
        | Next -> nextPlayer table
        | Interrupt -> table
        | BreakingInterrupt player -> breakingInterrupt player table



type Command =
| StartGame of StartGame
| Play of Play
and StartGame =
    { Players : Players
      FirstCard: Card }
and Play =
    { Player: Player
      Card : Card }
      

type Event =
| GameStarted of GameStarted
| CardPlayed of CardPlayed
| WrongCardPlayed of CardPlayed
| WrongPlayerPlayed of CardPlayed
| InterruptMissed of CardPlayed
and GameStarted =
    { Players: Players
      FirstCard: Card
      Effect: Effect }
and CardPlayed =
    { Player: Player
      Card: Card
      Effect: Effect }

type Pile = 
    { TopCard: Card
      SecondCard: Card option }

module Pile =
    let start card =
        { TopCard = card
          SecondCard = None }

    let put card pile =
        { TopCard = card
          SecondCard = Some pile.TopCard }


type State =
    | NotStarted
    | Started of Started
and Started =
    { Pile: Pile
      Table: Table
    }

let initialState = NotStarted

let effect card =
    match card.Rank with
    | Seven -> Skip
    | Jack -> Back
    | _ -> Next

let evolve (state: State) (event: Event) : State =
    match state, event with
    | NotStarted, GameStarted e -> 
        Started { Pile = Pile.start e.FirstCard 
                  Table = Table.start e.Players |> Table.nextTable e.Effect }
    | Started s, CardPlayed e ->
        Started { s with 
                    Pile = Pile.put e.Card s.Pile
                    Table = s.Table |> Table.nextTable e.Effect  }
    | _ -> state

let decide cmd state =
    match state, cmd with
    | NotStarted, StartGame c ->
        [ GameStarted { Players = c.Players; FirstCard = c.FirstCard; Effect = effect c.FirstCard } ]
    | _, StartGame _ ->
        raise AlreadyStarted
    | NotStarted, _ -> raise NotYetStarted
    | Started s, Play c when c.Card = s.Pile.TopCard ->
        [ CardPlayed { Player = c.Player; Card = c.Card; Effect = Interrupt }]
    | Started s,  Play c when s.Table.Player <> c.Player ->
        if Some c.Card = s.Pile.SecondCard then
            [ InterruptMissed { Player = c.Player; Card = c.Card; Effect = effect c.Card } ]
        else
            [ WrongPlayerPlayed { Player = c.Player; Card = c.Card; Effect = effect c.Card }]
    | Started s,  Play c when c.Card.Rank <> s.Pile.TopCard.Rank && c.Card.Suit <> s.Pile.TopCard.Suit ->
        [ WrongCardPlayed { Player = c.Player; Card = c.Card; Effect = effect c.Card }]
    | Started s,  Play c ->
        [ CardPlayed { Player = c.Player; Card = c.Card; Effect = effect c.Card } ]


module Result =
    let map2 f rx ry =
        match rx, ry with
        | Ok x, Ok y -> Ok (f x y)
        | Error e, Ok _
        | Ok _ , Error e -> Error e
        | Error ex, Error ey -> Error (sprintf "%s, %s" ex ey)

module Serialization =
    let (|StartsWith|_|) (prefix: string) (s: string) = 
        if s.StartsWith(prefix) then Some() else None
    let (|EndsWith|_|) (suffix: string) (s: string) = 
        if s.EndsWith(suffix) then Some() else None
        
    let (|Int|_|) (s: string) =
        match Int32.TryParse s with
        | true, v -> Some v
        | false, _ -> None


    let inline readonly (mem: byte Memory) = Memory.op_Implicit mem
    module Players =
        let tryOfString s =
            match Int32.TryParse(s :string) with
            | true, v ->
                match tryPlayers v with
                | Ok p -> Ok p
                | Error _ -> Error "Invalid player count"
            | false, _ ->
                Error "Players should be an int"

    module Player =
        let tryOfString s =
            match Int32.TryParse(s :string) with
            | true, p ->
                Ok (Player p)
            | false, _ ->
                Error "Player should be an int"
    module Rank =
        let toString = function
            | Ace -> "1"
            | Two -> "2"
            | Three -> "3"
            | Four -> "4"
            | Five -> "5"
            | Six -> "6"
            | Seven -> "7"
            | Eight -> "8"
            | Nine -> "9"
            | Ten -> "10"
            | Jack -> "J"
            | Queen -> "Q"
            | King -> "K"
        let tryOfString = function
            | StartsWith "K" -> Ok King
            | StartsWith "Q" -> Ok Queen
            | StartsWith "J" -> Ok Jack
            | StartsWith "10" -> Ok Ten
            | StartsWith "9" -> Ok Nine
            | StartsWith "8" -> Ok Eight
            | StartsWith "7" -> Ok Seven
            | StartsWith "6" -> Ok Six
            | StartsWith "5" -> Ok Five
            | StartsWith "4" -> Ok Four
            | StartsWith "3" -> Ok Three
            | StartsWith "2" -> Ok Two
            | StartsWith "1"
            | StartsWith "A" -> Ok Ace
            | s -> Error (sprintf "Unknown rank in %s" s)
        
        let ofString = tryOfString >> failOnError

    module Suit =
        let toString = function
            | Club -> "C"
            | Spade -> "S"
            | Diamond -> "D"
            | Heart -> "H"

        let tryOfString = function
            | EndsWith "C" -> Ok Club
            | EndsWith "S" -> Ok Spade
            | EndsWith "D" -> Ok Diamond
            | EndsWith "H" -> Ok Heart
            | s -> Error (sprintf "Unknown suit in %s" s)

        let ofString = tryOfString >> failOnError
    
    module Card =
        let toString card =
            Rank.toString card.Rank + Suit.toString card.Suit

        let tryOfString input =
            match Rank.tryOfString input, Suit.tryOfString input with
            | Ok rank, Ok suit -> Ok { Rank = rank; Suit = suit }
            | Error e, Ok _
            | Ok _, Error e -> Error e
            | Error er, Error es -> Error (er + " " + es)
        
        let ofString = tryOfString >> failOnError

    module Effect =
        let toString = function
            | Next -> "next"
            | Skip -> "skip"
            | Back -> "back"
            | Interrupt -> "int"
            | BreakingInterrupt (Player p) -> sprintf "int%d" p

        let tryOfString = function
            | "next" -> Ok Next
            | "skip" -> Ok Skip
            | "back" -> Ok Back
            | StartsWith "int" as s ->
                match Int32.TryParse(s.Substring(3)) with
                | true, v -> Ok (BreakingInterrupt (Player v))
                | _ -> Ok Interrupt
            | s -> Error (sprintf "Unknown effect %s" s)

        let ofString = tryOfString >> failOnError


    type CardPlayedDto =
        { Player: int
          Card: string
          Effect: string }
    module CardPlayed =
        let toDto (e: CardPlayed): CardPlayedDto =
            { Player = Player.value e.Player
              Card = Card.toString e.Card
              Effect = Effect.toString e.Effect }

        let ofDto (dto: CardPlayedDto) : CardPlayed =
            { Player = Player dto.Player
              Card = Card.ofString dto.Card 
              Effect = Effect.ofString dto.Effect }

    type GameStartedDto =
        { Players: int
          FirstCard: string
          Effect: string }
    module GameStarted =
        let toDto (e: GameStarted): GameStartedDto =
            { Players = Players.value e.Players
              FirstCard = Card.toString e.FirstCard
              Effect = Effect.toString e.Effect }

        let ofDto (dto: GameStartedDto) : GameStarted =
            { Players = players dto.Players
              FirstCard = Card.ofString dto.FirstCard 
              Effect = Effect.ofString dto.Effect }


    module Event =
        open System.Text.Json

        let toBytes indent eventType converter e =
            let dto = converter e
            let options = JsonSerializerOptions(WriteIndented=indent)
            let bytes = JsonSerializer.SerializeToUtf8Bytes (dto, options)
            eventType, readonly (bytes.AsMemory())
        
        let ofBytes case (converter: 'b -> 'e) (bytes:byte ReadOnlyMemory) =
            try
                [JsonSerializer.Deserialize<'b>(bytes.Span) |> converter |> case]
            with
            | _ -> []

        let serialize indent (event:Event) : string * byte ReadOnlyMemory =
            match event with
            | GameStarted e -> toBytes indent "GameStarted" GameStarted.toDto e
            | CardPlayed e -> toBytes indent "CardPlayed" CardPlayed.toDto e
            | WrongCardPlayed e -> toBytes indent "WrongCardPlayed" CardPlayed.toDto e
            | WrongPlayerPlayed e -> toBytes indent "WrongPlayerPlayed" CardPlayed.toDto e
            | InterruptMissed e -> toBytes indent "InterruptMissed" CardPlayed.toDto e

        let deserialize (eventType, bytes) =
            match eventType with
            | "GameStarted" -> ofBytes GameStarted GameStarted.ofDto bytes
            | "CardPlayed" -> ofBytes CardPlayed CardPlayed.ofDto bytes
            | "WrongCardPlayed" -> ofBytes WrongCardPlayed CardPlayed.ofDto bytes
            | "WrongPlayerPlayed" -> ofBytes WrongPlayerPlayed CardPlayed.ofDto bytes
            | "InterruptMissed" -> ofBytes InterruptMissed CardPlayed.ofDto bytes
            | _ -> []

    let toString (t,b: byte ReadOnlyMemory) = 
        sprintf "%s %s" t (Text.Encoding.UTF8.GetString(b.Span))

    let rx = Text.RegularExpressions.Regex(@"^([^\s{]+)?\s*({.*$)")
    let ofString (s: string) = 
        let m = rx.Match(s)
        if m.Success then
            m.Groups.[1].Value, Memory.op_Implicit ((Text.Encoding.UTF8.GetBytes m.Groups.[2].Value).AsMemory())
        else
            "", ReadOnlyMemory.Empty

    type StateDto =
        { Started: StartedDto }
    
    and StartedDto =
        { TopCard: string
          SecondCard: string
          Players: int
          Player: int
          Direction: int }

    module State =
        open System.Text.Json
        let toDto = function
        | NotStarted -> { Started = Unchecked.defaultof<StartedDto> }
        | Started s -> { Started = {
                            TopCard = Card.toString s.Pile.TopCard
                            SecondCard = 
                                s.Pile.SecondCard
                                |> Option.map Card.toString
                                |> Option.toObj
                            Players = Players.value s.Table.Players
                            Player = Player.value s.Table.Player
                            Direction = if s.Table.Direction = Clockwise then 1 else 0 }}

        let ofDto (dto: StateDto) =
            if dto.Started = Unchecked.defaultof<StartedDto> then
                NotStarted
            else
                let s = dto.Started
                Started { 
                    Pile = { TopCard = Card.ofString s.TopCard 
                             SecondCard =
                                s.SecondCard
                                |> Option.ofObj
                                |> Option.map Card.ofString }
                    Table = { Players = players (int s.Players)
                              Player = Player (int s.Player)
                              Direction = if s.Direction = 1 then Clockwise else CounterClockwise }
                 }

        let serialize indent (position: int64) guard (state: State) =
            let dto = toDto state
            let options = JsonSerializerOptions(WriteIndented = indent)
            let bytes = JsonSerializer.SerializeToUtf8Bytes(dto, options)
            sprintf "%d-%s" position guard, readonly (bytes.AsMemory())

        let rx = Text.RegularExpressions.Regex(@"^(\d+?)-(.*)$")
        let deserialize guard (eventType: string,mem: byte ReadOnlyMemory) =
            let m = rx.Match(eventType)
            if m.Success then
                let storedGuard = m.Groups.[2].Value
                if storedGuard <> guard then
                    None
                else
                    let version = int64 m.Groups.[1].Value
                    let dto = JsonSerializer.Deserialize(mem.Span)
                    Some (ofDto dto, version)
            else
                None


    module Command =
        let tryParseArgs args =
            match args with
            | [|"start"; p; card|] ->
                Result.map2 
                    (fun p card -> StartGame { Players = p; FirstCard = card} )
                    (Players.tryOfString p)
                    (Card.tryOfString card)
            | [| "p"; p; card |] ->
                Result.map2
                    (fun p card -> Play { Player = p; Card = card})
                    (Player.tryOfString p)
                    (Card.tryOfString card)
            | _ ->
                Error "Unknown command"
        let tryParse (s:string) =
            s.Split([|' '|], StringSplitOptions.RemoveEmptyEntries) |> tryParseArgs


        let serialize command =
            match command with
            | StartGame c ->
                sprintf "start %d %s" (Players.value c.Players) (Card.toString c.FirstCard)
            | Play c ->
                sprintf "p %d %s" (Player.value c.Player) (Card.toString c.Card)

        let printUsage() =
            printfn "usage:"
            printfn "start <players> <firstCard>"
            printfn "    players     number of players"
            printfn "    firstCard   the first card"
            printfn "p <player> <card>"
            printfn "    player      player identifier"
            printfn "    card        the card to play"
            printfn ""
            printfn "card format: <rank><suit>"
            printfn "    rank        1 .. 10, J, Q,K"
            printfn "    suit        C,S,H,D"
            printfn ""
            printfn "Use Ctrl+C to quit"

module Printer =
    open Serialization

    let sprintSuit = function
        | Spade -> "♠"
        | Club -> "♣"
        | Heart -> "♥"
        | Diamond -> "♦"

    let sprintCard card = 
        Rank.toString card.Rank + sprintSuit card.Suit

    let sprintEffectVerb = function
        | Interrupt
        | BreakingInterrupt _ -> "interrupted with"
        | _ -> "played"

    let sprintEffect = function
        | Next -> "."
        | Skip -> ". Skip next player!"
        | Back -> ". Kickback!"
        | Interrupt -> ". Game continues as if nothing happened."
        | BreakingInterrupt p -> sprintf ". Game now resumes after player %d" (Player.value p)
        
    let sprintEvent =
        function
        | GameStarted e -> sprintf "%d Players game started with a %s" (Players.value e.Players) (sprintCard e.FirstCard)
        | CardPlayed e -> 
            sprintf "Player %d %s a %s%s" 
                (Player.value e.Player) 
                (sprintEffectVerb e.Effect)
                (sprintCard e.Card)
                (sprintEffect e.Effect)
        | WrongCardPlayed e -> sprintf "Player %d tried to play a %s, but it was neither same rank nor same suit. Penalty!" (Player.value e.Player) (sprintCard e.Card)
        | WrongPlayerPlayed e -> sprintf "Player %d tried to play a %s, but it was not their turn. Penalty!" (Player.value e.Player) (sprintCard e.Card)
        | InterruptMissed e -> sprintf "Player %d tried to play a %s, but it was not their turn" (Player.value e.Player) (sprintCard e.Card)

    let printEvents events =
        for e in events do
            printfn "%s" (sprintEvent e)

open EventStore.Client

module EventStore =
    open System.Linq
    let create() =
        let settings = EventStoreClientSettings.Create("esdb://localhost:2113?Tls=false");
        new EventStoreClient(settings)

    let foldStream (store: EventStoreClient) deserialize evolve stream position seed =
        store.FoldStreamAsync(
                    (fun e -> deserialize (e.Event.EventType, e.Event.Data) :> _ seq),
                    (fun state e -> evolve state e),
                    stream,
                    position,
                    seed
                     ).Result


    let readStream (store: EventStoreClient) deserialize stream position =
        let result = store.ReadStreamAsync(Direction.Forwards, stream, position)
        if result.ReadState.Result = ReadState.StreamNotFound then
            []
        else
            [ for e in result.ToListAsync().Result do
                deserialize (e.Event.EventType, e.Event.Data) ]

    let readAll (store: EventStoreClient) deserialize position =
        let result = store.ReadAllAsync(Direction.Forwards, position)
        let mutable lastPos = position
        let events = 
            [for ed in result.ToListAsync().Result do
                lastPos <- ed.Event.Position
                for e in deserialize(ed.Event.EventStreamId, ed.Event.EventType, ed.Event.Data) do
                    ed.Event.Position, e ]
        lastPos, events

        

    let tryReadLast (store: EventStoreClient) deserialize stream =
        let result = store.ReadStreamAsync(Direction.Backwards, stream, StreamPosition.End, maxCount= 1L)
        if result.ReadState.Result = ReadState.StreamNotFound then
            None
        else
            result.ToListAsync().Result
            |> Seq.choose (fun e -> deserialize (e.Event.EventType, e.Event.Data))
            |> Seq.tryHead

    let tryAppendToStream (store: EventStoreClient) serialize stream (expectedVersion: StreamRevision) events =
        store.ConditionalAppendToStreamAsync(
                stream,
                expectedVersion,
                    seq { 
                        for e in events do
                            let t,d = serialize e
                            EventData(Uuid.NewUuid(), t, d, Nullable() )
                    }).Result

    let appendToStream (store: EventStoreClient) serialize stream (expectedVersion: StreamRevision) events =
        store.ConditionalAppendToStreamAsync(
                stream,
                expectedVersion,
                    seq { 
                        for e in events do
                            let t,d = serialize e
                            EventData(Uuid.NewUuid(), t, d, Nullable() )
                    }).Result
    let forceAppendToStream (store: EventStoreClient) serialize stream events =
        store.AppendToStreamAsync(
                stream,
                StreamState.Any,
                    seq { 
                        for e in events do
                            let t,d = serialize e
                            EventData(Uuid.NewUuid(), t, d, Nullable() )
                    }).Result |> ignore



let readState filename =
    if IO.File.Exists filename then
        let json = IO.File.ReadAllText filename
        let dto = Text.Json.JsonSerializer.Deserialize<Serialization.StateDto>(json)
        Some (Serialization.State.ofDto dto)
    else
        None

let readEvents filename =
    if IO.File.Exists filename then
        let lines = IO.File.ReadAllLines filename
        [ for l in lines do
            yield! l
                    |> Serialization.ofString
                    |> Serialization.Event.deserialize ]
    else
        []


let writeState filename state =
    let dto = Serialization.State.toDto state
    let json = Text.Json.JsonSerializer.Serialize(dto)
    IO.File.WriteAllText(filename, json)

let appendEvents filename events =
    let lines =
        [ for e in events do
            e
            |> Serialization.Event.serialize false
            |> Serialization.toString ]
    IO.File.AppendAllLines(filename, lines)

let getState filename =
    readState filename
    |> Option.defaultWith (fun _ -> 
        readEvents (filename + "-stream")
        |> List.fold evolve initialState)

let handler filename =
    let mutable state = getState filename

    fun cmd ->
        try
            let events = decide cmd state
            Printer.printEvents events
            state <- List.fold evolve state events
            writeState filename state
            appendEvents (filename + "-stream") events
        with
        | ex ->  printfn "%O" ex

module ES =
    let noStreamId serializer =
        fun (_,t,d) -> serializer(t,d)
    let loadState store stream v state =
        let r =
            EventStore.foldStream 
                store 
                Serialization.Event.deserialize
                evolve
                stream
                v
                state
        r.Revision, r.Value
    
    let appendEvents store stream expectedVersion events =
        EventStore.appendToStream
            store
            (Serialization.Event.serialize true)
            stream
            expectedVersion
            events

    let getState store stream =
        let s,v =
            EventStore.tryReadLast 
                    store
                    (Serialization.State.deserialize "v2")
                    (stream + "-snap")
                |> Option.defaultValue (initialState, 0L)
        loadState store stream (StreamPosition.FromInt64 v) s

let handlerES store stream =
    let v0, state0 = ES.getState store stream

    let mutable state = state0
    let mutable v = v0

    fun cmd -> 
        try
            let events = decide cmd state
            Printer.printEvents events
            let r = ES.appendEvents store stream v events
            if r.Status = ConditionalWriteStatus.Succeeded then
                state <- List.fold evolve state events
                v <- r.NextExpectedStreamRevision


                EventStore.forceAppendToStream
                    store
                    (Serialization.State.serialize true (v.Next().ToInt64()) "v2")
                    ( stream + "-snap")
                    [ state ]
                
                events
            else

                let r = 
                    EventStore.foldStream
                     store
                     Serialization.Event.deserialize
                     evolve
                     stream
                     (v.Next() |> StreamPosition.FromStreamRevision)
                     state

                state <- r.Value
                v <- r.Revision

                failwith "Problement de concurence"
        with
        | ex -> printfn "(%O" ex
                []
type GameId = GameId of int
module GameId =
    let value (GameId id) = id

    let tryParseFromStream (stream: string) =
        if stream.StartsWith("game-") then
            let index = stream.LastIndexOf('-') + 1
            match Int32.TryParse(stream.Substring(index)) with
            | true, v -> Some (GameId v)
            | false, _ -> None
        else
            None

    let parseFromStream (stream: string) =
        let index = stream.LastIndexOf('-')
        GameId (int (stream.Substring(index)))


module CardCount =
    type DBOp =
        | Increment of GameId

    let guard = "v2"

    let evolve (game,event) =
        match event with
        | CardPlayed _ -> [ Increment game ]
        | _ -> []

    let evolveDb cnx version (game, event) =
        for op in evolve (game,event) do
            match op with
            | Increment game -> Db.CardCount.increment cnx (GameId.value game, guard, version)

    let initialState = Map.empty
    
    let increment key state =
        let newCount = 
            match Map.tryFind key state with
            | Some count -> count+1
            | None -> 1
        Map.add key newCount state 

    let evolve' state (game, event) = 
        evolve (game,event)
        |> List.fold (fun s op ->
            match op with
            | Increment game -> increment game s
        ) state


    let load cnx =
        let loadedGuard, version, counts = Db.CardCount.load cnx
        if loadedGuard <> guard then
            Position.Start, initialState
        else
            let gameCounts =
                [ for g,c in counts do 
                    GameId g, c]
                |> Map.ofList
            version, gameCounts 

    let save cnx position state =
        Db.CardCount.deleteAll cnx
        for (game,count) in Map.toSeq state do
            Db.CardCount.insert cnx (GameId.value game,count)
        Db.CardCount.insertPosition cnx (guard,position)

    let loadPosition cnx =
        let loadedGuard, pos = Db.CardCount.loadPosition cnx
        if loadedGuard <> guard then
            Position.Start
        else
            pos

    let start cnx =
        let mutable position = Db.Sql.run cnx loadPosition

        let update (pos, event) =
            if position.CompareTo(pos) < 0 then
                Db.Sql.run cnx (fun c ->
                    evolveDb c pos event 
                )
            position <- pos

        let batchUpdate events =
            let p, state = Db.Sql.run cnx load
            let mutable pos = p
            let mutable state = state
            let mutable changed = false
            for p,e  in events do
                if pos.CompareTo(p) < 0 then
                    state <- evolve' state e
                    pos <- p
                    changed <- true
            if changed then
                Db.Sql.transaction cnx (fun cnx ->
                    Db.CardCount.deleteAll cnx
                    for g,c in Map.toSeq state do
                        Db.CardCount.insert cnx (GameId.value g,c)
                    Db.CardCount.insertPosition cnx (guard, pos)
                )
                position <- pos

        position, update, batchUpdate


module TurnsSinceLastError =
    type DbOp =
    | Increment of GameId * Player
    | Reset of GameId * Player

    let guard = "v2"
    let evolve (game, event) =
        match event with
        | CardPlayed e -> [Increment(game,e.Player)]
        | WrongCardPlayed { Player = player}
        | WrongPlayerPlayed { Player = player}
             -> [Reset(game,player)]
        | _ -> []

    let evolveDb cnx version (game, event) =
        for op in evolve (game, event) do
            match op with
            | Increment(game,player)->
                Db.TurnsSinceLastError.increment cnx (GameId.value game, Player.value player, guard, version)
            | Reset(game,player) ->
                Db.TurnsSinceLastError.reset cnx (GameId.value game, Player.value player, guard, version)

    let initialState = Map.empty

    let increment key state =
        match Map.tryFind key state with
        | Some count -> Map.add key (count+1) state
        | None -> Map.add key 1 state

    let reset key state =
        Map.add key 0 state


    let evolve' state (game, event) =
        evolve (game,event)
        |> List.fold (fun s op ->
            match op with
            | Increment(g,p) -> increment (g,p) s
            | Reset(g,p) -> reset (g,p) s
        ) state

    let loadPosition cnx =
        let loadedGuard, position = Db.TurnsSinceLastError.loadPosition cnx
        if loadedGuard <> guard then
            Position.Start
        else
            position

    let load cnx =
        let loadedGuard, version, state = Db.TurnsSinceLastError.load cnx
        if loadedGuard <> guard then
            Position.Start, initialState
        else
            let gameCounts =
                [ for g,p,c in state do 
                    (GameId g,Player p), c]
                |> Map.ofList
            version, gameCounts 

    let save cnx version state =
        Db.TurnsSinceLastError.deleteAll cnx
        for ((game,player), count) in Map.toSeq state do
            Db.TurnsSinceLastError.insert cnx (GameId.value game, Player.value player, count)
        Db.TurnsSinceLastError.insertPosition cnx (guard, version)

    let start cnx =
        let mutable position = Db.Sql.run cnx loadPosition
                
        let update (pos, event) =
            if position.CompareTo(pos) < 0 then
                Db.Sql.run cnx (fun c->
                    evolveDb c pos event )

        let batchUpdate events =
            let p, state = Db.Sql.run cnx load
            let mutable pos = p
            let mutable state = state
            let mutable changed = false
            for p,e  in events do
                if pos.CompareTo(p) < 0 then
                    state <- evolve' state e
                    pos <- p
                    changed <- true
            if changed then
                Db.Sql.transaction cnx (fun cnx ->
                    Db.TurnsSinceLastError.deleteAll cnx
                    for (g,p),c in Map.toSeq state do
                        Db.TurnsSinceLastError.insert cnx (GameId.value g, Player.value p, c)
                    Db.TurnsSinceLastError.insertPosition cnx (guard, pos)
                )
                position <- pos        
        position, update, batchUpdate

        


// type MailBoxMsg =
//     | Update of (unit -> unit)
//     | Query of ((State * StreamPosition) -> unit)

// let proj =
//     MailboxProcessor.Start (fun mailbox ->
//         let rec loop state version =
//             async {
//                 let! msg = mailbox.Receive()
//                 match msg with
//                 | Update reply ->
//                       let r =
//                         EventStore.foldStream
//                             store
//                             Serialization.Event.deserialize
//                             evolve
//                             stream
//                             version
//                             state
//                       reply()
                      
//                       saveSnapshot()
//                       return! loop r.Value r.Version
//                 | Query reply ->
//                     let r =
//                         EventStore.foldStream
//                             store
//                             Serialization.Event.deserialize
//                             evolve
//                             stream
//                             version
//                             state

//                     reply (r.State, r.Version)
//                     return! loop r.State r.Version
//             }
//         let s,v = loadFromSnapshot
//         let state, version =
//             EventStore.foldStream
//                 ...
//         loop s v
//     )

// proj.Post Update

// proj.PostAndAsyncReply(fun c -> Query c.Reply)

// Hands
// PlayerId , Card

// INSERT (PlayerId, Card) VALUE (@player, @card) IN HANDS
// DELETE FROM Hands WHERE PlayerId = @player AND Card = @card



    
    
    // let gameCounterDb db events =
    //     let count = db.Query "SELECT Count FROM GameCount"
    //     let newCount = List.fold evolve count events
    //     db.Exec "UPDATE Count = @count FROM GameCount" {| count = newCount |}


module PendingNotifications =
    type GameId=  GameId of int
    type State = GameId Set

    let evolve state event =
        match event with
        | (gameId, GameStarted _ ) -> Set.add gameId state
        | (gameId, NotificationSent) -> Set.remove gameId state
        | _ -> state

let minPos (x: Position) (y: Position) =
    if x.CompareTo(y) <= 0 then x else y

[<EntryPoint>]
let main argv =
    //Serialization.Command.printUsage()

    let game = GameId 1
    let stream = sprintf "game-%d" (GameId.value game) 
    use store = EventStore.create()
    let handle = handlerES store stream
    
    let cnxString = "server=localhost;database=crazyeights;user id=sa;password=Hackyourj0b"
    let cardCountPos, cardCountProj, cardCountBatch = CardCount.start cnxString
    let turnPos , turnProj, turnBatch = TurnsSinceLastError.start cnxString

    let mutable allPos = List.reduce minPos [turnPos; cardCountPos]

    let catchUp() =

        let lastPos, events =
            EventStore.readAll 
                store
                (fun (stream, t,d) ->
                    match GameId.tryParseFromStream stream with
                    | Some game ->
                        [ for e in Serialization.Event.deserialize (t,d) do
                            game, e ]
                    | None -> [])
                allPos
        for e in events do
            cardCountProj e
            turnProj e
        allPos <- lastPos

    let catchUpBatch() =
        let lastPos, events =
            EventStore.readAll 
                store
                (fun (stream, t,d) ->
                    match GameId.tryParseFromStream stream with
                    | Some game ->
                        [ for e in Serialization.Event.deserialize (t,d) do
                            game, e ]
                    | None -> [])
                allPos
        cardCountBatch events
        turnBatch events

    if allPos = Position.Start then
        catchUpBatch()
    else

        catchUp()


    while true do
        printf "> "
        match Serialization.Command.tryParse (Console.ReadLine()) with
        | Ok cmd ->
            let events = handle cmd
            catchUp()

        | Error e -> 
            eprintfn "%s" e

    // let b = 
    //     GameStarted { Players = players 4; FirstCard = Six ^ Club; Effect = Next}
    //     |> Serialization.Event.serialize false
    //     |> Serialization.toString
    // let c = 
    //     CardPlayed { Player = Player 2; Card = Six ^ Club; Effect = Next}
    //     |> Serialization.Event.serialize false
    //     |> Serialization.toString
    // let e =
    //     """GameStarted {"Players":4,"FirstCard":"6C","Effect":"next"}"""
    //     |> Serialization.ofString
    //     |> Serialization.Event.deserialize

    // let e2 =
    //     """CardPlayed {"Player":2,"Card":"6C","Effect":"next"}"""
    //     |> Serialization.ofString
    //     |> Serialization.Event.deserialize

    // let c1 = Serialization.Command.tryParse "start 4 3C"
    // let c2 = Serialization.Command.tryParse "p 1 1C"

    // let c3 = Serialization.Command.serialize (StartGame { Players = players 4; FirstCard = Three ^ Club})
    // let c4 = Serialization.Command.serialize (Play { Player = Player 1; Card = Three ^ Club})

    0 // return an integer exit code
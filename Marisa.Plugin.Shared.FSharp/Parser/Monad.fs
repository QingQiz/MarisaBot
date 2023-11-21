namespace Marisa.Plugin.Shared.FSharp.Parser
#nowarn "40"


module Monad =
    type Parser<'T> = Parser of (string -> int -> 'T option * int)

    let parse (Parser p) input pos = p input pos

    let failAt p = None, p
    let succeedWith res p = Some res, p

    // Monad 的实现
    // 最小实现
    let (>>=) (Parser p1: Parser<'a>) (p2: 'a -> Parser<'b>) : Parser<'b> =
        Parser
        <| fun inp pos ->
            match p1 inp pos with
            | None, _ -> failAt pos
            | Some res, nextPos -> parse (p2 res) inp nextPos

    // 扩展实现
    let (>>) (p1: Parser<'a>) (p2: Parser<'b>) : Parser<'b> = p1 >>= fun _ -> p2

    let ret a = Parser <| fun _ -> succeedWith a

    // Applicative 的实现
    // 最小实现
    let lift pFab pa =
        pFab >>= fun f -> pa >>= fun a -> ret <| f a

    // 扩展实现
    let (<*>) = lift

    let fMap fab pa = ret fab <*> pa
    let (<&>) = fMap

    let ( *> ) p1 p2 = p1 >>= fun _ -> p2
    let (<*) pa pb = (fun a _ -> a) <&> pa <*> pb

    // Alternative
    // 最小实现
    let (<|>) (Parser pa) (Parser pb) =
        Parser
        <| fun inp pos ->
            match pa inp pos with
            | None, _ -> pb inp pos
            | Some res, nextPos -> succeedWith res nextPos

    /// one or more
    let some pa =
        let rec pElse =
            pa
            >>= fun a -> (pElse <|> ret []) >>= fun l -> ret <| a :: l

        pElse

    // 扩展实现
    /// zero or more
    let many pa = some pa <|> ret []

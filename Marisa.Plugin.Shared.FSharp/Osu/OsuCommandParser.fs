namespace Marisa.Plugin.Shared.FSharp.Osu


module OsuCommandParser =
    open Marisa.Plugin.Shared.FSharp.Parser
    open Monad
    open Parser

    type OsuMode = int
    type OsuId = string

    type OsuCommand =
        { Name: OsuId
          BpRank: (int * int option) option
          Mode: OsuMode option }

        override this.ToString() =
            match this with
            | { BpRank = None; Mode = None } -> this.Name
            | { BpRank = None } -> $"{this.Name}:{this.Mode.Value}"
            | { Mode = None } -> $"{this.Name}#{this.BpRank.Value}"
            | _ -> $"{this.Name}#{this.BpRank.Value}:{this.Mode.Value}"

    let osuCommandParser =
        let nameCharset =
            List.concat [ [ 'a' .. 'z' ]; [ 'A' .. 'Z' ]; [ '0' .. '9' ]; [ '-'; '_'; '['; ']'; ' ' ] ]

        let name = stringOfCharset nameCharset >>= fun (s: string) -> (ret <| s.Trim(' '))

        let rankInRange = fun a -> a > 0 && a <= 200

        let rank =
            (many space
             *> ignorableChar '#'
             *> ((number >>= fun a -> char '-' >> number >>= (fun b -> ret (a, Some b)))
                 <|> (number >>= fun a -> ret (a, None)))
             >>= (fun (a, b) ->
                 if not <| rankInRange a then
                     ret None
                 else
                     match b with
                     | Some b when (not <| rankInRange b) || a >= b -> ret None
                     | Some b -> ret <| Some(a, Some b)
                     | None -> ret <| Some(a, None)))
            <|> ret None

        let modeIdx =
            let idx =
                number
                >>= fun num ->
                    match num with
                    | x when x >= 0 && x <= 3 -> ret <| Some x
                    | _ -> ret None

            let modeName =
                (string "osu" *> (Some 0 |> ret))
                <|> (string "taiko" *> (Some 1 |> ret))
                <|> (string "fruit" *> (Some 2 |> ret))
                <|> (string "mania" *> (Some 3 |> ret))

            idx <|> modeName

        let mode: Parser<OsuMode option> =
            (many space *> (char ':' <|> char '：') *> many space *> modeIdx) <|> ret None

        let constructor a b c = { Name = a; BpRank = b; Mode = c }

        ((fun r -> constructor "" r None) <&> (rank <* many space <* eos)) // 只有一个1-200的数字，认为是 rank
        <|> (constructor <&> name <*> rank <*> mode) // 名字#rank:mode
        <|> (constructor "" <&> rank <*> mode) // #rank:mode
        <|> (many space *> eos *> (constructor "" None None |> ret)) // 什么都没有

    let parser s = parse osuCommandParser s 0 |> fst

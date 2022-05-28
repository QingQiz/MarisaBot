namespace Marisa.Plugin.Shared.FSharp.Osu


module OsuCommandParser =
    open Marisa.Plugin.Shared.FSharp.Parser
    open Monad
    open Parser

    type OsuMode = int
    type OsuId = string

    type OsuCommand =
        {
            Name: OsuId
            BpRank: int option
            Mode: OsuMode option
        }
        override this.ToString() =
            match this with
            | { BpRank = None; Mode = None } -> this.Name
            | { BpRank = None } -> $"{this.Name}:{this.Mode.Value}"
            | { Mode = None } -> $"{this.Name}#{this.BpRank.Value}"
            | _ -> $"{this.Name}#{this.BpRank.Value}:{this.Mode.Value}"

    let osuCommandParser =
        let nameCharset = List.concat [ [ 'a' .. 'z' ]; [ 'A' .. 'Z' ]; [ '0' .. '9' ]; [ '-'; '_'; '['; ']'; ' ' ] ]
        let name = stringOfCharset nameCharset >>= fun (s: string) -> ret <| s.Trim(' ')

        let rank =
            (many space *> ignorableChar '#' *> many space *> number
             >>= fun n ->
                     if n > 100 || n <= 0 then
                         ret None
                     else
                         ret <| Some n)
            <|> ret None

        let mode: Parser<OsuMode option> =
            (many space *> (char ':' <|> char '：') *> many space *> number
             >>= fun num ->
                     match num with
                     | x when x >= 0 && x <= 3 -> ret <| Some x
                     | _ -> ret None)
            <|> ret None

        let constructor a b c = { Name = a; BpRank = b; Mode = c }

        ((fun r -> constructor "" r None) <&> (rank <* many space <* eos)) // 只有一个1-100的数字，认为是 rank
        <|> (constructor <&> name <*> rank <*> mode )                      // 名字#rank:mode
        <|> (constructor "" <&> rank <*> mode )                            // #rank:mode
        <|> (many space *> eos *> (constructor "" None None |> ret))       // 什么都没有

    let parser s = parse osuCommandParser s 0 |> fst

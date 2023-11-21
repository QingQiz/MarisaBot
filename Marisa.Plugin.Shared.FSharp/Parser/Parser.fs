namespace Marisa.Plugin.Shared.FSharp.Parser

open System
open System.Collections.Generic


module Parser =
    open Monad
    //最基础的
    let satisfy func : Parser<char> = Parser <| fun s p ->
        if p < s.Length && func s[p] then
            succeedWith s[p] (p + 1)
        else
            failAt p
    
    let peek = Parser <| fun s p ->
        if p < s. Length then
            succeedWith s[p] p
        else
            failAt p

    let char c = satisfy <| (=) c
    let anyChar = satisfy <| fun _ -> true
    let digit = satisfy Char.IsDigit
    let letter = satisfy Char.IsLetter
    let space = satisfy Char.IsWhiteSpace
    let ignorableChar c = char c <|> peek

    let eos = Parser <| fun inp pos ->
        if pos = inp.Length then
            succeedWith "" pos
        else
            failAt pos

    let string (s: string) =
        Parser
        <| fun inp pos ->
            if pos + s.Length > inp.Length then
                failAt pos
            else if inp.Substring(pos, s.Length) = s then
                succeedWith s <| pos + s.Length
            else
                failAt pos

    let stringOfCharset (cs: IEnumerable<char>) =
        let hs = HashSet<char>(cs)

        (many <| satisfy hs.Contains) >>= fun x -> ret (String (Array.ofList x))

    // 数字
    let number =
        let rec toInt l res =
            match l with
            | [] -> res
            | d :: rest -> toInt rest <| res * 10 + (int d - int '0')

        let toInt_v l = toInt l 0

        toInt_v <&> some digit

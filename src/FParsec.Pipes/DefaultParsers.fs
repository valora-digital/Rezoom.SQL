[<AutoOpen>]
module FParsec.Pipes.DefaultParsers
open FParsec

/// Parse a character case insensitively. Returns the parsed character.
let pcharCI c : Parser<char, 'u> =
    let cfs = Text.FoldCase(c : char)
    fun stream ->
        if stream.SkipCaseFolded(cfs) then
             Reply(stream.Peek(-1))
        else Reply(Error, expectedString (string c))

/// Represents a parser whose output is captured within a pipeline.
[<NoComparison>]
[<NoEquality>]
type CaptureParser<'a, 'u> =
    | CaptureParser of Parser<'a, 'u>
    static member inline (---) (pipe, CaptureParser (p : Parser<'a, 'u>)) = appendCapture pipe p
    static member inline (?--) (pipe, CaptureParser (p : Parser<'a, 'u>)) = appendCaptureBacktrackLeft pipe p
    static member inline (--?) (pipe, CaptureParser (p : Parser<'a, 'u>)) = appendCaptureBacktrackRight pipe p

[<AllowNullLiteral>]
type DefaultParserOf<'a>() =
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<char>) = anyChar
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<float>) = pfloat
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<int8>) = pint8
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<int16>) = pint16
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<int32>) = pint32
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<int64>) = pint64
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<uint8>) = puint8
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<uint16>) = puint16
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<uint32>) = puint32
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<uint64>) = puint64
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<Position>) = getPosition
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf< ^x >) =
        (^x : (static member get_DefaultParser : unit -> Parser< ^x, unit >)())
and DefaultParser =
    | DefaultParser
    static member inline (%!!~~%) (_ : DefaultParser, cap : CaptureParser<_, _>) = cap
    static member inline (%!!~~%) (_ : DefaultParser, existing : Parser<'a, 'u>) = existing

    static member inline (%!!~~%) (_ : DefaultParser, literal : char) = pchar literal
    static member inline (%!!~~%) (_ : DefaultParser, literal : string) = pstring literal

    static member inline (%!!~~%) (_ : DefaultParser, list : _ list) =
        [| for parserish in list -> DefaultParser %!!~~% parserish
        |] |> choice
and CaseInsensitive<'a> =
    | CaseInsensitive of 'a
    static member inline (%!!~~%) (_ : DefaultParser, CaseInsensitive (literal : char)) = pcharCI literal
    static member inline (%!!~~%) (_ : DefaultParser, CaseInsensitive (literal : string)) = pstringCI literal

/// Mark `x` as being case insensitive.
/// Useful for use with `%`. For example `%ci "test"` is equivalent
/// to `pstringCI "test"`, while `%"test"` is equivalent to `pstring "test"`.
let ci x = CaseInsensitive x

/// Represents the default parser for the given type.
/// If the type `'a` has a default parser implemented, `p<'a>`
/// can be converted to a `Parser<'a, 'u>` with the % operator,
/// e.g. `%p<int>`.
let p<'a> = null : DefaultParserOf<'a>

/// Create a parser from `x`, if there is a single sensible parser possible.
/// For example, `defaultParser "str"` is equivalent to `pstring str`.
let inline defaultParser x : Parser<_, _> = DefaultParser %!!~~% x

/// Converts its argument to a parser via `defaultParser` and
/// marks the result as a captured input, which can be consumed
/// by the function at the end of a pipe.
let inline (~+.) x = CaptureParser (defaultParser x)

/// Chains `parser` onto `pipe`.
/// `parser` will be converted to a parser and may be captured or ignored based
/// on whether `+.` was used on it.
let inline (--) (pipe : Pipe<'inp, 'out, 'fn, 'r, 'u>) parser : Pipe<_, _, 'fn, _, 'u> =
    pipe --- (DefaultParser %!!~~% parser)

/// Chains `parser` onto `pipe`, with backtracking if `pipe` fails prior to `parser`.
/// `parser` will be converted to a parser and may be captured or ignored based
/// on whether `+.` was used on it.
let inline (?-) (pipe : Pipe<'inp, 'out, 'fn, 'r, 'u>) parser : Pipe<_, _, 'fn, _, 'u> =
    pipe ?-- (DefaultParser %!!~~% parser)

/// Chains `parser` onto `pipe`, with backtracking if `pipe` fails prior to `parser`
/// or `parser` fails without changing the parser state.
/// `parser` will be converted to a parser and may be captured or ignored based
/// on whether `+.` was used on it.
let inline (-?) (pipe : Pipe<'inp, 'out, 'fn, 'r, 'u>) parser : Pipe<_, _, 'fn, _, 'u> =
    pipe --? (DefaultParser %!!~~% parser)

/// Creates a pipe starting with `parser`. Shorthand for `pipe -- parser`.
let inline (~%%) parser : Pipe<_, _, _, _, _> =
    pipe -- parser

/// Prefix operator equivalent to `defaultParser x`.
let inline (~%) x = defaultParser x

/// Defines a self-referential parser given `defineParser`, which returns a parser given its own output parser.
/// The parser that will be passed to `defineParser` is a `createParserForwardedToRef()` pointed at a reference
/// that will be assigned to `defineParser`'s output.
let precursive defineParser =
    let p, pref = createParserForwardedToRef()
    pref := defineParser p
    p
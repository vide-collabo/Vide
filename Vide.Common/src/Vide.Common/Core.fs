namespace Vide

// TODO: generalize this again
// Why we return option(!) of 's? -> Because of else branch / zero:
// See elseForget: The state type of the else branch has to be the same
// state type used in the if branch, but: It has no value.
// So it must be an Option<'s>.

#if USE_SINGLECASEDU_WITHOUT_INLINEIFLAMBDA

type Vide<'v,'s,'c> = Vide of ('s option -> 'c -> 'v * 's option)

[<AutoOpen>]
module VideHandling =
    type InlineIfLambdaAttribute = FSharp.Core.InlineIfLambdaAttribute
    let inline mkVide v = Vide v
    let inline runVide (Vide v) = v

#else

type Vide<'v,'s,'c> = ('s option -> 'c -> 'v * 's option)

[<AutoOpen>]
module VideHandling =
    type InlineIfLambdaAttribute() = inherit System.Attribute()
    let inline mkVide (v: Vide<_,_,_>) = v
    let inline runVide (v: Vide<_,_,_>) = v

#endif

module Vide =

    // Preserves the first value given and discards subsequent values.
    let preserveValue x =
        mkVide <| fun s ctx ->
            let s = s |> Option.defaultValue x
            s, Some s
    
    let preserveWith x =
        mkVide <| fun s ctx ->
            let s = s |> Option.defaultWith x
            s, Some s
    
    // TODO: Think about which function is "global" and module-bound
    let inline map ([<InlineIfLambda>] proj: 'v1 -> 'v2) (v: Vide<'v1,'s,'c>) : Vide<'v2,'s,'c> =
        mkVide <| fun s ctx ->
            let v,s = (runVide v) s ctx
            proj v, s
    
    // why 's and not unit? -> see comment in "VideBuilder.Zero"
    [<GeneralizableValue>]
    let zero<'c> : Vide<unit,unit,'c> =
        mkVide <| fun s ctx -> (),None

    // this "zero", but the form where state is variable
    // -> see comment in "VideBuilder.Zero"
    // -> we don't really need that, only as "elseForget" or cases
    // with similar semantics.
    ////[<GeneralizableValue>]
    ////let empty<'s,'c> : Vide<unit,'s,'c> =
    ////    fun s ctx -> (),None

    [<GeneralizableValue>]
    let context<'c> : Vide<'c,unit,'c> =
        mkVide <| fun s ctx -> ctx,None

module BuilderBricks =
    let inline bind<'v1,'s1,'v2,'s2,'c>
        (
            m: Vide<'v1,'s1,'c>,
            [<InlineIfLambda>] f: 'v1 -> Vide<'v2,'s2,'c>
        ) 
        : Vide<'v2,'s1 option * 's2 option,'c>
        =
        mkVide <| fun s ctx ->
            let ms,fs =
                match s with
                | None -> None,None
                | Some (ms,fs) -> ms,fs
            let mv,ms = (runVide m) ms ctx
            let f = runVide (f mv)
            let fv,fs = f fs ctx
            fv, Some (ms,fs)

    let return'<'v,'c>
        (x: 'v)
        : Vide<'v,unit,'c> 
        =
        mkVide <| fun s ctx -> x,None

    let yield'<'v,'s,'c>
        (v: Vide<'v,'s,'c>)
        : Vide<'v,'s,'c>
        =
        v

    // This zero (with "unit" as state) is required for multiple returns.
    // Another zero (with 's as state) is required for "if"s without an "else".
    // We cannot have both, which means: We cannot have "if"s without "else".
    // This is ok (and not unfortunate), because the developer has to make a
    // decision about what should happen: "elseForget" or "elsePreserve".
    let zero<'c>
        ()
        : Vide<unit,unit,'c>
        = Vide.zero<'c>

    let inline delay<'v,'s,'c>
        ([<InlineIfLambda>] f: unit -> Vide<'v,'s,'c>)
        : Vide<'v,'s,'c>
        =
        f ()

    // TODO
    // those 2 are important for being able to combine
    // HTML elements, returns and asyncs in arbitrary order
    //member _.Combine
    //    (
    //        a: Vide<unit,'s1,'c>,
    //        b: Vide<'v,'s2,'c>
    //    )
    //    : Vide<'v,'s1 option * 's2 option,'c>
    //    =
    //    combine a b snd
    //member _.Combine
    //    (
    //        a: Vide<'v,'s1,'c>,
    //        b: Vide<unit,'s2,'c>
    //    )
    //    : Vide<'v,'s1 option * 's2 option,'c>
    //    =
    //    combine a b fst
    let combine<'v1,'s1,'v2,'s2,'c>
        (
           a: Vide<'v1,'s1,'c>,
           b: Vide<'v2,'s2,'c>
        )
        : Vide<'v2,'s1 option * 's2 option,'c>
        =
        mkVide <| fun s ctx ->
            let sa,sb =
                match s with
                | None -> None,None
                | Some (ms,fs) -> ms,fs
            let va,sa = (runVide a) sa ctx
            let vb,sb = (runVide b) sb ctx
            vb, Some (sa,sb)

    let inline for'<'a,'v,'s,'c when 'a: comparison>
        (
            input: seq<'a>,
            [<InlineIfLambda>] body: 'a -> Vide<'v,'s,'c>
        ) 
        : Vide<'v list, Map<'a, 's option>,'c>
        = 
        mkVide <| fun s ctx ->
            let mutable currMap = s |> Option.defaultValue Map.empty
            let resValues,resStates =
                [ for x in input do
                    let matchingState = currMap |> Map.tryFind x |> Option.flatten
                    let v,s = 
                        let v = runVide (body x)
                        v matchingState ctx
                    do currMap <- currMap |> Map.remove x
                    v, (x,s)
                ]
                |> List.unzip
            resValues, Some (resStates |> Map.ofList)

// For value restriction and other resolution issues, it's better to
// move these (remaining) builder methods "as close as possible" to the builder class that's
// most specialized.
type VideBaseBuilder() = class end

[<AutoOpen>]
module VideBaseBuilderCoreExtensions =
    type VideBaseBuilder with
        member _.Bind(m, f) = BuilderBricks.bind(m, f)
        member _.Zero() = BuilderBricks.zero()

[<AutoOpen>]
module ControlFlow =
    // TODO: More branches

    type Branch2<'b1,'b2> =
        | B1Of2 of 'b1
        | B2Of2 of 'b2
    
    type Branch3<'b1,'b2,'b3> =
        | B1Of3 of 'b1
        | B2Of3 of 'b2
        | B3Of3 of 'b3
    
    type Branch4<'b1,'b2,'b3,'b4> =
        | B1Of4 of 'b1
        | B2Of4 of 'b2
        | B3Of4 of 'b3
        | B4Of4 of 'b4

[<AutoOpen>]
module Keywords =
    
    [<GeneralizableValue>]
    let elsePreserve<'s,'c> : Vide<unit,'s,'c> =
        mkVide <| fun s ctx -> (),s

    [<GeneralizableValue>]
    let elseForget<'s,'c> : Vide<unit,'s,'c> = 
        mkVide <| fun s ctx -> (),None
        //Vide.empty<'s,'c>

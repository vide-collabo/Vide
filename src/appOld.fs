// TODOs:
//  - Don't calc the whole tree when triggering Update
//  - first class task/async support (in gen)
//  - implement "for" in ChildBuilder
//  - hide all the crazy generic type signatures

module App

open System
open LocSta
open Browser
open Browser.Types

[<AutoOpen>]
module DomExtensions =
    type NodeList with
        member this.toSeq = seq { for i in 0 .. this.length-1 do this.Item i }
    type NodeList with
        member this.toList = this.toSeq |> Seq.toList
    type Node with
        member this.clearChildren() = this.textContent <- "" // TODO: really?

type App(document: Document, appElement: Element, triggerUpdate: App -> Node list) =
    member _.Document = document
    member this.Run() =
        for elem in triggerUpdate this do
            appElement.appendChild elem |> ignore
    member this.TriggerUpdate() =
        printfn $"Trigger update"
        let element = triggerUpdate this
        // TODO: Sync returned element(s) with current
        ()

type AppGen<'v,'s> = Gen<'v,'s,App>
type RTState = { stateType: Type; boxedState: obj }
type RTAppGen<'v> = AppGen<'v,RTState>

module ViewBuilderFunctions =
    let inline typedGenToRTGen
        (typeofState: Type)
        (Gen x: Gen<'v,'s,'r>)
        : Gen<'v,RTState,'r>
        =
        fun s r ->
            let s =
                match s with
                | None -> None
                | Some (s: RTState) -> Some (s.boxedState :?> 's)
            let xv,xs = x s r
            xv, { stateType = typeofState; boxedState = xs }
        |> Gen

    let inline combine
        (a: RTAppGen<Node list>)
        (b: RTAppGen<Node list>)
        : RTAppGen<Node list>
        =
        // we need 's as a denominator for the state type
        // (it's statically known, but it's safer in case it changes)
        let g : Gen<_,'s,_> = 
            gen {
                let! aNodes = a
                let! bNodes = b
                return List.append aNodes bNodes
            }
        typedGenToRTGen typeof<'s> g

// TODO: Could it be that we neet "toRTAppGen" only in bind?
// TODO: Generalize (App, so that this can be used in any context / framework)
type ViewBuilder<'ret>([<InlineIfLambda>] emitResult: RTAppGen<Node list> -> 'ret) =

    member inline _.Bind(
        Gen m: AppGen<'v1,'s1>,
        f: 'v1 -> AppGen<'v2,'s2>)
        : RTAppGen<'v2>
        =
        fun mfState r ->
            let mState,fState =
                match mfState with
                | None -> None,None
                | Some s ->
                    // we want a nominal generic type parameter that is 's1 * 's2
                    let mState,fState = s.boxedState :?> 'stateType
                    Some mState, Some fState
            let mOut,mState' = m mState r
            let (Gen fgen) = f mOut
            let fOut,fState' = fgen fState r

            let state : 'stateType = mState', fState'
            fOut, { stateType = typeof<'stateType>; boxedState = state }
        |> Gen
    
    member inline _.Yield(
        x: AppGen<'v,'s>)
        : RTAppGen<Node list>
        =
        ViewBuilderFunctions.typedGenToRTGen (typeof<'s>) x |> Gen.map (fun xv -> [xv :> Node])

    member inline _.Yield(
        x: AppGen<'v list,'s>)
        : RTAppGen<Node list>
        =
        ViewBuilderFunctions.typedGenToRTGen (typeof<'s>) x |> Gen.map (List.map (fun x -> x :> Node))
    
    member _.Delay(
        f: unit -> RTAppGen<Node list>)
        : RTAppGen<Node list>
        =
        f()

    member _.Combine(
        a: RTAppGen<Node list>,
        b: RTAppGen<Node list>)
        : RTAppGen<Node list>
        =
        ViewBuilderFunctions.combine a b

    member inline this.For(
        s: seq<'a>,
        body: 'a -> RTAppGen<Node list>)
        : RTAppGen<Node list>
        =
        s
        |> Seq.map body
        |> Seq.fold ViewBuilderFunctions.combine (this.Zero())

    member inline _.Zero()
        : RTAppGen<Node list>
        =
        // 's: same reason as in Combine
        let res : Gen<_,'s,_> = Gen.ofValue []
        res |> ViewBuilderFunctions.typedGenToRTGen typeof<'s>

    member inline _.Run(children) : 'ret =
        emitResult children

let pov = ViewBuilder<_>(id)

[<AutoOpen>]
module HtmlElementsApi =
    let app : AppGen<_,_> = Gen (fun s r -> r,NoState)

    let inline syncAttributes (elem: Node) attributes =
        do for aname,avalue in attributes do
            let elemAttr = elem.attributes.getNamedItem aname
            if elemAttr.value <> avalue then
                elemAttr.value <- avalue

    let inline syncChildren (elem: Node) (children: RTAppGen<Node list>) =
        gen {
            let! children = children
            // TODO: Performance
            do elem.clearChildren()
            do for child in children do
                elem.appendChild child |> ignore
            return ()
        }

    let inline baseElem<'elem when 'elem :> HTMLElement and 'elem: equality> name =
        gen {
            let! app = app
            let! elem = Gen.preserve (fun () -> app.Document.createElement name :?> 'elem)
            printfn $"Eval: {name} ({elem.GetHashCode()})"
            return elem
        }

    let inline elem<'elem when 'elem :> HTMLElement and 'elem: equality> name attributes (children: RTAppGen<Node list>) =
        gen {
            let! elem = baseElem<'elem> name
            do syncAttributes elem attributes
            do! syncChildren elem children
            return elem
        }
    
    let span content =
        gen {
            let! elem = baseElem<HTMLSpanElement> "span"
            do if elem.textContent <> content then
                elem.textContent <- content
            return elem
        }

    let div attributes = ViewBuilder <| elem "div" attributes

    let p attributes = ViewBuilder <| elem "p" attributes

    let button attributes click =
        ViewBuilder <| fun children ->
            gen {
                let! app = app
                let! button = elem<HTMLButtonElement> "button" attributes children
                button.onclick <- fun _ ->
                    printfn "-----CLICK"
                    click ()
                    app.TriggerUpdate()
                return button :> Node // TODO: It's crap that we have to cast everything to "Node"
            }

let textInst = span "test"
// TODO: Value restriction
// let divInst = div [] { () }
// let buttonInst = button [] id { () }



let spanInst = span "test"
// TODO: Value restriction
let divInst()  = div [] { () }
let divInst2() = div [] { span "xxxx" }
let buttonInst() = button [] id { () }

let test1() =
    pov {
        span "test"
    }

let test2() =
    pov {
        span "test 1"
        span "test 2"
    }

let test3() =
    pov {
        span "test 1"
        div [] {
            ()
        }
        span "test 2"
    }

let test4() =
    pov {
        span "test 1"
        div [] {
            span "inner 1"
            span "inner 2"
        }
        span "test 2"
    }

let test5() =
    pov {
        let! c1, setCount = Gen.ofMutable 0
        span $"c1 = {c1}"

        div [] {
            span "inner 1"
            span "inner 2"
        }
        span "test 2"
        div [] {()}
    }

let test6() =
    pov {
        let! c1,_ = Gen.ofMutable 0
        span $"c1 = {c1}"
        
        let! c2,_ = Gen.ofMutable 0
        div [] {
            span $"c2 = {c2}"
            
            let! c3,_ = Gen.ofMutable 0
            span $"c3 = {c3}"
        }
    }

let test7() =
    pov {
        // TODO: document that this is not working (yield) and not useful.
        // - Maybe Gen.iter?
        // - or `wrap` to emit the spanElement afterwards?
        // - make also a "preserve" example
        let! spanElememt = span "test 1"
        printfn $"Span inner text: {spanElememt.innerText}"

        // yield spanElememt
        span "test 2"
    }

let comp() =
    pov {
        let! count, setCount = Gen.ofMutable 0
        div [] {
            div []  {
                span $"BEGIN for ..."
                for x in 0..3 do
                    span $"count = {count}"
                    button [] (fun () -> setCount (count + 1)) { 
                        span "..." 
                    }
                    span $"    (another x = {x})"
                    span $"    (another x = {x})"
                span $"END for ..."
            }
        }
    }


let view() =
   pov {
       div [] {
           comp()
           div [] {
               span "Hurz"
               comp()
           }
       }
   }
    

do
   App(
       document,
       document.querySelector("#app"),
       view() |> Gen.toEvaluable
   ).Run()

[<AutoOpen>]
module Vide.Fable

open Browser
open Browser.Types
open Vide
open System

type NodeList with 
    member this.ToSeq() = seq { for i in 0 .. this.length-1 do this.Item i }
    member this.ToList() = this.ToSeq() |> Seq.toList

module NodeExt =
    let displayString (node: Node) =
        let idOrDefault = try node.attributes.getNamedItem("id").value with _ -> "--"
        $"<{node.nodeName} id='{idOrDefault}'>"

type FableController
    (
        node: Node, 
        evaluateView: unit -> unit, 
        elementsContext: ElementsContext
    ) =
    inherit ControllerBase(evaluateView)
    member _.Node = node
    member _.ElementsContext = elementsContext

and ElementsContext(parent: Node) =
    let mutable keptChildren = []
    let memory child =
        keptChildren <- (child :> Node) :: keptChildren
        child
    let append child =
        do parent.appendChild(child) |> ignore
        child
    member _.AddElement<'n when 'n :> HTMLElement>(tagName: string) =
        document.createElement tagName |> memory |> append :?> 'n
    member _.AddText(text: string) =
        document.createTextNode text |> memory |> append
    member _.KeepChild(child: Node) =
        child |> memory |> ignore
    member _.GetObsoleteChildren() =
        let childNodes = parent.childNodes.ToList()
        childNodes |> List.except keptChildren

type Modifier<'n> = 'n -> unit
type NodeBuilderState<'n,'s when 'n :> Node> = option<'n> * option<'s>
type NodeCheckResult = Keep | DiscardAndCreateNew

type NodeBuilder<'n when 'n :> Node>
    (
        createNode: FableController -> 'n,
        checkOrUpdateNode: 'n -> NodeCheckResult
    ) =
    inherit VideBuilder()
    member val Modifiers: Modifier<'n> list = [] with get,set
    member val InitOnlyModifiers: Modifier<'n> list = [] with get,set
    //member _.CreateNode = createNode
    //member _.CheckOrUpdateNode = checkOrUpdateNode
    member this.AddModifier(m: Modifier<'n>) =
        do this.Modifiers <- m :: this.Modifiers
        this
    member this.AddInitOnlyModifier(m: Modifier<'n>) =
        do this.InitOnlyModifiers <- m :: this.InitOnlyModifiers
        this
    member this.Run
        (Vide childVide: Vide<'v,'fs,FableController>)
        : Vide<_, NodeBuilderState<'n,'fs>, FableController>
        =
        let runModifiers modifiers node =
            for x in modifiers do
                x node
        Vide <| fun s (controller: FableController) ->
            Debug.print "RUN:NodeBuilder"
            let s,cs = separateStatePair s
            let node,cs =
                match s with
                | None ->
                    let newNode,s = createNode controller,cs
                    do newNode |> runModifiers this.InitOnlyModifiers
                    newNode,s
                | Some node ->
                    match checkOrUpdateNode node with
                    | Keep ->
                        controller.ElementsContext.KeepChild(node)
                        node,cs
                    | DiscardAndCreateNew ->
                        createNode controller,None
            do runModifiers this.Modifiers node
            let childController = FableController(node, controller.EvaluateView, ElementsContext(node))
            let cv,cs = childVide cs childController
            for x in childController.ElementsContext.GetObsoleteChildren() do
                node.removeChild(x) |> ignore
                // we don'tneed this? Weak enough?
                // events.RemoveListener(node)
            cv, Some (Some node, cs)

// we always use EmitBuilder and "map ignore" the result in yield or use it in bind
////type DiscardNodeBuilder<'n when 'n :> Node>(newNode, checkOrUpdateNode) =
////    inherit NodeBaseBuilder<'n>(newNode, checkOrUpdateNode)
////    member this.Run
////        (
////            childVide: Vide<unit,'fs,Context>
////        ) : Vide<unit, NodeBuilderState<'fs, 'n>, Context>
////        =
////        this.SyncNode(childVide) |> map ignore

type HTMLElementBuilder<'n when 'n :> HTMLElement>(elemName: string) =
    inherit NodeBuilder<'n>(
        (fun controller -> controller.ElementsContext.AddElement<'n>(elemName)),
        (fun node ->
            match node.nodeName.Equals(elemName, StringComparison.OrdinalIgnoreCase) with
            | true -> Keep
            | false ->
                // TODO:
                console.log($"TODO: if/else detection? Expected node name: {elemName}, but was: {node.nodeName}")
                DiscardAndCreateNew
        )        
    )

module internal HtmlBase =
    // TODO: This is something special
    let inline nothing () =
        NodeBuilder(
            (fun controller -> controller.ElementsContext.AddElement "span"),
            (fun node -> Keep))
    // TODO: This is something special
    let inline text text =
        NodeBuilder(
            (fun controller -> controller.ElementsContext.AddText(text)),
            (fun node ->
                if typeof<Text>.IsInstanceOfType(node) then
                    if node.textContent <> text then
                        node.textContent <- text
                    Keep
                else
                    DiscardAndCreateNew
            ))

type VideBuilder with
    /// This allows constructs like:
    ///     let! emptyDivElement = div
    /// What is already allowed is (because of Run):
    ///     let! emptyDivElement = div { nothing }
    //member inline this.Bind
    //    (
    //        x: NodeBuilder<'n>,
    //        f: 'n -> Vide<'v,'s,FableController>
    //    ) : Vide<'v,NodeBuilderState<'n,unit> option * 's option, FableController>
    //    =
    //    let v = x { () }
    //    this.Bind(v, f)
    //member inline _.Yield<'n,'s,'c when 'n :> Node>
    member inline _.Yield<'v,'s,'c>
        (v: Vide<'v,'s,'c>)
        : Vide<unit,'s,'c>
        =
        Debug.print "YIELD Vide"
        v |> map ignore
    /// Same explanation as of Bind (see above)
    member inline _.Yield
        (nb: NodeBuilder<'n>)
        : Vide<unit, NodeBuilderState<'n,unit>, FableController>
        =
        Debug.print "YIELD NodeBuilder"
        nb { () } |> map ignore
    member inline _.Yield
        (s: string)
        : Vide<unit, NodeBuilderState<Text,unit>, FableController>
        =
        Debug.print "YIELD string"
        HtmlBase.text s { () } |> map ignore

module App =
    type RootBuilder<'n when 'n :> Node>(newNode, checkOrUpdateNode) =
        inherit NodeBuilder<'n>(newNode, checkOrUpdateNode)
        member inline _.Yield
            (x: Vide<unit,'s,FableController>)
            : Vide<unit,'s,FableController>
            =
            x
    
    let inline start (holder: #Node) (v: Vide<_,'s,FableController>) onEvaluated =
        let controller = FableController(holder, (fun () -> ()), ElementsContext(holder))
        let videMachine =
            VideMachine(
                None,
                controller,
                RootBuilder((fun _ -> holder), fun _ -> Keep) { v },
                onEvaluated)
        do controller.EvaluateView <- videMachine.EvaluateView
        videMachine

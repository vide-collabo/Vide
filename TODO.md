
Mandatory
---

* AttachedProperties (Wpfish)
* Solution for multiple content properties / templates + slots

Avalonia
---

* .HorizontalAlignment(HorizontalAlignment.Center) 
  -> Control.HAlign.Center
* Think about some way for css-like styling
* Provide some convenience for: type App() =



New Ideas / Brainstorm
---

* onRender (direct html element access), onMount, onUnmount
* timer / Observables
* SVG API
* Vide als .fsx file und dann einen C# SourceGen
* Work this idea out: Shadowing and defaults
  ```
    type [<AutoOpen>] AvaloniaControlsEx =
        static member inline TextBlock =
            AvaloniaControlBuilders.TextBlock().onInit(fun x -> x.node.FontSize <- 80.0)

        open type Vide.UI.AvaloniaControls
        open type AvaloniaControlsEx
    ```
* [<AutoOpen>] for all API factories? That would make `open type Vide.UI.AvaloniaControls` unnecessary.

Useful (already discovered)
---

* "Resetting" views
* RawSpan / Way of emitting HTML (not only text)
* async
    * Restart- und Retrigger-Verhalten
    * CancellationToken
    * Error handling
    * Timeouts
    * async button ("wait for click")
    * Maybe don't use later/promise, but async because of cancellation
    * UseCase: "Until" (geht schon jetzt)
    * UseCase: See AsyncWithSubsequentCalls
        // TODO: Interesting use case for component result
        // handling and ordering / evaluation
* Components
    * reset component / state from outside
    * Templates / slots
    * Provide an "element" function
    * yield support for fragments
        <>
            <....
        <>
    * Welche Arten?
        * CompoundComponents (=vide { ... })
        * RenderComponents (die die direkt auf ctx zugreuif)
    * Gutes Beispiel: Aus "input" eine "checkbox"-Komponente machen
    * Components with events
* Elements list + Remove / change / etc.
  * Provide an overload in BuilderBricks.for
  * call it VideList :)


Performance / Optimizations / Robustheit
---

* More [<InlineIfLambda>] on builder methods etc.
* 2 Property-APIs: For each Property: 1 for Init-Only and 1 for OnEval
* Compare current values and only set when different (also in 'bind' methods)
  * Mandatory for bind in conjunction with:
    [<Extension>]
    static member Text(this: #AvaloniaControlBuilders.TextBox, value) =
        this.onEval(fun x -> if x.node.Text <> value then x.node.Text <- value)

* Optimize "EvaluateView"
* Perf
  * diffing
  * instead of storing attr and events in the builder, they should have a direct effect on the underlying HTMLElement
* MemLeaks bei evt reg?
* Testcase:
    For-Loop mit disjunktem State -> wie verhält sich das?


Samples
---
* Conditional Attributes
* Conditional Elements
* State-Verschachtelungen (z.B. div in div mit jeweils State) oder State-In-List



Propably not
---
* Vide as no single case DU -> inlineIfLambda




----------------------------



* HTML Api
    * globals as base class
    * "add className"
    * className + class'
    * Properties / Ctor initialization
        type X(?myProp: int) =
            let mutable _myProp = defaultArg myProp 0
            member this.myProp(xxx) = _myProp <- xxx
            member this.myProp() = _myProp
    * Other events like checked change
    * hr, br etc. sollen Void sein
    * Events:
        * Overload ohne Argumente (Evaluiert nur)
        * Event soll ein Arg bekommen mit
            - Event (Fable)
            - Node
            - Eventkontext mit "TriggerEval: bool"
    * Input soll automatisch "OnChange" auslösen
    * Kontext weiter abstrahieren, damit man ohne Browser testen kann
    * input: checked / radio / etc.
    * form elements

* "Placeholder": A box that can be used later to place elements into


HTML Api Gen
---

* Don't call just ToString for attr values
* Enum types
* Choice types
* events not as hardcoded list
* type and member docu
* elem.attrs.attrs |> List.distinctBy (fun a -> a.name)
*
    let x1 = X()config..myProp()
    let x2 = X(myProp = 12).attrs.myProp()
    let x2 = X(myProp = 12).attrs.myProp()

Docu
---

* Perf: 2versions: 
  * a) SingleCaseDU
  * b) Function type + InlineIfLambda

* IMPORTANT: Do NOT do this, since this is a pitfall that I stumbled acrowss (TODO List Avalonia), and it causes really strange behaviour. Why? Controls are reused! Instead, it's absolutely necessary to always instanciate a new builder (Shadow a new type or use somehow other styling techniques):
    let Button = Button.Margin(Thickness 5.0)
    let TextBlock = TextBlock.Margin(Thickness 5.0)
    let TextBox = TextBox.Margin(Thickness 5.0)
    let DockPanel = DockPanel.LastChildFill(true)


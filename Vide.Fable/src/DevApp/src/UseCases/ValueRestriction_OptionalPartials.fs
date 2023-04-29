namespace ValueRestriction_OptionalPartials

// see also: https://github.com/RonaldSchlenker/Vide/issues/3

module Alt_With_Turning_Into_Functions =

    open Vide
    open type Vide.Html

    let Card
        (
            content: Vide<_,_,_>,
            footerContent: Vide<_,_,_> option
        ) =
        vide {
            div { content }
        
            match footerContent with
            | Some footerContent -> header { footerContent }
            | None -> Vide.elseForget
        }

    let cards() = vide {
        Card(
            vide {
                let! counter = Vide.ofMutable 0
                $"The current count is {counter.Value} :)"
            },
            Some (vide { "Footer is here" })
        )

        Card(
            vide { "This is just another Usage" },
            None
        )
    }

    let view() = vide { article { main { cards() } } }


module Alt_With_Specifying_GenArgs =

    open Vide
    open type Vide.Html

    let Card<'vc,'sc,'sf>
        (
            content: Vide<'vc,'sc,_>,
            footerContent: Vide<_,'sf,_> option
        ) =
        vide {
            div { content }
        
            match footerContent with
            | Some footerContent -> header { footerContent }
            | None -> Vide.elseForget
        }

    let cards = vide {
        Card(
            vide {
                let! counter = Vide.ofMutable 0
                $"The current count is {counter.Value} :)"
            },
            Some (vide { "Footer is here" })
        )

        Card<_,_,unit>(
            vide { "This is just another Usage" },
            None
        )
    }

    let view = vide { article { main { cards } } }


module Alt_With_VideNone =

    open Vide
    open type Vide.Html

    let Card
        (
            content: Vide<_,_,_>,
            footerContent: Vide<_,_,_> option
        ) =
        vide {
            div { content }
        
            match footerContent with
            | Some footerContent -> header { footerContent }
            | None -> Vide.elseForget
        }

    let videNone : Vide<unit,unit,_> option = None

    let cards = vide {
        Card(
            vide {
                let! counter = Vide.ofMutable 0
                $"The current count is {counter.Value} :)"
            },
            Some (vide { "Footer is here" })
        )

        Card(
            vide { "This is just another Usage" },
            videNone
        )
    }

    let view = vide { article { main { cards } } }
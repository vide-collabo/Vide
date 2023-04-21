module DevApp.UseCases.GettingStarted

open Vide
open type Vide.Html

// TODO: demo for async + clear

let helloWorld =
    vide { "Hello World" }

let counter =
    vide {
        let! count = Vide.ofMutable 0

        div { $"Count = {count.Value}" }
        button.onclick(fun _ -> count -= 1) { "dec" }
        button.onclick(fun _ -> count += 1) { "inc" }
    }

let conditionalAttributes =
    vide {
        let! count = Vide.ofMutable 0

        button.onclick(fun _ -> count += 1) {
            $"Hit me! Count = {count.Value}"
        }
        div.class'("the-message") {
            span.hidden(count.Value <> 5) {
                "You have the right to defend yourself!"
            }
        }
    }
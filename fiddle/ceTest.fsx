
// module Test

// type Builder() =
//     member inline this.Yield(x) = [x]
//     member inline _.Delay(f: unit -> _) = f
//     member _.Combine(a, b) = List.append a (b())
//     member _.Zero() = []
//     member _.Run(children) = children()
//     member inline _.For(inputList: seq<'a>, body: 'a -> list<'b>) : list<'b> =
//         [ for x in inputList do yield! body x ]
// let test = Builder()

// let res = 
//     test { 
//         for x in 0..10 do
//             string x
//     }








// #if INTERACTIVE
// #else
// module Test
// #endif


// type Gen<'o> = { value: 'o }
// type RTGen = { message: string }

// let toRTGen x = { message = string x.value }
// let bind (m: Gen<_>) (f: _ -> Gen<_>) : Gen<_> = f m.value

// // type Delayed = 

// type Builder() =
//     member _.Bind(m: Gen<'o>, f: 'o -> RTGen) : RTGen =
//         bind m (fun v ->
//             let rtgen = f v
//             { value = rtgen.message }
//         )
//         |> toRTGen
//     member _.Return(x: RTGen) = x
//     // member _.Delay(f) = f
//     // member _.Combine(a, b) = a :: b
//     // member _.Zero() = []
//     // member _.Run(children: RTGen list) = children

// let test = Builder()

// let res = 
//     test { 
//         let! x = { value = 100 }
//         let! y = { value = 200 }
//         return { message = $"First value is: {x} and {y}"}
    
//         // let! z = { value = "Hello World" }
//         // return { message = $"Second value is: {z}"}
//     }




open System

type App = App
type Gen<'o,'s,'r> = { value: 'o; state: 's }
type AppGen<'o,'s> = Gen<'o,'s,App>
type NoState = NoState

type RTState = { stateTypeName: string; state: obj }

let toRTState (x: 'a) =
    let rec getTypeName (t: Type) =
        match t.GenericTypeArguments with
        | [| |] -> t.Name
        | args ->
            let args = args |> Array.map getTypeName |> String.concat ", "
            $"{t.Name}<{args}>"
    { stateTypeName = getTypeName typeof<'a>
      state = x :> obj }

let bind (m: AppGen<'a,'s1>) (f: 'a -> AppGen<'b,'s2>) : AppGen<'b,'s1*'s2> =
    let fres = f m.value
    { value = fres.value
      state = m.state,fres.state }


let ofValue v : AppGen<_,_> = { value = v; state = NoState }

type YieldedOrCombined<'a> = YieldedOrCombined of 'a list
type Delayed<'a> = Delayed of 'a list

type Builder() =
    member inline _.Bind(m: AppGen<'a,'s1>, f: 'a -> AppGen<'b,'s2>) : AppGen<'b,_> =
        printfn $"BIND     -  m.value = {m.value}"
        let fres = bind m f
        { value = fres.value
          state = toRTState (m.state, fres.state) }
    member _.Yield(x: Gen<'a,'s,_>) =
        printfn $"YIELD    -  x.value = {x.value}"
        { value = YieldedOrCombined [x.value]
          state = toRTState x.state }
    member _.Delay(f: unit -> AppGen<YieldedOrCombined<'a>, RTState>) : AppGen<Delayed<'a>, RTState> =
        let fres = f()
        let (YieldedOrCombined fvalue) = fres.value
        printfn $"DELAY    -  f() = {fvalue}"
        { value = Delayed fvalue 
          state = fres.state }
    member _.Combine(a: AppGen<YieldedOrCombined<'a>, 's>, b: AppGen<Delayed<'a>, RTState>) =
        printfn $"COMBINE  -  a.value = {a.value}  -  b.value = {b.value}"
        let (YieldedOrCombined avalues) = a.value
        let (Delayed bvalues) = b.value
        { value = YieldedOrCombined (avalues @ bvalues)
          state = toRTState (a.state, b.state) }
    member inline _.For(
            sequence: seq<'a>,
            body: 'a -> AppGen<YieldedOrCombined<'o>, RTState>
            ) : AppGen<YieldedOrCombined<'o>, RTState> =
        failwith "TODO"
        // [ for x in sequence do
        //     yield! body x
        // ]
    member inline _.Zero() =
        printfn $"ZERO"
        { value = YieldedOrCombined []
          state = toRTState NoState }
    member _.Run(elements: AppGen<Delayed<'v>, RTState>) =
        printfn $"RUN"
        elements

let test = Builder()



let a =
    test {
        let! a = { value = 100; state = "a" }
        let! b = { value = 200; state = 44.2 }
        { value = a + b; state = 20UL }
        
        let! c = { value = 33; state = "c" }
        let! d = { value = 66; state = 44.1 }
        { value = c + d; state = 10.0 }

        { value = -77; state = 20.0 }
        for i in 0..3 do
            { value = -77; state = 20.0 }

        let! e = { value = -2; state = [909090] }
        let! f = { value = -3; state = (0.1, 0.2, 0.3) }
        for i in 0..3 do
            { value = e + f + i; state = ("Hello", "World") }
            { value = e + f + i; state = ("Hello", "World") }
            { value = e + f + i; state = ("Hello", "World") }

        { value = e + f; state = ("Hello", "World") }
    }


let b =
    test {
        let! a = { value = 100; state = "a" }
        let! b = { value = 200; state = 44.2 }
        { value = a + b; state = 20UL }
        
        let! c = { value = 33; state = "c" }
        let! d = { value = 66; state = 44.1 }
        { value = c + d; state = 10.0 }
        { value = -77; state = 20.0 }
    }


let c =
    test {
        { value = 33; state = 20UL }
        { value = -77; state = 20.0 }
    }

let d =
    test {
        { value = -77; state = 20.0 }
    }

let e =
    test {
        if true then
            { value = -77; state = 20.0 }
    }

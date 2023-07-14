module HtmlApiGenerator

open FSharp.Text.TypedTemplateProvider
open W3schoolScrape

let [<Literal>] HtmlApiTemplate = """
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was auto generated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Vide

open System.Runtime.CompilerServices
open Browser.Types
open Vide
open Vide.WebModel
open Vide.ApiPre

[<AutoOpen>]
module HtmlEnumAttributeTypes =
    {{for enum in enumElements}}
    module {{enum.elemName}} =
        {{for enumType in enum.types}}
        [<RequireQualifiedAccess>]
        type ``{{enumType.name}}`` = {{for enumLabel in enumType.labels}}
        | ``{{enumLabel.name}}`` {{end}}
        {{end}}
    {{end}}

module HtmlElementBuilders =
    type HtmlGARenderPotC0Builder<'v,'e when 'e :> HTMLElement and 'e: equality>(tagName, resultSelector) =
        inherit RenderPotC0Builder<'v,'e>(BuilderHelper.createNode tagName, (fun node -> BuilderHelper.checkNode tagName node.nodeName), resultSelector)

    type HtmlGARenderRetC0Builder<'e when 'e :> HTMLElement and 'e: equality>(tagName) =
        inherit RenderRetC0Builder<'e>(BuilderHelper.createNode tagName, (fun node -> BuilderHelper.checkNode tagName node.nodeName))

    type HtmlGARenderPotCnBuilder<'v,'e when 'e :> HTMLElement and 'e: equality>(tagName, resultSelector) =
        inherit RenderPotCnBuilder<'v,'e>(BuilderHelper.createNode tagName, (fun node -> BuilderHelper.checkNode tagName node.nodeName), resultSelector)

    type HtmlGARenderRetCnBuilder<'e when 'e :> HTMLElement and 'e: equality>(tagName) =
        inherit RenderRetCnBuilder<'e>(BuilderHelper.createNode tagName, (fun node -> BuilderHelper.checkNode tagName node.nodeName))

    {{for builder in builders}}{{builder.definition}}{{end}}

open HtmlElementBuilders

{{for ext in builderExtensions}}
[<Extension>]
type {{ext.builderName}}Extensions =
    class
        // Attributes
        {{for attr in ext.attributes}}
{{attr.xmlDoc}}
        [<Extension>]
        static member {{attr.memberName}}(this: {{ext.builderParamTypeAnnotation}}, value: {{attr.typ}}) =
            this.onEval({{attr.setterCode}})
        {{end}}

        // Events
        {{for evt in ext.events}}
{{evt.xmlDoc}}
        [<Extension>]
        static member {{evt.memberName}}(this: {{ext.builderParamTypeAnnotation}}, handler) =
            this.onEval(fun x -> x.node.{{evt.name}} <- Event.handle x.node x.globalContext handler)

{{evt.xmlDoc}}
        [<Extension>]
        static member {{evt.memberName}}(this: {{ext.builderParamTypeAnnotation}}, ?requestEvaluation: bool) =
            this.onEval(fun x -> x.node.{{evt.name}} <- Event.handle x.node x.globalContext (fun args ->
                args.requestEvaluation <- defaultArg requestEvaluation true))
        {{end}}
    end
{{end}}

type Html =
    {{for builder in builders}}
{{builder.xmlDoc}}
    static member inline {{builder.name}} = HtmlElementBuilders.{{builder.name}}(){{builder.pipedConfig}}
    {{end}}
"""

type Api = Template<HtmlApiTemplate>

let generate (elements: Element list) (globalAttrs: Attr list) (globalEvents: Evt list) =

    let makeEnumTypeName (attrName: string) = $"{attrName}"

    let makeCodeDoc (desc: string) indent =
        desc.Split('\n')
        |> Array.map (fun s ->
            let indent = String.replicate indent "    "
            $"{indent}/// {s}")
        |> String.concat "\n"

    let builders =
        [ for elem in elements do
            let builderDefinition =
                let valueTypeName = $"{elem.tagName}Value"
                match elem.returnsValue, elem.elementType with
                | true,Void ->
                    $"""
    type {elem.fsharpName}() =
        inherit HtmlGARenderPotC0Builder<{valueTypeName}, {elem.domInterfaceName}>
            (
                "{elem.tagName}",
                fun node -> {valueTypeName}(node)
            )
                    """

                | false,Void ->
                    $"""
    type {elem.fsharpName}() =
        inherit HtmlGARenderRetC0Builder<{elem.domInterfaceName}>("{elem.tagName}")
                    """
                
                | true,Content ->
                    $"""
    type {elem.fsharpName}() =
        inherit HtmlGARenderPotC0Builder<{valueTypeName}, {elem.domInterfaceName}>
            (
                "{elem.tagName}",
                fun node -> {valueTypeName}(node)
            )
                    """


                | false,Content ->

                    $"""
    type {elem.fsharpName}() =
        inherit HtmlGARenderRetCnBuilder<{elem.domInterfaceName}>("{elem.tagName}")
                    """
            
            let pipedConfig = "" //if elem.returnsValue then ".oninput()" else ""

            Api.builder(
                builderDefinition,
                elem.fsharpName,
                pipedConfig,
                makeCodeDoc elem.desc 1
            )
        ]

    let enumTypes =
        let allAttrs = 
            [ for elem in elements do 
                elem.fsharpName, elem.attrs 
              "Global", globalAttrs
            ]
        [ for elemName,attrs in allAttrs do
            let enumType =
                [ for attr in attrs do
                    for attrType in attr.types do
                        match attrType with
                        | AttrTyp.Enum labels -> Some (makeEnumTypeName attr.name, labels)
                        | _ -> None
                ]
                |> List.choose id
                |> List.map (fun (name,labels) ->
                    let enumLabels = labels |> List.map (fun l -> Api.enumLabel(l.fsharpName))
                    Api.enumType(enumLabels, name))
            Api.enum(elemName, enumType)
        ]
        |> List.filter (fun x -> x.types.Length > 0)
    
    let builderExtensions =
        let makeAttrs (elemName: string) (attrs: Attr list) =
            [ for attr in attrs do
                for attrType in attr.types do
                    let typ =
                        match attrType with
                        | AttrTyp.Text -> "string"
                        | AttrTyp.Boolean Present -> "bool"
                        | AttrTyp.Boolean TrueFalse -> "bool"
                        | AttrTyp.Enum _ -> $"{elemName}.``{makeEnumTypeName attr.name}``"
                    let setterCode =
                        match attr.setMode with
                        | SetAttribute ->
                            let setAttrString valueAsString =
                                $"""fun x -> x.node.setAttribute("{attr.name}", %s{valueAsString})"""
                            let body =
                                match attrType with
                                | AttrTyp.Text -> 
                                    setAttrString "value"
                                | AttrTyp.Boolean Present ->
                                    $"""fun x -> if value then x.node.setAttribute("{attr.name}", null) else x.node.removeAttribute("{attr.name}") """
                                | AttrTyp.Boolean TrueFalse ->
                                    setAttrString """if value then "true" else "false" """
                                | AttrTyp.Enum _ ->
                                    setAttrString "value.ToString()"
                            body
                        | DomPropertySetter ->
                            $"""fun x -> x.node.{attr.name} <- value"""
                    Api.attr(
                        attr.fsharpName, 
                        setterCode,
                        typ,
                        makeCodeDoc attr.desc 2
                    )
            ]

        let makeEvts (evts: Evt list) =
            [ for evt in evts do
                Api.evt(evt.name, evt.name, makeCodeDoc evt.desc 2)
            ]

        [
            let globalElementPseudoName = "Global"

            Api.ext(
                makeAttrs globalElementPseudoName globalAttrs,
                "HtmlGARenderPotC0Builder",
                "#HtmlGARenderPotC0Builder<_,_>",
                makeEvts globalEvents
            )
            Api.ext(
                makeAttrs globalElementPseudoName globalAttrs,
                "HtmlGARenderRetC0Builder",
                "#HtmlGARenderRetC0Builder<_>",
                makeEvts globalEvents
            )
            Api.ext(
                makeAttrs globalElementPseudoName globalAttrs,
                "HtmlGARenderPotCnBuilder",
                "#HtmlGARenderPotCnBuilder<_,_>",
                makeEvts globalEvents
            )
            Api.ext(
                makeAttrs globalElementPseudoName globalAttrs,
                "HtmlGARenderRetCnBuilder",
                "#HtmlGARenderRetCnBuilder<_>",
                makeEvts globalEvents
            )

            for elem in elements do
                Api.ext(
                    makeAttrs elem.fsharpName elem.attrs, 
                    elem.fsharpName, 
                    $"#{elem.fsharpName}", 
                    []
                )
        ]

    let root = Api.Root(builderExtensions, builders, enumTypes)

    Api.Render(root)

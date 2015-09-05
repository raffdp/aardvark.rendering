﻿namespace Aardvark.SceneGraph

open System
open System.Runtime.InteropServices
open System.Collections.Generic

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.Base.Rendering

module Sg =

    [<AbstractClass>]
    type AbstractApplicator(child : IMod<ISg>) =
        interface IApplicator with
            member x.Child = child

        member x.Child = child

        new(child : ISg) = AbstractApplicator(Mod.constant child)

    type AdapterNode(node : obj) =
        interface ISg

        member x.Node = node

    type DynamicNode(child : IMod<ISg>) = inherit AbstractApplicator(child)

    type RenderNode(call : IMod<DrawCallInfo>) =
        interface ISg

        member x.DrawCallInfo = call

        new(call : IEvent<DrawCallInfo>) = RenderNode(Mod.fromEvent call)
        new(call : DrawCallInfo) = RenderNode(Mod.constant call)
    
    type VertexAttributeApplicator(values : Map<Symbol, BufferView>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Values = values

        new(values : Map<Symbol, BufferView>, child : ISg)            = VertexAttributeApplicator(values, Mod.constant child)
        new(semantic : Symbol, value : BufferView, child : IMod<ISg>) = VertexAttributeApplicator(Map.ofList [semantic, value], child)
        new(semantic : Symbol, value : BufferView, child : ISg)       = VertexAttributeApplicator(Map.ofList [semantic, value], Mod.constant child)
        new(values : SymbolDict<BufferView>, child : ISg)             = VertexAttributeApplicator(values |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Map.ofSeq, Mod.constant child)

    type VertexIndexApplicator(value : IMod<Array>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Value = value

        new(value : IMod<Array>, child : ISg)         = VertexIndexApplicator(value, Mod.constant child)
        new(value : IEvent<Array>, child : IMod<ISg>) = VertexIndexApplicator(Mod.fromEvent value, child)
        new(value : IEvent<Array>, child : ISg)       = VertexIndexApplicator(Mod.fromEvent value, Mod.constant child)

    type InstanceAttributeApplicator(values : Map<Symbol, BufferView>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Values = values

        new(values : Map<Symbol, BufferView>, child : ISg)            = InstanceAttributeApplicator(values, Mod.constant child)
        new(semantic : Symbol, value : BufferView, child : IMod<ISg>) = InstanceAttributeApplicator(Map.ofList [semantic, value], child)
        new(semantic : Symbol, value : BufferView, child : ISg)       = InstanceAttributeApplicator(Map.ofList [semantic, value], Mod.constant child)
        new(values : SymbolDict<BufferView>, child : ISg)             = InstanceAttributeApplicator(values |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Map.ofSeq, Mod.constant child)
 

    type OnOffNode(on : IMod<bool>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.IsActive = on

        new(on : IMod<bool>, child : ISg) = OnOffNode(on, Mod.constant child)
        new(on : IEvent<bool>, child : IMod<ISg>) = OnOffNode(Mod.fromEvent on, child)
        new(on : IEvent<bool>, child : ISg) = OnOffNode(Mod.fromEvent on, Mod.constant child)

    type PassApplicator(pass : IMod<uint64>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Pass = pass

        new(pass : IMod<uint64>, child : ISg) = PassApplicator(pass, Mod.constant child)
        new(pass : IEvent<uint64>, child : IMod<ISg>) = PassApplicator(Mod.fromEvent pass, child)
        new(pass : IEvent<uint64>, child : ISg) = PassApplicator(Mod.fromEvent pass, Mod.constant child)

    type UniformApplicator(uniformHolder : IUniformProvider, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member internal x.Uniforms = uniformHolder
        
        member x.TryFindUniform (scope : Scope) (name : Symbol) =
            uniformHolder.TryGetUniform (scope,name)

        new(value : IUniformProvider, child : ISg) = UniformApplicator( value, Mod.constant child)
        new(name : string, value : IMod, child : ISg) = UniformApplicator( (new Providers.SimpleUniformHolder ([Symbol.Create name,value]) :> IUniformProvider), Mod.constant child)
        new(name : Symbol, value : IMod, child : ISg) = UniformApplicator( (new Providers.SimpleUniformHolder( [name,value]) :> IUniformProvider), Mod.constant child)
        new(name : Symbol, value : IMod, child : IMod<ISg>) = UniformApplicator( (new Providers.SimpleUniformHolder( [name,value]) :> IUniformProvider), child)
        new(map : Map<Symbol,IMod>, child : ISg) = UniformApplicator( (new Providers.SimpleUniformHolder( map) :> IUniformProvider), Mod.constant child)


    type SurfaceApplicator(surface : IMod<ISurface>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Surface = surface

        new(value : IMod<ISurface>, child : ISg) = SurfaceApplicator(value, Mod.constant child)

        new(value : IEvent<ISurface>, child : IMod<ISg>) = SurfaceApplicator(Mod.fromEvent value, child)
        new(value : IEvent<ISurface>, child : ISg) = SurfaceApplicator(Mod.fromEvent value, Mod.constant child)
        new(value : ISurface, child : ISg) = SurfaceApplicator(Mod.constant value, Mod.constant child)

    type TextureApplicator(semantic : Symbol, texture : IMod<ITexture>, child : IMod<ISg>) =
        inherit UniformApplicator(semantic, texture :> IMod, child)

        member x.Texture = texture

        new(semantic : Symbol, texture : IMod<ITexture>, child : ISg) = TextureApplicator(semantic, texture, Mod.constant child)
        new(semantic : Symbol, texture : IEvent<ITexture>, child : IMod<ISg>) = TextureApplicator(semantic, Mod.fromEvent texture, child)
        new(semantic : Symbol, texture : IEvent<ITexture>, child : ISg) = TextureApplicator(semantic, Mod.fromEvent texture, Mod.constant child)
        new(texture : IMod<ITexture>, child : ISg) = TextureApplicator(DefaultSemantic.DiffuseColorTexture, texture, child)
        new(texture : IEvent<ITexture>, child : IMod<ISg>) = TextureApplicator(DefaultSemantic.DiffuseColorTexture,  texture, child)
        new(texture : IEvent<ITexture>, child : ISg) = TextureApplicator(DefaultSemantic.DiffuseColorTexture, texture, child)
        new(texture : IMod<ITexture>, child : IMod<ISg>) = TextureApplicator(DefaultSemantic.DiffuseColorTexture,texture,child)


    type TrafoApplicator(trafo : IMod<Trafo3d>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Trafo = trafo

        new(value : IMod<Trafo3d>, child : ISg) = TrafoApplicator(value, Mod.constant child)
        new(value : IEvent<Trafo3d>, child : IMod<ISg>) = TrafoApplicator(Mod.fromEvent value, child)
        new(value : IEvent<Trafo3d>, child : ISg) = TrafoApplicator(Mod.fromEvent value, Mod.constant child)
        new(value : Trafo3d, child : ISg) = TrafoApplicator(Mod.constant value, Mod.constant child)
    
    type ViewTrafoApplicator(trafo : IMod<Trafo3d>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.ViewTrafo = trafo

        new(value : IMod<Trafo3d>, child : ISg) = ViewTrafoApplicator(value, Mod.constant child)
        new(value : IEvent<Trafo3d>, child : IMod<ISg>) = ViewTrafoApplicator(Mod.fromEvent value, child)
        new(value : IEvent<Trafo3d>, child : ISg) = ViewTrafoApplicator(Mod.fromEvent value, Mod.constant child)

    type ProjectionTrafoApplicator(trafo : IMod<Trafo3d>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.ProjectionTrafo = trafo

        new(value : IMod<Trafo3d>, child : ISg) = ProjectionTrafoApplicator(value, Mod.constant child)
        new(value : IEvent<Trafo3d>, child : IMod<ISg>) = ProjectionTrafoApplicator(Mod.fromEvent value, child)
        new(value : IEvent<Trafo3d>, child : ISg) = ProjectionTrafoApplicator(Mod.fromEvent value, Mod.constant child)


    type DepthTestModeApplicator(mode : IMod<DepthTestMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<DepthTestMode>, child : ISg) = DepthTestModeApplicator(value, Mod.constant child)
        new(value : IEvent<DepthTestMode>, child : IMod<ISg>) = DepthTestModeApplicator(Mod.fromEvent value, child)
        new(value : IEvent<DepthTestMode>, child : ISg) = DepthTestModeApplicator(Mod.fromEvent value, Mod.constant child)

    type CullModeApplicator(mode : IMod<CullMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<CullMode>, child : ISg) = CullModeApplicator(value, Mod.constant child)
        new(value : IEvent<CullMode>, child : IMod<ISg>) = CullModeApplicator(Mod.fromEvent value, child)
        new(value : IEvent<CullMode>, child : ISg) = CullModeApplicator(Mod.fromEvent value, Mod.constant child)

    type FillModeApplicator(mode : IMod<FillMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<FillMode>, child : ISg) = FillModeApplicator(value, Mod.constant child)
        new(value : IEvent<FillMode>, child : IMod<ISg>) = FillModeApplicator(Mod.fromEvent value, child)
        new(value : IEvent<FillMode>, child : ISg) = FillModeApplicator(Mod.fromEvent value, Mod.constant child)
        new(value : FillMode, child : ISg) = FillModeApplicator(Mod.constant value, Mod.constant child)

    type StencilModeApplicator(mode : IMod<StencilMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<StencilMode>, child : ISg) = StencilModeApplicator(value, Mod.constant child)
        new(value : IEvent<StencilMode>, child : IMod<ISg>) = StencilModeApplicator(Mod.fromEvent value, child)
        new(value : IEvent<StencilMode>, child : ISg) = StencilModeApplicator(Mod.fromEvent value, Mod.constant child)

    type BlendModeApplicator(mode : IMod<BlendMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<BlendMode>, child : ISg) = BlendModeApplicator(value, Mod.constant child)
        new(value : IEvent<BlendMode>, child : IMod<ISg>) = BlendModeApplicator(Mod.fromEvent value, child)
        new(value : IEvent<BlendMode>, child : ISg) = BlendModeApplicator(Mod.fromEvent value, Mod.constant child)

    type RasterizerStateApplicator(state : IMod<RasterizerState>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        let depth = state |> Mod.map (fun s -> s.DepthTest)
        let cull = state |> Mod.map (fun s -> s.CullMode)
        let fill = state |> Mod.map (fun s -> s.FillMode)
        let stencil = state |> Mod.map (fun s -> s.StencilMode)
        let blend = state |> Mod.map (fun s -> s.BlendMode)

        member x.RasterizerState = state
        member x.DepthTestMode = depth
        member x.CullMode = cull
        member x.FillMode = fill
        member x.StencilMode = stencil
        member x.BlendMode = blend

        new(value : IMod<RasterizerState>, child : ISg) = RasterizerStateApplicator(value, Mod.constant child)
        new(value : IEvent<RasterizerState>, child : IMod<ISg>) = RasterizerStateApplicator(Mod.fromEvent value, child)
        new(value : IEvent<RasterizerState>, child : ISg) = RasterizerStateApplicator(Mod.fromEvent value, Mod.constant child)


    type Group(elements : seq<ISg>) =
        let aset = cset(elements)

        interface IGroup with
            member x.Children = x.ASet

        member x.ASet : aset<ISg> = aset :> aset<_>

        member x.Add v =
            transact (fun () ->
               aset.Add v
            )

        member x.Remove v =
            transact (fun () ->
                aset.Remove v
            )

        member x.Clear() =
            transact (fun () ->
                aset.Clear()
            )

        member x.UnionWith v =
            transact (fun () ->
                aset.UnionWith v
            )

        member x.ExceptWith v =
            transact (fun () ->
                aset.ExceptWith v
            )

        member x.SymmetricExceptWith v =
            transact (fun () ->
                aset.SymmetricExceptWith v
            )

        member x.IntersectWith v =
            transact (fun () ->
                aset.IntersectWith v
            )


        member x.Count = aset.Count

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = (aset :> System.Collections.IEnumerable).GetEnumerator()

        interface IEnumerable<ISg> with
            member x.GetEnumerator() = (aset :> seq<_>).GetEnumerator()

        interface ICollection<ISg> with
            member x.IsReadOnly = false
            member x.Add v = x.Add v |> ignore
            member x.Remove v = x.Remove v
            member x.Clear() = x.Clear()
            member x.Contains v = aset.Contains v
            member x.Count = x.Count
            member x.CopyTo(arr, index) =
                let mutable id = index
                for e in aset do
                    arr.[id] <- e
                    id <- id + 1

        interface ISet<ISg> with
            member x.Add v = x.Add v
            member x.UnionWith other = x.UnionWith other
            member x.IntersectWith other = x.IntersectWith other
            member x.ExceptWith other = x.ExceptWith other
            member x.SymmetricExceptWith other = x.SymmetricExceptWith other
            member x.IsSubsetOf other = (aset :> ISet<ISg>).IsSubsetOf other
            member x.IsSupersetOf other = (aset :> ISet<ISg>).IsSupersetOf other
            member x.IsProperSubsetOf other = (aset :> ISet<ISg>).IsProperSubsetOf other
            member x.IsProperSupersetOf other = (aset :> ISet<ISg>).IsProperSupersetOf other
            member x.Overlaps other = (aset :> ISet<ISg>).Overlaps other
            member x.SetEquals other = (aset :> ISet<ISg>).SetEquals other

        new() = Group(Seq.empty)
        
    type Set(content : aset<ISg>) =

        interface IGroup with
            member x.Children = content

        member x.ASet = content


    type Environment (runtime : IRuntime, viewTrafo : IMod<Trafo3d>, projTrafo : IMod<Trafo3d>, viewSize : IMod<V2i>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Runtime = runtime
        member x.ViewTrafo = viewTrafo
        member x.ProjTrafo = projTrafo
        member x.ViewSize = viewSize

        member x.Scene = child

        new(runtime : IRuntime, viewTrafo : IEvent<Trafo3d>, projTrafo : IEvent<Trafo3d>, viewSize : IEvent<V2i>, child : ISg) =
            Environment(runtime, Mod.fromEvent viewTrafo, Mod.fromEvent projTrafo, Mod.fromEvent viewSize, Mod.constant child)
        new(runtime : IRuntime, viewTrafo : IEvent<Trafo3d>, projTrafo : IEvent<Trafo3d>, viewSize : IEvent<V2i>, child : IMod<ISg>) =
            Environment(runtime, Mod.fromEvent viewTrafo, Mod.fromEvent projTrafo, Mod.fromEvent viewSize, child)
        new(runtime : IRuntime, viewTrafo : IMod<Trafo3d>, projTrafo : IMod<Trafo3d>, viewSize : IMod<V2i>, child : ISg) =
            Environment(runtime, viewTrafo, projTrafo, viewSize, Mod.constant child)


module SceneGraphCompletenessCheck =
    open System.Text.RegularExpressions

    let semantics =
        [
            "RenderObjects"
            "GlobalBoundingBox"
            "LocalBoundingBox"
        ]

    let genericNameRx = Regex @"(?<name>.*?)´[0-9]+"
    let cleanName (name : string) =
        let m = genericNameRx.Match name
        if m.Success then m.Groups.["name"].Value
        else name

    let intrisicNames =
        Dict.ofList [
            typeof<byte>, "byte"
            typeof<int8>, "int8"
            typeof<uint16>, "uint16"
            typeof<int16>, "int16"
            typeof<int>, "int"
            typeof<uint32>, "uint32"
            typeof<int64>, "int64"
            typeof<uint64>, "uint64"
            typeof<obj>, "obj"
        ]

    let rec prettyName (t : Type) =
        match intrisicNames.TryGetValue t with
            | (true, n) -> n
            | _ -> 
                if t.IsArray then 
                    sprintf "%s[]" (t.GetElementType() |> prettyName)
                elif t.IsGenericType then
                    let args = t.GetGenericArguments() |> Seq.map prettyName |> String.concat ","
                    sprintf "%s<%s>" (cleanName t.Name) args
                else
                    cleanName t.Name

    [<OnAardvarkInit>]
    let checkSemanticCompleteness() =
        let sgTypes = Introspection.GetAllClassesImplementingInterface(typeof<ISg>)

        let sgModule = typeof<Sg.Group>.DeclaringType

        for att in semantics do
            let semTypes = HashSet<Type>()
            for t in sgTypes do
                if t.DeclaringType = sgModule then
                    match t |> Ag.tryGetAttributeType att with
                        | Some attType ->
                            semTypes.Add attType |> ignore
                        | None ->
                            Log.warn "no semantic %A for type %s" att (prettyName t)

            if semTypes.Count > 1 then
                let allTypes = semTypes |> Seq.map prettyName |> String.concat "; "
                Log.warn "conflicting types for semantic functions %A [%s]" att allTypes


        ()


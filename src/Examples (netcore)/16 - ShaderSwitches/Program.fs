﻿open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open FShade

// This example illustrates how to use dynamic shaders.

[<EntryPoint>]
let main argv =

    // first we need to initialize Aardvark's core components
    Aardvark.Init()

    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug true
            samples 8
        }

    let activeShader = AVal.init 0

    let effects =
        [|
            // red
            Effect.compose [
                toEffect DefaultSurfaces.trafo
                toEffect (DefaultSurfaces.constantColor C4f.Red)
            ]

            // red with lighting
            Effect.compose [
                toEffect DefaultSurfaces.trafo
                toEffect (DefaultSurfaces.constantColor C4f.Red)
                toEffect DefaultSurfaces.simpleLighting
            ]

            // vertex colors with lighting
            Effect.compose [
                toEffect DefaultSurfaces.trafo
                toEffect DefaultSurfaces.simpleLighting
            ]

            // texture with lighting
            Effect.compose [
                toEffect DefaultSurfaces.trafo
                toEffect DefaultSurfaces.diffuseTexture
                toEffect DefaultSurfaces.simpleLighting
            ]
        |]


    win.Keyboard.DownWithRepeats.Values.Add (fun k ->
        match k with
            | Keys.Enter ->
                transact (fun () -> activeShader.Value <- (activeShader.Value + 1) % effects.Length)
            | _ ->
                ()
    )

    let sg =
        Sg.box' C4b.Green Box3d.Unit
            |> Sg.diffuseTexture DefaultTextures.checkerboard
            |> Sg.effectPool effects activeShader

    // show the scene in a simple window
    win.Scene <- sg
    win.Run()

    0

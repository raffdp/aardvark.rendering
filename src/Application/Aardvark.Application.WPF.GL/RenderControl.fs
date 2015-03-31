﻿namespace Aardvark.Application.WPF

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering.GL
open OpenTK.Graphics.OpenGL4
open System.Windows
open System.Windows.Controls
open System.Windows.Forms.Integration
open Aardvark.Application

type private WinFormsControl = Aardvark.Application.WinForms.OpenGlRenderControl

type OpenGlRenderControl(context : Context, samples : int) as this =
    inherit WindowsFormsHost()
    let ctrl = new WinFormsControl(context, samples)

    do this.Child <- ctrl
       this.Loaded.Add(fun e -> this.Focusable <- false)

    member x.Inner = ctrl

    member x.RenderTask
        with get() = ctrl.RenderTask
        and set t = ctrl.RenderTask <- t

    member x.Sizes = ctrl.Sizes

    member x.Time = ctrl.Time
    interface IRenderTarget with
        member x.Time = ctrl.Time
        member x.RenderTask
            with get() = x.RenderTask
            and set t = x.RenderTask <- t

        member x.Sizes = x.Sizes

    new(context) = new OpenGlRenderControl(context, 1)
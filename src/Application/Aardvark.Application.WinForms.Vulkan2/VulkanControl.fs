﻿namespace Aardvark.Application.WinForms

open System
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Windows.Forms
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Aardvark.Application.WinForms.Vulkan

type VulkanControl(device : Device, graphicsMode : AbstractGraphicsMode) =
    inherit UserControl()

    do base.SetStyle(ControlStyles.UserPaint, true)
       base.SetStyle(ControlStyles.DoubleBuffer, false)
       base.SetStyle(ControlStyles.AllPaintingInWmPaint, true)
       base.SetStyle(ControlStyles.Opaque, true)
       base.SetStyle(ControlStyles.ResizeRedraw, true)

    let mutable surface : Surface = Unchecked.defaultof<_>
    let mutable swapchainDescription : SwapchainDescription = Unchecked.defaultof<_>
    let mutable swapchain : Swapchain = Unchecked.defaultof<_>
    let mutable loaded = false


    member x.SwapChainDescription = 
        if not x.IsHandleCreated then x.CreateHandle()
        swapchainDescription

    member x.RenderPass = 
        if not x.IsHandleCreated then x.CreateHandle()
        swapchainDescription.renderPass


    abstract member OnRenderFrame : RenderPass * DeviceQueue * Framebuffer -> unit
    default x.OnRenderFrame(_,_,_) = ()

    override x.OnHandleCreated(e) =
        base.OnHandleCreated e
        surface <- device.CreateSurface(x)
        swapchainDescription <- device.CreateSwapchainDescription(surface, graphicsMode)
        swapchain <- device.CreateSwapchain(swapchainDescription)

        loaded <- true


    override x.OnPaint(e) =
        base.OnPaint(e)

        if loaded then

            swapchain.RenderFrame(fun queue framebuffer ->
                Aardvark.Base.Incremental.EvaluationUtilities.evaluateTopLevel (fun () ->
                    x.OnRenderFrame(swapchainDescription.renderPass, queue, framebuffer)
                )
            )


    override x.Dispose(d) =
        base.Dispose(d)

        if loaded then
            loaded <- false
            device.Delete swapchain
            device.Delete swapchainDescription
            device.Delete surface

type VulkanRenderControl(runtime : Runtime, graphicsMode : AbstractGraphicsMode) as this =
    inherit VulkanControl(runtime.Device, graphicsMode)
    
    static let messageLoop = MessageLoop()
    static do messageLoop.Start()

    let mutable renderTask : IRenderTask = Unchecked.defaultof<_>
    let mutable taskSubscription : IDisposable = null
    let mutable sizes = Mod.init (V2i(this.ClientSize.Width, this.ClientSize.Height))
    let mutable needsRedraw = false
    let mutable renderContiuously = false

    let time = Mod.custom(fun _ -> DateTime.Now)

    override x.OnHandleCreated(e) =
        base.OnHandleCreated(e)
        x.KeyDown.Add(fun e ->
            if e.KeyCode = System.Windows.Forms.Keys.End && e.Control then
                renderContiuously <- not renderContiuously
                x.Invalidate()
                e.Handled <- true
        )

    override x.OnRenderFrame(pass, queue, fbo) =
        needsRedraw <- false
        let s = V2i(x.ClientSize.Width, x.ClientSize.Height)
        if s <> sizes.Value then
            transact (fun () -> Mod.change sizes s)

        queue.WaitIdle()



        renderTask.Run(fbo) |> ignore

        //x.Invalidate()
        transact (fun () -> time.MarkOutdated())
        if renderContiuously then
            x.Invalidate()


    member x.Time = time
    member x.Sizes = sizes :> IMod<_>

    member x.FramebufferSignature = x.RenderPass :> IFramebufferSignature

    member private x.ForceRedraw() =
        messageLoop.Draw(x)

    member x.RenderTask
        with get() = renderTask
        and set t = 
            if not (isNull taskSubscription) then 
                taskSubscription.Dispose()
                renderTask.Dispose()

            renderTask <- t
            taskSubscription <- t.AddMarkingCallback x.ForceRedraw

    interface IControl with
        member x.IsInvalid = needsRedraw
        member x.Invalidate() =
            if not x.IsDisposed && not renderContiuously then
                if not needsRedraw then
                    needsRedraw <- true
                    x.Invalidate()

        member x.Paint() =
            if not x.IsDisposed && not renderContiuously then
                use g = x.CreateGraphics()
                use e = new System.Windows.Forms.PaintEventArgs(g, x.ClientRectangle)
                x.InvokePaint(x, e)

        member x.Invoke f =
            if not x.IsDisposed then
                base.Invoke (new System.Action(f)) |> ignore

    interface IRenderTarget with
        member x.FramebufferSignature = x.RenderPass :> IFramebufferSignature
        member x.Runtime = runtime :> IRuntime
        member x.RenderTask
            with get() = x.RenderTask
            and set t = x.RenderTask <- unbox t

        member x.Samples = 1
        member x.Sizes = sizes :> IMod<_>
        member x.Time = time



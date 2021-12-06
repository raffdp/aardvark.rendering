﻿namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open FSharp.Data.Adaptive

type ClearTask(device : Device, renderPass : RenderPass, values : aval<ClearValues>) =
    inherit AdaptiveObject()

    let id = newId()
    let pool = device.GraphicsFamily.CreateCommandPool()
    let cmd = pool.CreateCommandBuffer(CommandBufferLevel.Primary)

    let renderPassDepthAspect =
        match renderPass.DepthStencilAttachment with
        | Some (_, signature) ->
            let depth, stencil =
                RenderbufferFormat.hasDepth signature.format,
                RenderbufferFormat.hasStencil signature.format

            match depth, stencil with
            | true,  true  -> ImageAspect.DepthStencil
            | true,  false -> ImageAspect.Depth
            | false, true  -> ImageAspect.Stencil
            | false, false -> ImageAspect.None
        | _ ->
            ImageAspect.None

    member x.Run(caller : AdaptiveToken, t : RenderToken, outputs : OutputDescription, queries : IQuery) =
        x.EvaluateAlways caller (fun caller ->
            let fbo = unbox<Framebuffer> outputs.framebuffer
            use token = device.Token

            let values = values.GetValue caller
            let depth = values.Depth
            let stencil = values.Stencil

            let vulkanQueries = queries.ToVulkanQuery()

            queries.Begin()

            token.enqueue {
                for q in vulkanQueries do
                    do! Command.Begin q

                let views = fbo.ImageViews

                for KeyValue(i, (sem, _)) in renderPass.ColorAttachments do
                    match values.Colors.[sem] with
                    | Some color -> do! Command.ClearColor(views.[i], ImageAspect.Color, color)
                    | _ -> ()

                if renderPassDepthAspect <> ImageAspect.None then
                    let view = views.[views.Length - 1]
                    match depth, stencil with
                    | Some d, Some s -> do! Command.ClearDepthStencil(view, renderPassDepthAspect, d, s)
                    | Some d, None   -> do! Command.ClearDepthStencil(view, ImageAspect.Depth, d, 0)
                    | None, Some s   -> do! Command.ClearDepthStencil(view, ImageAspect.Stencil, 0.0, s)
                    | None, None     -> ()

                for q in vulkanQueries do
                    do! Command.End q
            }

            queries.End()

            token.Sync()
        )

    interface IRenderTask with
        member x.Id = id
        member x.Update(c, t) = ()
        member x.Run(c,t,o,q) = x.Run(c,t,o,q)
        member x.Dispose() =
            cmd.Dispose()
            pool.Dispose()

        member x.FrameId = 0UL
        member x.FramebufferSignature = Some (renderPass :> _)
        member x.Runtime = Some device.Runtime
        member x.Use f = lock x f
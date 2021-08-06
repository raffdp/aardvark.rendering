﻿open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO
open Aardvark.SceneGraph.Raytracing
open Aardvark.Application
open Aardvark.Application.Slim
open FSharp.Data.Adaptive

[<AutoOpen>]
module Semantics =
    module HitGroup =
        let Model = Sym.ofString "HitGroupModel"
        let Floor = Sym.ofString "HitGroupFloor"
        let Sphere = Sym.ofString "HitGroupSphere"

    module MissShader =
        let Shadow = Sym.ofString "MissShadow"

    module InstanceAttribute =
        let ModelMatrix = Sym.ofString "ModelMatrix"
        let NormalMatrix = Sym.ofString "NormalMatrix"

    module GeometryAttribute =
        let Colors = Sym.ofString "Colors"

module Effect =
    open FShade

    [<AutoOpen>]
    module private Shaders =

        type UniformScope with
            member x.OutputBuffer : Image2d<Formats.rgba32f> = uniform?OutputBuffer
            member x.RecursionDepth : int = uniform?RecursionDepth

            member x.GeometryInfos  : TraceGeometryInfo[]   = uniform?StorageBuffer?GeometryInfos
            member x.Positions      : V3d[]                 = uniform?StorageBuffer?Positions
            member x.Normals        : V3d[]                 = uniform?StorageBuffer?Normals
            member x.Indices        : int[]                 = uniform?StorageBuffer?Indices
            member x.TextureCoords  : V2d[]                 = uniform?StorageBuffer?TextureCoords
            member x.ModelMatrices  : M44d[]                = uniform?StorageBuffer?ModelMatrices
            member x.NormalMatrices : M33d[]                = uniform?StorageBuffer?NormalMatrices
            member x.Colors         : V3d[]                 = uniform?StorageBuffer?Colors

            member x.SphereOffsets  : V3d[]                 = uniform?StorageBuffer?SphereOffsets

        type Payload =
            {
                recursionDepth : int
                color : V3d
            }

        let private mainScene =
            scene {
                accelerationStructure uniform?MainScene
            }

        let private textureFloor =
            sampler2d {
                texture uniform?TextureFloor
                filter Filter.MinMagPointMipLinear
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        [<ReflectedDefinition>]
        let trace (origin : V3d) (offset : V2d) (input : RayGenerationInput) =
            let pixelCenter = V2d input.work.id.XY + offset
            let inUV = pixelCenter / V2d input.work.size.XY
            let d = inUV * 2.0 - 1.0

            let target = uniform.ProjTrafoInv * V4d(d, 1.0, 1.0)
            let direction = uniform.ViewTrafoInv * V4d(target.XYZ.Normalized, 0.0)

            let payload = { recursionDepth = 0; color = V3d.Zero }
            let result = mainScene.TraceRay<Payload>(origin.XYZ, direction.XYZ, payload, flags = RayFlags.CullBackFacingTriangles)
            result.color

        let rgenMain (input : RayGenerationInput) =
            raygen {
                let origin = uniform.ViewTrafoInv * V4d.WAxis

                let c1 = input |> trace origin.XYZ (V2d(0.25, 0.25))
                let c2 = input |> trace origin.XYZ (V2d(0.75, 0.25))
                let c3 = input |> trace origin.XYZ (V2d(0.25, 0.75))
                let c4 = input |> trace origin.XYZ (V2d(0.75, 0.75))
                let final = (c1 + c2 + c3 + c4) / 4.0

                uniform.OutputBuffer.[input.work.id.XY] <- V4d(final, 1.0)
            }

        let missSolidColor (color : C3d) =
            let color = V3d color

            miss {
                return { color = color; recursionDepth = 0 }
            }

        let missShadow =
            miss { return false }

        [<ReflectedDefinition>]
        let fromBarycentric (v0 : V3d) (v1 : V3d) (v2 : V3d) (coords : V2d) =
            let barycentricCoords = V3d(1.0 - coords.X - coords.Y, coords.X, coords.Y)
            v0 * barycentricCoords.X + v1 * barycentricCoords.Y + v2 * barycentricCoords.Z

        [<ReflectedDefinition>]
        let fromBarycentric2d (v0 : V2d) (v1 : V2d) (v2 : V2d) (coords : V2d) =
            let barycentricCoords = V3d(1.0 - coords.X - coords.Y, coords.X, coords.Y)
            v0 * barycentricCoords.X + v1 * barycentricCoords.Y + v2 * barycentricCoords.Z

        [<ReflectedDefinition>]
        let getGeometryInfo (input : RayHitInput<'T, 'U>) =
            let id = input.geometry.instanceCustomIndex + input.geometry.geometryIndex
            uniform.GeometryInfos.[id]

        [<ReflectedDefinition>]
        let getIndices (info : TraceGeometryInfo) (input : RayHitInput<'T, 'U>) =
            let firstIndex = info.FirstIndex + 3 * input.geometry.primitiveId
            let baseVertex = info.BaseVertex

            V3i(uniform.Indices.[firstIndex],
                uniform.Indices.[firstIndex + 1],
                uniform.Indices.[firstIndex + 2]) + baseVertex

        [<ReflectedDefinition>]
        let getPosition (indices : V3i) (input : RayHitInput<'T, V2d>) =
            let p0 = uniform.Positions.[indices.X]
            let p1 = uniform.Positions.[indices.Y]
            let p2 = uniform.Positions.[indices.Z]
            input.hit.attribute |> fromBarycentric p0 p1 p2

        [<ReflectedDefinition>]
        let getNormal (indices : V3i) (input : RayHitInput<'T, V2d>) =
            let n0 = uniform.Normals.[indices.X]
            let n1 = uniform.Normals.[indices.Y]
            let n2 = uniform.Normals.[indices.Z]
            input.hit.attribute |> fromBarycentric n0 n1 n2

        [<ReflectedDefinition>]
        let getTextureCoords (indices : V3i) (input : RayHitInput<'T, V2d>) =
            let uv0 = uniform.TextureCoords.[indices.X]
            let uv1 = uniform.TextureCoords.[indices.Y]
            let uv2 = uniform.TextureCoords.[indices.Z]
            input.hit.attribute |> fromBarycentric2d uv0 uv1 uv2

        [<ReflectedDefinition>]
        let diffuseLighting (normal : V3d) (position : V3d) =
            let L = uniform.LightLocation - position |> Vec.normalize
            let NdotL = Vec.dot normal L |> max 0.0

            let ambient = 0.3
            ambient + NdotL

        [<ReflectedDefinition>]
        let specularLighting (shininess : float) (normal : V3d) (position : V3d) =
            let L = uniform.LightLocation - position |> Vec.normalize
            let V = uniform.CameraLocation - position |> Vec.normalize
            let R = Vec.reflect normal -L
            let VdotR = Vec.dot V R |> max 0.0
            pow VdotR shininess

        [<ReflectedDefinition>]
        let reflection (depth : int) (direction : V3d) (normal : V3d) (position : V3d) =
            if depth < uniform.RecursionDepth then
                let direction = Vec.reflect normal direction
                let payload = { recursionDepth = depth + 1; color = V3d.Zero }
                let result = mainScene.TraceRay(position, direction, payload, flags = RayFlags.CullBackFacingTriangles)
                result.color
            else
                V3d.Zero

        [<ReflectedDefinition>]
        let lightingWithShadow (mask : int32) (reflectiveness : float) (specularAmount : float) (shininess : float)
                               (position : V3d) (normal : V3d) (input : RayHitInput<Payload>) =

            let shadowed =
                let direction = Vec.normalize (uniform.LightLocation - position)
                let flags = RayFlags.SkipClosestHitShader ||| RayFlags.TerminateOnFirstHit ||| RayFlags.Opaque ||| RayFlags.CullFrontFacingTriangles
                mainScene.TraceRay<bool>(position, direction, payload = true, miss = "MissShadow", flags = flags, minT = 0.01, cullMask = mask)

            let diffuse = diffuseLighting normal position

            let result =
                if reflectiveness > 0.0 then
                    let reflection = reflection input.payload.recursionDepth input.ray.direction normal position
                    reflectiveness |> lerp (V3d(diffuse)) reflection
                else
                    V3d(diffuse)

            if shadowed then
                0.3 * result
            else
                let specular = specularLighting shininess normal position
                result + specularAmount * V3d(specular)

        let chitSolidColor (color : C3d) (input : RayHitInput<Payload>) =
            let color = V3d color

            closesthit {
                let info = getGeometryInfo input
                let indices = getIndices info input

                let position =
                    let p = getPosition indices input
                    let m = uniform.ModelMatrices.[info.InstanceAttributeIndex]
                    m.TransformPos p

                let normal =
                    let n = getNormal indices input
                    let m = uniform.NormalMatrices.[info.InstanceAttributeIndex]
                    (m * n) |> Vec.normalize

                let diffuse = lightingWithShadow 0xFF 0.0 1.0 16.0 position normal input
                return { color = color * diffuse; recursionDepth = 0 }
            }

        let chitTextured (input : RayHitInput<Payload>) =
            closesthit {
                let info = getGeometryInfo input
                let indices = getIndices info input

                let position =
                    let p = getPosition indices input
                    let m = uniform.ModelMatrices.[info.InstanceAttributeIndex]
                    m.TransformPos p

                let texCoords =
                    getTextureCoords indices input

                let color = textureFloor.Sample(texCoords).XYZ
                let diffuse = lightingWithShadow 0xFF 0.3 0.5 28.0 position V3d.ZAxis input
                return { color = color * diffuse; recursionDepth = 0 }
            }

        let chitSphere (input : RayHitInput<Payload>) =
            closesthit {
                let info = getGeometryInfo input
                let position = input.ray.origin + input.hit.t * input.ray.direction
                let center = input.objectSpace.objectToWorld.TransformPos uniform.SphereOffsets.[input.geometry.geometryIndex]
                let normal = Vec.normalize (position - center)

                let color = uniform.Colors.[info.GeometryAttributeIndex]
                let diffuse = lightingWithShadow 0x7F 0.8 1.0 28.0 position normal input
                return { color = color * diffuse; recursionDepth = 0 }
            }

        let intersectionSphere (radius : float) (input : RayIntersectionInput) =
            intersection {
                let origin = input.objectSpace.rayOrigin - uniform.SphereOffsets.[input.geometry.geometryIndex]
                let direction = input.objectSpace.rayDirection

                let a = Vec.dot direction direction
                let b = 2.0 * Vec.dot origin direction
                let c = (Vec.dot origin origin) - (radius * radius)

                let discriminant = b * b - 4.0 * a * c
                if discriminant >= 0.0 then
                    let t = (-b - sqrt discriminant) / (2.0 * a)
                    Intersection.Report(t) |> ignore
            }


    let private hitgroupModel =
        hitgroup {
            closesthit (chitSolidColor C3d.BurlyWood)
        }

    let private hitgroupFloor =
        hitgroup {
            closesthit chitTextured
        }

    let private hitgroupSphere =
        hitgroup {
            closesthit chitSphere
            intersection (intersectionSphere 0.2)
        }


    let main =
        raytracing {
            raygen rgenMain
            miss (missSolidColor C3d.Lavender)
            miss (MissShader.Shadow, missShadow)
            hitgroup (HitGroup.Model, hitgroupModel)
            hitgroup (HitGroup.Floor, hitgroupFloor)
            hitgroup (HitGroup.Sphere, hitgroupSphere)
        }

[<EntryPoint>]
let main argv =
    Aardvark.Init()

    let rnd = RandomSystem()

    use app = new VulkanApplication(debug = true)
    let runtime = app.Runtime :> IRuntime

    use win = app.CreateGameWindow(samples = 1)

    let cameraView =
        let initialView = CameraView.LookAt(V3d.One * 10.0, V3d.Zero, V3d.OOI)
        DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    let viewTrafo =
        cameraView |> AVal.map CameraView.viewTrafo

    let projTrafo =
        win.Sizes
        |> AVal.map (fun s ->
            Frustum.perspective 60.0 0.1 150.0 (float s.X / float s.Y)
            |> Frustum.projTrafo
        )

    let traceTexture =
        runtime.CreateTexture2D(TextureFormat.Rgba32f, 1, win.Sizes)

    let geometryPool =
        let signature =
            let vertexAttributes =
                Map.ofList [
                    DefaultSemantic.Positions, typeof<V4f>
                    DefaultSemantic.Normals, typeof<V4f>
                    DefaultSemantic.DiffuseColorCoordinates, typeof<V2f>
                ]

            let instanceAttributes =
                Map.ofList [
                    InstanceAttribute.ModelMatrix, typeof<M44f>
                    InstanceAttribute.NormalMatrix, typeof<M34f>
                ]

            let geometryAttributes =
                Map.ofList [
                    GeometryAttribute.Colors, typeof<V4f>
                ]

            { IndexType              = IndexType.UInt32
              VertexAttributeTypes   = vertexAttributes
              InstanceAttributeTypes = instanceAttributes
              GeometryAttributeTypes = geometryAttributes }

        new ManagedRaytracingPool(runtime, signature)

    use model =
        let scene = Loader.Assimp.load (Path.combine [__SOURCE_DIRECTORY__; "..";"..";"..";"data";"aardvark";"aardvark.obj"])

        let trafo =
            Trafo3d.Scale(5.0, 5.0, -5.0) * Trafo3d.Translation(0.0, 0.0, 1.5)

        let instanceAttributes =
            let normalMatrix =
                trafo.Backward.Transposed.UpperLeftM33()

            Map.ofList [
                InstanceAttribute.ModelMatrix, AVal.constant trafo.Forward :> IAdaptiveValue
                InstanceAttribute.NormalMatrix, AVal.constant normalMatrix :> IAdaptiveValue
            ]

        let instance =
            AdaptiveTraceInstance.ofIndexedGeometry' GeometryFlags.Opaque trafo scene.meshes.[0].geometry
            |> AdaptiveTraceInstance.instanceAttributes instanceAttributes

        geometryPool.Add(instance)

    use floor =
        let positions = [| V3f(-0.5f, -0.5f, 0.0f); V3f(-0.5f, 0.5f, 0.0f); V3f(0.5f, -0.5f, 0.0f); V3f(0.5f, 0.5f, 0.0f); |]
        let indices = [| 0; 1; 2; 3 |]
        let uv = positions |> Array.map (fun p -> p.XY + 0.5f)

        let vertexAttributes =
            SymDict.ofList [
                DefaultSemantic.Positions, positions :> System.Array
                DefaultSemantic.DiffuseColorCoordinates, uv :> System.Array
            ]

        let trafo = Trafo3d.Scale(48.0)

        let instanceAttributes =
            let normalMatrix =
                trafo.Backward.Transposed.UpperLeftM33()

            Map.ofList [
                InstanceAttribute.ModelMatrix, AVal.constant trafo.Forward :> IAdaptiveValue
                InstanceAttribute.NormalMatrix, AVal.constant normalMatrix :> IAdaptiveValue
            ]

        let geometry =
            IndexedGeometry(IndexedGeometryMode.TriangleStrip, indices, vertexAttributes, SymDict.empty)

        let instance =
            AdaptiveTraceInstance.ofIndexedGeometry' GeometryFlags.Opaque trafo geometry
            |> AdaptiveTraceInstance.instanceAttributes instanceAttributes

        geometryPool.Add(instance)

    let sphereOffsets =
        let o1 = V3d(0.0, 0.0, 0.5)
        let o2 = V3d(0.0, 0.0, -0.5)
        let o3 = V3d(0.5, 0.0, 0.0)
        let o4 = V3d(-0.5, 0.0, 0.0)
        let o5 = V3d(0.0, 0.5, 0.0)
        let o6 = V3d(0.0, -0.5, 0.0)

        [| o1; o2; o3; o4; o5; o6 |]

    let indices =
        geometryPool.IndexBuffer

    let positions =
        geometryPool.GetVertexAttribute DefaultSemantic.Positions

    let textureCoordinates =
        geometryPool.GetVertexAttribute DefaultSemantic.DiffuseColorCoordinates

    let normals =
        geometryPool.GetVertexAttribute DefaultSemantic.Normals

    let modelMatrices =
        geometryPool.GetInstanceAttribute InstanceAttribute.ModelMatrix

    let normalMatrices =
        geometryPool.GetInstanceAttribute InstanceAttribute.NormalMatrix

    let colors =
        geometryPool.GetGeometryAttribute GeometryAttribute.Colors

    let geometryInfos =
        geometryPool.GeometryBuffer

    let lightLocation =
        let startTime = System.DateTime.Now
        win.Time |> AVal.map (fun t ->
            let t = (t - startTime).TotalSeconds
            V3d(Rot2d(t * Constant.PiQuarter) * V2d(8.0, 0.0), 16.0)
        )

    let uniforms =
        Map.ofList [
            Sym.ofString "OutputBuffer",     traceTexture |> AdaptiveResource.mapNonAdaptive (fun t -> t :> ITexture) :> IAdaptiveValue
            Sym.ofString "RecursionDepth",   AVal.constant 4 :> IAdaptiveValue
            Sym.ofString "ViewTrafo",        viewTrafo :> IAdaptiveValue
            Sym.ofString "ProjTrafo",        projTrafo :> IAdaptiveValue
            Sym.ofString "CameraLocation",   cameraView |> AVal.map CameraView.location :> IAdaptiveValue
            Sym.ofString "GeometryInfos",    geometryInfos :> IAdaptiveValue
            Sym.ofString "Positions",        positions :> IAdaptiveValue
            Sym.ofString "Normals",          normals :> IAdaptiveValue
            Sym.ofString "Indices",          indices :> IAdaptiveValue
            Sym.ofString "TextureCoords",    textureCoordinates :> IAdaptiveValue
            Sym.ofString "ModelMatrices",    modelMatrices :> IAdaptiveValue
            Sym.ofString "NormalMatrices",   normalMatrices :> IAdaptiveValue
            Sym.ofString "Colors",           colors :> IAdaptiveValue
            Sym.ofString "SphereOffsets",    sphereOffsets |> Array.map V4f |> ArrayBuffer :> IBuffer |> AVal.constant :> IAdaptiveValue
            Sym.ofString "TextureFloor",     DefaultTextures.checkerboard :> IAdaptiveValue
            Sym.ofString "LightLocation",    lightLocation :> IAdaptiveValue
        ]

    let staticObjects =
        let objModel =
            traceObject {
                geometry model.Geometry
                customIndex model.Index
                hitgroup HitGroup.Model
                culling (CullMode.Enabled WindingOrder.Clockwise)
            }

        let objFloor =
            traceObject {
                geometry floor.Geometry
                customIndex floor.Index
                hitgroup HitGroup.Floor
                culling (CullMode.Enabled WindingOrder.CounterClockwise)
            }

        ASet.ofList [objModel; objFloor]

    let sphereObjects =
        cset<TraceObject>()

    let createSphere =

        let colors = [|
            C3d.BlueViolet
            C3d.DodgerBlue
            C3d.HoneyDew
            C3d.BlanchedAlmond
            C3d.LimeGreen
            C3d.MistyRose
        |]

        fun () ->
            let colors =
                let p = rnd.CreatePermutationArray(colors.Length)
                colors |> Array.permute (fun i -> p.[i])

            let mti =
                sphereOffsets |> Array.map (fun offset ->
                    BoundingBoxes.ofCenterAndRadius offset 0.2
                    |> BoundingBoxes.flags GeometryFlags.Opaque
                )
                |> TraceGeometry.AABBs
                |> AdaptiveTraceInstance.ofGeometry
                |> AdaptiveTraceInstance.geometryAttribute' GeometryAttribute.Colors colors
                |> geometryPool.Add

            let trafo =
                let startTime = System.DateTime.Now

                let position = rnd.UniformV3d(Box3d(V3d(-10.0, -10.0, 2.0), V3d(10.0)))
                let rotation = ((rnd.UniformV3d()) * 2.0 - 1.0) * 2.0 * Constant.Pi

                win.Time |> AVal.map (fun t ->
                    let t = (t - startTime).TotalSeconds
                    Trafo3d.RotationEuler(t * rotation) * Trafo3d.Translation(position)
                )

            let obj =
                traceObject {
                    geometry mti.Geometry
                    customIndex mti.Index
                    hitgroups (HitGroup.Sphere |> List.replicate 6)
                    transform trafo
                    mask 0x80
                }

            obj, mti

    let objects =
        ASet.union staticObjects sphereObjects

    let scene =
        RaytracingScene.ofASet objects

    let pipeline =
        {
            Effect            = Effect.main
            Scenes            = Map.ofList [Sym.ofString "MainScene", scene]
            Uniforms          = uniforms
            MaxRecursionDepth = AVal.constant 2048
        }

    use traceTask = runtime.CompileTraceToTexture(pipeline, traceTexture)

    use fullscreenTask =
        let sg =
            Sg.fullScreenQuad
            |> Sg.diffuseTexture traceTexture
            |> Sg.shader {
                do! DefaultSurfaces.diffuseTexture
            }

        RenderTask.ofList [
            runtime.CompileClear(win.FramebufferSignature, C4f.PaleGreen)
            runtime.CompileRender(win.FramebufferSignature, sg)
        ]

    use renderTask =
        RenderTask.custom (fun (t, rt, fbo, q) ->
            q.Begin()
            traceTask.Run(t, q)
            fullscreenTask.Run(t, rt, fbo, q)
            q.End()
        )

    let mtis = System.Collections.Generic.List()

    win.Keyboard.KeyDown(Keys.Enter).Values.Add(fun _ ->
        transact (fun () ->
            let obj, mti = createSphere()
            mtis.Add(mti)
            sphereObjects.Value <- sphereObjects.Value |> HashSet.add obj
        )
    )

    win.Keyboard.KeyDown(Keys.Delete).Values.Add(fun _ ->
        transact (fun () ->
            let list = sphereObjects.Value |> HashSet.toList
            let idx = rnd.UniformInt(list.Length)
            let set = list |> List.indexed |> List.filter (fst >> (<>) idx) |> List.map snd |> HashSet.ofList
            sphereObjects.Value <- set
        )
    )

    win.RenderTask <- renderTask
    win.Run()

    for mti in mtis do
        mti.Dispose()

    0
﻿open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO
open Aardvark.Application
open Aardvark.Application.Slim
open FSharp.Data.Adaptive

module Semantic =
    let MissShadow = Sym.ofString "MissShadow"
    let HitGroupModel = Sym.ofString "HitGroupModel"
    let HitGroupFloor = Sym.ofString "HitGroupFloor"
    let HitGroupSphere1 = Sym.ofString "HitGroupSphere1"
    let HitGroupSphere2 = Sym.ofString "HitGroupSphere2"
    let HitGroupSphere3 = Sym.ofString "HitGroupSphere3"
    let HitGroupSphere4 = Sym.ofString "HitGroupSphere4"
    let HitGroupSphere5 = Sym.ofString "HitGroupSphere5"
    let HitGroupSphere6 = Sym.ofString "HitGroupSphere6"

module Effect =
    open FShade

    [<AutoOpen>]
    module private Shaders =

        type UniformScope with
            member x.OutputBuffer : Image2d<Formats.rgba32f> = uniform?OutputBuffer
            member x.RecursionDepth : int = uniform?RecursionDepth

            // Model
            //member x.ModelNormals : V4d[] = uniform?StorageBuffer?ModelNormals

            // Floor
            member x.FloorIndices : int[] = uniform?StorageBuffer?FloorIndices
            member x.FloorTextureCoords : V2d[] = uniform?StorageBuffer?FloorTextureCoords

            // Sphere
            member x.SphereOffsets : V3d[] = uniform?StorageBuffer?SphereOffsets

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
        let lightingWithShadow (mask : int32) (reflectiveness : float) (specularAmount : float) (shininess : float) (normal : V3d) (input : RayHitInput<Payload>) =
            let position =
                input.ray.origin + input.hit.t * input.ray.direction

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

        //let chitModel (color : C3d) (input : RayHitInput<Payload>) =
        //    let color = V3d color

        //    closesthit {
        //        let id = input.geometry.primitiveId
        //        let indices = V3i(3 * id, 3 * id + 1, 3 * id + 2)

        //        let normal =
        //            let n0 = uniform.ModelNormals.[indices.X].XYZ
        //            let n1 = uniform.ModelNormals.[indices.Y].XYZ
        //            let n2 = uniform.ModelNormals.[indices.Z].XYZ
        //            input.hit.attribute |> fromBarycentric n0 n1 n2 |> Vec.normalize

        //        let diffuse = lightingWithShadow 0xFF 0.0 1.0 16.0 normal input
        //        return { color = color * diffuse; recursionDepth = 0 }
        //    }

        let chitFloor (input : RayHitInput<Payload>) =
            closesthit {
                let id = input.geometry.primitiveId
                let indices = V3i(uniform.FloorIndices.[3 * id], uniform.FloorIndices.[3 * id + 1], uniform.FloorIndices.[3 * id + 2])

                let texCoords =
                    let uv0 = uniform.FloorTextureCoords.[indices.X]
                    let uv1 = uniform.FloorTextureCoords.[indices.Y]
                    let uv2 = uniform.FloorTextureCoords.[indices.Z]
                    input.hit.attribute |> fromBarycentric2d uv0 uv1 uv2

                let color = textureFloor.Sample(texCoords).XYZ
                let diffuse = lightingWithShadow 0xFF 0.3 0.5 28.0 V3d.ZAxis input
                return { color = color * diffuse; recursionDepth = 0 }
            }

        let chitSphere (color : C3d) (input : RayHitInput<Payload>) =
            let color = V3d color

            closesthit {
                let position = input.ray.origin + input.hit.t * input.ray.direction
                let center = input.objectSpace.objectToWorld.TransformPos uniform.SphereOffsets.[input.geometry.geometryIndex]
                let normal = Vec.normalize (position - center)

                let diffuse = lightingWithShadow 0x7F 0.8 1.0 28.0 normal input
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

    // Note: This is not how you should implement materials.
    // Normally, you'd want to encode the color and radius of your spheres
    // in a buffer and access those via the custom index. Here, we do it this way to
    // test if the shader binding table is computed correctly if an object has mutliple
    // geometries with different hitgroups.
    let private hitgroupSphere1 =
        hitgroup {
            closesthit (chitSphere C3d.BlueViolet)
            intersection (intersectionSphere 0.2)
        }

    let private hitgroupSphere2 =
        hitgroup {
            closesthit (chitSphere C3d.DodgerBlue)
            intersection (intersectionSphere 0.2)
        }

    let private hitgroupSphere3 =
        hitgroup {
            closesthit (chitSphere C3d.HoneyDew)
            intersection (intersectionSphere 0.2)
        }

    let private hitgroupSphere4 =
        hitgroup {
            closesthit (chitSphere C3d.BlanchedAlmond)
            intersection (intersectionSphere 0.2)
        }

    let private hitgroupSphere5 =
        hitgroup {
            closesthit (chitSphere C3d.LimeGreen)
            intersection (intersectionSphere 0.2)
        }

    let private hitgroupSphere6 =
        hitgroup {
            closesthit (chitSphere C3d.MistyRose)
            intersection (intersectionSphere 0.2)
        }

    //let private hitgroupModel =
    //    hitgroup {
    //        closesthit (chitModel C3d.BurlyWood)
    //    }

    let private hitgroupFloor =
        hitgroup {
            closesthit chitFloor
        }


    let main() =
        raytracing {
            raygen rgenMain
            miss (missSolidColor C3d.Lavender)
            miss (Semantic.MissShadow, missShadow)
            //hitgroup (Semantic.HitGroupModel, hitgroupModel)
            hitgroup (Semantic.HitGroupFloor, hitgroupFloor)
            hitgroup (Semantic.HitGroupSphere1, hitgroupSphere1)
            hitgroup (Semantic.HitGroupSphere2, hitgroupSphere2)
            hitgroup (Semantic.HitGroupSphere3, hitgroupSphere3)
            hitgroup (Semantic.HitGroupSphere4, hitgroupSphere4)
            hitgroup (Semantic.HitGroupSphere5, hitgroupSphere5)
            hitgroup (Semantic.HitGroupSphere6, hitgroupSphere6)
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

    //let model, modelNormals =
    //    let scene = Loader.Assimp.load (Path.combine [__SOURCE_DIRECTORY__; "..";"..";"..";"data";"lucy.obj"])
    //    let geometry = scene.meshes.[0].geometry

    //    let normals =
    //        geometry.IndexedAttributes.[DefaultSemantic.Normals] :?> V3f[]
    //        |> Array.map V4f

    //    geometry |> TraceGeometry.ofIndexedGeometry GeometryFlags.Opaque Trafo3d.Identity,
    //    AVal.constant <| ArrayBuffer(normals)

    let floor, floorIndices, floorTextureCoords =
        let positions = [| V3f(-0.5f, -0.5f, 0.0f); V3f(-0.5f, 0.5f, 0.0f); V3f(0.5f, -0.5f, 0.0f); V3f(0.5f, 0.5f, 0.0f); |]
        let indices = [| 0; 1; 2; 3 |]
        let uv = positions |> Array.map (fun p -> p.XY + 0.5f)

        let attributes =
            SymDict.ofList [
                DefaultSemantic.Positions, positions :> System.Array
            ]

        let trafo = Trafo3d.Scale(48.0)
        let geometry =
            IndexedGeometry(IndexedGeometryMode.TriangleStrip, indices, attributes, SymDict.empty)
            |> IndexedGeometry.toNonStripped

        geometry |> TraceGeometry.ofIndexedGeometry GeometryFlags.Opaque trafo,
        AVal.constant <| ArrayBuffer(geometry.IndexArray),
        AVal.constant <| ArrayBuffer(uv)

    let spheres, sphereOffsets =
        let offsets =
            let o1 = V3d(0.0, 0.0, 0.5)
            let o2 = V3d(0.0, 0.0, -0.5)
            let o3 = V3d(0.5, 0.0, 0.0)
            let o4 = V3d(-0.5, 0.0, 0.0)
            let o5 = V3d(0.0, 0.5, 0.0)
            let o6 = V3d(0.0, -0.5, 0.0)
            [| o1; o2; o3; o4; o5; o6 |]

        let geometry =
            offsets |> Array.map (fun offset ->
                TraceGeometry.ofCenterAndRadius GeometryFlags.Opaque offset 0.2
            )
            |> TraceGeometry.ofArray

        let offsetsBuffer =
            ArrayBuffer(offsets |> Array.map V4f) |> AVal.constant

        geometry, offsetsBuffer

    //use accelerationStructureModel =
    //    runtime.CreateAccelerationStructure(
    //        model, AccelerationStructureUsage.Static
    //    )

    use accelerationStructureFloor =
        runtime.CreateAccelerationStructure(
            floor, AccelerationStructureUsage.Static
        )

    use accelerationStructureSphere =
        runtime.CreateAccelerationStructure(
            spheres, AccelerationStructureUsage.Static
        )

    let lightLocation =
        let startTime = System.DateTime.Now
        win.Time |> AVal.map (fun t ->
            let t = (t - startTime).TotalSeconds
            V3d(Rot2d(t * Constant.PiQuarter) * V2d(8.0, 0.0), 16.0)
        )

    use uniforms =
        Map.ofList [
            "OutputBuffer", traceTexture |> AdaptiveResource.mapNonAdaptive (fun t -> t :> ITexture) :> IAdaptiveValue
            "RecursionDepth", AVal.constant 4 :> IAdaptiveValue
            "ViewTrafo", viewTrafo :> IAdaptiveValue
            "ProjTrafo", projTrafo :> IAdaptiveValue
            "CameraLocation", cameraView |> AVal.map CameraView.location :> IAdaptiveValue
            //"ModelNormals", modelNormals :> IAdaptiveValue
            "FloorIndices", floorIndices :> IAdaptiveValue
            "FloorTextureCoords", floorTextureCoords :> IAdaptiveValue
            "TextureFloor", DefaultTextures.checkerboard :> IAdaptiveValue
            "LightLocation", lightLocation :> IAdaptiveValue
            "SphereOffsets", sphereOffsets :> IAdaptiveValue
        ]
        |> UniformProvider.ofMap

    let staticObjects =
        //let objModel =
        //    traceobject {
        //        geometry accelerationStructureModel
        //        hitgroup Semantic.HitGroupModel
        //        culling (CullMode.Enabled WindingOrder.CounterClockwise)
        //    }

        let objFloor =
            traceobject {
                geometry accelerationStructureFloor
                hitgroup Semantic.HitGroupFloor
                culling (CullMode.Enabled WindingOrder.CounterClockwise)
            }

        ASet.ofList [objFloor]
        //ASet.ofList [objModel; objFloor]

    let sphereObjects =
        cset<TraceObject>()

    let createSphere =
        let hitgroups = [
            Semantic.HitGroupSphere1
            Semantic.HitGroupSphere2
            Semantic.HitGroupSphere3
            Semantic.HitGroupSphere4
            Semantic.HitGroupSphere5
            Semantic.HitGroupSphere6
        ]

        fun () ->
            let trafo =
                let startTime = System.DateTime.Now

                let position = rnd.UniformV3d(Box3d(V3d(-10.0, -10.0, 2.0), V3d(10.0)))
                let rotation = ((rnd.UniformV3d()) * 2.0 - 1.0) * 2.0 * Constant.Pi

                win.Time |> AVal.map (fun t ->
                    let t = (t - startTime).TotalSeconds
                    Trafo3d.RotationEuler(t * rotation) * Trafo3d.Translation(position)
                )

            let hitg =
                let shift = rnd.UniformInt(hitgroups.Length)
                hitgroups |> List.permute (fun i -> (i + shift) % hitgroups.Length)

            traceobject {
                geometry accelerationStructureSphere
                hitgroups hitg
                transform trafo
                mask 0x80
            }

    let objects =
        ASet.union staticObjects sphereObjects

    let scene =
        { Objects = objects
          Indices = AMap.empty
          Usage   = AccelerationStructureUsage.Static }

    let pipeline =
        {
            Effect            = Effect.main()
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

    win.Keyboard.KeyDown(Keys.Enter).Values.Add(fun _ ->
        transact (fun () ->
            let obj = createSphere()
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

    0
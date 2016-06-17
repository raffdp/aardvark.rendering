﻿namespace Aardvark.Base.Rendering.Effects

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open FShade
open DefaultSurfaceVertex
 
module PointSurface =

    let internal pointSurface (size : IMod<V2d>) (p : Point<Vertex>) =
            triangle {
                let pos = p.Value.pos
                let pxyz = pos.XYZ / pos.W
                let s = !!size
                //let v = p.Value

                let p00 = V3d(pxyz + V3d( -s.X, -s.Y, 0.0 ))
                let p01 = V3d(pxyz + V3d( -s.X,  s.Y, 0.0 ))
                let p10 = V3d(pxyz + V3d(  s.X, -s.Y, 0.0 ))
                let p11 = V3d(pxyz + V3d(  s.X,  s.Y, 0.0 ))

                yield { p.Value with pos = V4d(p00 * pos.W, pos.W); tc = V2d.OO }
                yield { p.Value with pos = V4d(p10 * pos.W, pos.W); tc = V2d.IO }
                yield { p.Value with pos = V4d(p01 * pos.W, pos.W); tc = V2d.OI }
                yield { p.Value with pos = V4d(p11 * pos.W, pos.W); tc = V2d.II }
            }

    let Effect (size:IMod<V2d>) = 
        toEffect (pointSurface size)
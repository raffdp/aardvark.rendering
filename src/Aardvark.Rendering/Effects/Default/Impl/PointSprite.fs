﻿namespace Aardvark.Rendering.Effects

open Aardvark.Base
open Aardvark.Rendering
open FShade

module PointSprite = 

    let internal pointSprite (p : Point<Vertex>) =
            triangle {
                let s = uniform.PointSize / V2d uniform.ViewportSize
                let pos = p.Value.pos
                let pxyz = pos.XYZ / pos.W

                let p00 = V3d(pxyz + V3d( -s.X, -s.Y, 0.0 ))
                let p01 = V3d(pxyz + V3d( -s.X,  s.Y, 0.0 ))
                let p10 = V3d(pxyz + V3d(  s.X, -s.Y, 0.0 ))
                let p11 = V3d(pxyz + V3d(  s.X,  s.Y, 0.0 ))

                yield { p.Value with pos = V4d(p00 * pos.W, pos.W); tc = V2d.OO; }
                yield { p.Value with pos = V4d(p10 * pos.W, pos.W); tc = V2d.IO; }
                yield { p.Value with pos = V4d(p01 * pos.W, pos.W); tc = V2d.OI; }
                yield { p.Value with pos = V4d(p11 * pos.W, pos.W); tc = V2d.II; }
            }

    let Effect = 
        toEffect pointSprite


﻿namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base

#nowarn "9"
#nowarn "51"

// =======================================================================
// Resource Definition
// =======================================================================
type Buffer =
    class
        inherit Resource<VkBuffer>
        val mutable public Memory : DevicePtr

        member x.Size = x.Memory.Size

        interface IBackendBuffer with
            member x.Handle = x.Handle :> obj

        new(device, handle, memory) = { inherit Resource<_>(device, handle); Memory = memory }
    end


// =======================================================================
// Command Extensions
// =======================================================================
[<AutoOpen>]
module BufferCommands =
    type Command with
        
        // ptr to buffer
        static member Copy(src : DevicePtr, srcOffset : int64, dst : Buffer, dstOffset : int64, size : int64) =
            if size < 0L || srcOffset < 0L || srcOffset + size > src.Size || dstOffset < 0L || dstOffset + size > dst.Size then
                failf "bad copy range"

            let mutable srcBuffer = VkBuffer.Null
            let device = src.Memory.Heap.Device
            { new Command<unit>() with
                member x.Enqueue cmd =
                    let align = device.MinUniformBufferOffsetAlignment

                    let srcOffset = src.Offset + srcOffset
                    let srcBufferOffset = Alignment.prev align srcOffset
                    let srcCopyOffset = srcOffset - srcBufferOffset
                    let srcBufferSize = size + srcCopyOffset

                    let mutable srcInfo =
                        VkBufferCreateInfo(
                            VkStructureType.BufferCreateInfo, 0n,
                            VkBufferCreateFlags.None,
                            uint64 srcBufferSize, VkBufferUsageFlags.TransferSrcBit, VkSharingMode.Exclusive, 
                            0u, NativePtr.zero
                    )

                    VkRaw.vkCreateBuffer(device.Handle, &&srcInfo, NativePtr.zero, &&srcBuffer)
                        |> check "could not create temporary buffer"

                    VkRaw.vkBindBufferMemory(device.Handle, srcBuffer, src.Memory.Handle, uint64 srcBufferOffset)
                        |> check "could not bind temporary buffer memory"

                    let mutable copyInfo = VkBufferCopy(uint64 srcCopyOffset, uint64 dstOffset, uint64 size)
                    VkRaw.vkCmdCopyBuffer(cmd.Handle, srcBuffer, dst.Handle, 1u, &&copyInfo)

                member x.Dispose() =
                    if srcBuffer.IsValid then VkRaw.vkDestroyBuffer(device.Handle, srcBuffer, NativePtr.zero)
            }

        // buffer to ptr
        static member Copy(src : Buffer, srcOffset : int64, dst : DevicePtr, dstOffset : int64, size : int64) =
            if size < 0L || srcOffset < 0L || srcOffset + size > src.Size || dstOffset < 0L || dstOffset + size > dst.Size then
                failf "bad copy range"

            let mutable dstBuffer = VkBuffer.Null
            let device = src.Device
            { new Command<unit>() with
                member x.Enqueue cmd =
                    let align = device.MinUniformBufferOffsetAlignment

                    let dstOffset = dst.Offset + dstOffset
                    let dstBufferOffset = Alignment.prev align dstOffset
                    let dstCopyOffset = dstOffset - dstBufferOffset
                    let dstBufferSize = size + dstCopyOffset


                    let mutable dstInfo =
                        VkBufferCreateInfo(
                            VkStructureType.BufferCreateInfo, 0n,
                            VkBufferCreateFlags.None,
                            uint64 dstBufferSize, VkBufferUsageFlags.TransferDstBit, VkSharingMode.Exclusive, 
                            0u, NativePtr.zero
                    )

                    VkRaw.vkCreateBuffer(device.Handle, &&dstInfo, NativePtr.zero, &&dstBuffer)
                        |> check "could not create temporary buffer"

                    VkRaw.vkBindBufferMemory(device.Handle, dstBuffer, dst.Memory.Handle, uint64 dstBufferOffset)
                        |> check "could not bind temporary buffer memory"


                    let mutable copyInfo = VkBufferCopy(uint64 srcOffset, uint64 dstCopyOffset, uint64 size)
                    VkRaw.vkCmdCopyBuffer(cmd.Handle, src.Handle, dstBuffer, 1u, &&copyInfo)

                member x.Dispose() =
                    if dstBuffer.IsValid then VkRaw.vkDestroyBuffer(device.Handle, dstBuffer, NativePtr.zero)
            }

        // buffer to buffer
        static member Copy(src : Buffer, srcOffset : int64, dst : Buffer, dstOffset : int64, size : int64) =
            if size < 0L || srcOffset < 0L || srcOffset + size > src.Size || dstOffset < 0L || dstOffset + size > dst.Size then
                failf "bad copy range"

            { new Command<unit>() with
                member x.Enqueue cmd =
                    let mutable copyInfo = VkBufferCopy(uint64 srcOffset, uint64 dstOffset, uint64 size)
                    VkRaw.vkCmdCopyBuffer(cmd.Handle, src.Handle, dst.Handle, 1u, &&copyInfo)
                member x.Dispose() =
                    ()
            }


        static member inline Copy(src : DevicePtr, dst : Buffer, size : int64) = 
            Command.Copy(src, 0L, dst, 0L, size)

        static member inline Copy(src : Buffer, dst : DevicePtr, size : int64) = 
            Command.Copy(src, 0L, dst, 0L, size)

        static member inline Copy(src : Buffer, dst : Buffer, size : int64) = 
            Command.Copy(src, 0L, dst, 0L, size)

        static member inline Copy(src : DevicePtr, dst : Buffer) = 
            Command.Copy(src, 0L, dst, 0L, min src.Size dst.Size)

        static member inline Copy(src : Buffer, dst : DevicePtr) = 
            Command.Copy(src, 0L, dst, 0L, min src.Size dst.Size)

        static member inline Copy(src : Buffer, dst : Buffer) = 
            Command.Copy(src, 0L, dst, 0L, min src.Size dst.Size)


// =======================================================================
// Resource functions for Device
// =======================================================================
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Buffer =
    [<AutoOpen>]
    module private Helpers = 
        let memoryTypes (d : Device) (bits : uint32) =
            let mutable mask = 1u
            d.Memories 
            |> Seq.filter (fun m ->
                let mask = 1u <<< m.Info.index
                bits &&& mask <> 0u
               )
            |> Seq.toList

    let ofDevicePtr (flags : VkBufferUsageFlags) (ptr : DevicePtr) (device : Device) =    
        let mutable info =
            VkBufferCreateInfo(
                VkStructureType.BufferCreateInfo, 0n,
                VkBufferCreateFlags.None,
                uint64 ptr.Size, 
                flags,
                VkSharingMode.Exclusive,
                0u, NativePtr.zero
            )

        let mutable handle = VkBuffer.Null
        VkRaw.vkCreateBuffer(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "could not create buffer"

        let mutable reqs = VkMemoryRequirements()
        VkRaw.vkGetBufferMemoryRequirements(device.Handle, handle, &&reqs)

        if ptr.Offset % int64 reqs.alignment <> 0L then
            failf "ill-aligned buffer memory: { offset = %A; align = %A }" ptr.Offset reqs.alignment

        if ptr.Size < int64 reqs.size then
            failf "insufficient buffer-memory size: { is = %A; should = %A }" ptr.Size reqs.size
            
        let heap = 1u <<< ptr.Memory.Heap.Info.index
        if heap &&& reqs.memoryTypeBits = 0u then
            let types = memoryTypes device reqs.memoryTypeBits
            failf "invalid memory-type: %A (expected one of %A)" ptr.Memory.Heap types

        VkRaw.vkBindBufferMemory(device.Handle, handle, ptr.Memory.Handle, uint64 ptr.Offset)
            |> check "could not bind buffer-memory"


        Buffer(device, handle, ptr)
    
    let inline private ofWriter (flags : VkBufferUsageFlags) (size : nativeint) (writer : nativeint -> unit) (device : Device) =
        use token = device.ResourceToken

        let align = int64 device.MinUniformBufferOffsetAlignment

        let deviceAlignedSize = Alignment.next align (int64 size)
        let deviceMem = device.DeviceMemory.Alloc(align, deviceAlignedSize)
        let buffer = device |> ofDevicePtr flags deviceMem
        
        let hostPtr = device.HostMemory.Alloc(align, deviceAlignedSize)
        hostPtr.Mapped (fun dst -> writer dst)

        token.enqueue {
            try do! Command.Copy(hostPtr, 0L, buffer, 0L, int64 size)
            finally hostPtr.Dispose()
        }

        buffer

    let delete (buffer : Buffer) (device : Device) =
        if buffer.Handle.IsValid then
            VkRaw.vkDestroyBuffer(device.Handle, buffer.Handle, NativePtr.zero)
            buffer.Handle <- VkBuffer.Null
            buffer.Memory.Dispose()

    let ofBuffer (flags : VkBufferUsageFlags) (buffer : IBuffer) (device : Device) =
        match buffer with
            | :? ArrayBuffer as ab ->
                let size = nativeint ab.Data.LongLength * nativeint (Marshal.SizeOf ab.ElementType)
                let gc = GCHandle.Alloc(ab.Data, GCHandleType.Pinned)
                try device |> ofWriter flags size (fun dst -> Marshal.Copy(gc.AddrOfPinnedObject(), dst, size))
                finally gc.Free()

            | :? INativeBuffer as nb ->
                let size = nb.SizeInBytes |> nativeint
                nb.Use(fun src ->
                    device |> ofWriter flags size (fun dst -> Marshal.Copy(src, dst, size))
                )

            | :? Buffer as b ->
                b

            | _ ->
                failf "unsupported buffer type %A" buffer


// =======================================================================
// Device Extensions
// =======================================================================
[<AbstractClass; Sealed; Extension>]
type ContextBufferExtensions private() =

    [<Extension>]
    static member inline CreateBuffer(device : Device, flags : VkBufferUsageFlags, ptr : DevicePtr) =
        device |> Buffer.ofDevicePtr flags ptr

    [<Extension>]
    static member inline Delete(device : Device, buffer : Buffer) =
        device |> Buffer.delete buffer

    [<Extension>]
    static member inline CreateBuffer(device : Device, flags : VkBufferUsageFlags, b : IBuffer) =
        device |> Buffer.ofBuffer flags b



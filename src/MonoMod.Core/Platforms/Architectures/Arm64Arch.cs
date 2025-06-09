using MonoMod.Core.Utils;
using MonoMod.Utils;
using System;

namespace MonoMod.Core.Platforms.Architectures
{
    internal sealed class Arm64Arch : IArchitecture
    {
        public ArchitectureKind Target => ArchitectureKind.Arm64;

        public ArchitectureFeature Features => ArchitectureFeature.Immediate64;

        private BytePatternCollection? lazyKnownMethodThunks;
        public BytePatternCollection KnownMethodThunks => Helpers.GetOrInit(ref lazyKnownMethodThunks, CreateKnownMethodThunks);

        public IAltEntryFactory AltEntryFactory => null!;

        private readonly ISystem System;

        public Arm64Arch(ISystem system)
        {
            System = system;
        }

        public NativeDetourInfo ComputeDetourInfo(IntPtr from, IntPtr target, int maxSizeHint)
        {
            // Should work for arm64 as well
            x86Shared.FixSizeHint(ref maxSizeHint);

            if (maxSizeHint < BranchRegisterKind.Instance.Size)
            {
                MMDbgLog.Warning($"Size too small for all known detour kinds! Defaulting to BranchRegister. provided size: {maxSizeHint}");
            }

            return new(from, target, BranchRegisterKind.Instance, null);
        }

        public int GetDetourBytes(NativeDetourInfo info, Span<byte> buffer, out IDisposable? allocationHandle)
        {
            return DetourKindBase.GetDetourBytes(info, buffer, out allocationHandle);
        }

        public NativeDetourInfo ComputeRetargetInfo(NativeDetourInfo detour, IntPtr target, int maxSizeHint = -1)
        {
            // Should work for arm64 as well
            x86Shared.FixSizeHint(ref maxSizeHint);

            if (DetourKindBase.TryFindRetargetInfo(detour, target, maxSizeHint, out var retarget))
            {
                // the detour knows how to retarget itself, we'll use that
                return retarget;
            }

            // the detour doesn't know how to retarget itself, lets just compute a new detour to our new target
            return ComputeDetourInfo(detour.From, target, maxSizeHint);
        }

        public int GetRetargetBytes(NativeDetourInfo original, NativeDetourInfo retarget, Span<byte> buffer,
            out IDisposable? allocationHandle, out bool needsRepatch, out bool disposeOldAlloc)
        {
            return DetourKindBase.DoRetarget(original, retarget, buffer, out allocationHandle, out needsRepatch, out disposeOldAlloc);
        }

        public ReadOnlyMemory<IAllocatedMemory> CreateNativeVtableProxyStubs(IntPtr vtableBase, int vtableSize)
        {
            ReadOnlySpan<byte> stubData = [
                0x00, 0x04, 0x40, 0xF9, // ldr x0, [x0, #8]
                0x08, 0x00, 0x40, 0xF9, // ldr x8, [x0]
                0x8F, 0x00, 0x00, 0x18, // ldr w15, _offset
                0x08, 0x01, 0x0F, 0x8B, // add x8, x8, x15
                0x08, 0x01, 0x40, 0xF9, // ldr x8, [x8]
                0x00, 0x01, 0x1F, 0xD6, // br x8
                0x00, 0x00, 0x00, 0x00, // _offset: .word 0x0
            ];

            return Shared.CreateVtableStubs(System, vtableBase, vtableSize, stubData, 24, true);
        }

        public IAllocatedMemory CreateSpecialEntryStub(IntPtr target, IntPtr argument)
        {
            // CreateNativeExceptionHelper should be implemented first

            throw new NotImplementedException();
        }

        private static BytePatternCollection CreateKnownMethodThunks()
        {
            const byte Bn = BytePattern.BAnyValue;
            const byte Bd = BytePattern.BAddressValue;

            if (PlatformDetection.Runtime is RuntimeKind.Framework or RuntimeKind.CoreCLR)
            {
                return new BytePatternCollection(
                    // BytePatternCollection cannot handle both a partial bit match and extracting the remainder as an address for the same byte.
                    // Unfortunately LDR (immediate) has bits 4-0 as Rt and 23-5 as imm19 and the final address is computed as pc + imm19 * 4.
                    //
                    // Until BytePatternCollection is improved, we can sort of workaround the limitation by the nature of the alignments and
                    // simply ignore the lower 3 bits. However that means the address needs LShift by 3 to compensate. Since we also need to multiply
                    // by 4 for the proper address calculation, that equates to a total LShift of 5. To avoid any problems, the masks are set such
                    // that the ignored 3 bits must always be 0 for the target address ldr instructions. For the other addresses can we can exactly match
                    // on the interested bits because we aren't going to extract them, though allowing them deviate at all is probably silly.
                    //
                    //
                    // .NET 8 Support
                    //
                    // #define STUB_PAGE_SIZE 16384
                    // #define DATA_SLOT(stub, field) (stub##Code + STUB_PAGE_SIZE + stub##Data__##field)
                    //
                    // FixupPrecodeCode
                    new BytePattern(
                        new AddressMeaning(AddressKind.Rel32 | AddressKind.Indirect, 0, 5), mustMatchAtStart: true,
                        new byte[]
                        {
                            0xff, 0x00, 0x00, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0x1f, 0x00, 0x00, 0xff,
                            0x1f, 0x00, 0x00, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                        },
                        new byte[]
                        {
                            0x0b,   Bd,   Bd, 0x58, // ldr x11, DATA_SLOT(FixupPrecode, Target)
                            0x60, 0x01, 0x1f, 0xd6, // br x11
                            0x0c,   Bn,   Bn, 0x58, // ldr x12, DATA_SLOT(FixupPrecode, MethodDesc)
                            0x2b,   Bn,   Bn, 0x58, // ldr x11, DATA_SLOT(FixupPrecode, PrecodeFixupThunk)
                            0x60, 0x01, 0x1f, 0xd6, // br x11
                        }
                    ),
                    // StubPrecodeCode
                    new BytePattern(
                        new AddressMeaning(AddressKind.Rel32 | AddressKind.Indirect, 0, 5), mustMatchAtStart: true,
                        new byte[]
                        {
                            0xff, 0x00, 0x00, 0xff,
                            0x1f, 0x00, 0x00, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                        },
                        new byte[]
                        {
                            0x4a,   Bd,   Bd, 0x58, // ldr x10, DATA_SLOT(StubPrecode, Target)
                            0xec,   Bn,   Bn, 0x58, // ldr x12, DATA_SLOT(StubPrecode, SecretParam)
                            0x40, 0x01, 0x1f, 0xd6, // br x10
                        }
                    ),
                    // CallCountingStubCode
                    new BytePattern(
                        new AddressMeaning(AddressKind.Rel32 | AddressKind.Indirect, 0, 5), mustMatchAtStart: true,
                        new byte[]
                        {
                            0xff, 0x00, 0x00, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0x1f, 0x00, 0x00, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0x1f, 0x00, 0x00, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                        },
                        new byte[]
                        {
                            0x09,   Bd,   Bd, 0x58, // ldr  x9, DATA_SLOT(CallCountingStub, RemainingCallCountCell)
                            0x2a, 0x01, 0x40, 0x79, // ldrh w10, [x9]
                            0x4a, 0x05, 0x00, 0x71, // subs w10, w10, #1
                            0x2a, 0x01, 0x02, 0x79, // strh w10, [x9]
                            0x60, 0x00, 0x00, 0x54, // beq CountReachedZero
                            0xa9,   Bn,   Bn, 0x58, // ldr  x9, DATA_SLOT(CallCountingStub, TargetForMethod)
                            0x20, 0x01, 0x1f, 0xd6, // br   x9
                                                    // CountReachedZero:
                            0xaa,   Bn,   Bn, 0x58, // ldr  x10, DATA_SLOT(CallCountingStub, TargetForThresholdReached)
                            0x40, 0x01, 0x1F, 0xD6, // br   x10
                        }
                    )
                );
            }
            else
            {
                // TODO: Mono
                return new();
            }
        }

        private sealed class BranchRegisterKind : DetourKindBase
        {
            public static readonly BranchRegisterKind Instance = new();

            public override int Size => 4 + 4 + 8;

            public override int GetBytes(IntPtr from, IntPtr to, Span<byte> buffer, object? data, out IDisposable? allocHandle)
            {
                // ldr x8, _target
                buffer[0] = 0x48;
                buffer[1] = 0x00;
                buffer[2] = 0x00;
                buffer[3] = 0x58;
                // br x8
                buffer[4] = 0x00;
                buffer[5] = 0x01;
                buffer[6] = 0x1F;
                buffer[7] = 0xD6;
                // _target: .quad 0x0
                Unsafe.WriteUnaligned(ref buffer[8], (ulong)to);

                allocHandle = null;
                
                MMDbgLog.Trace($"Detouring arm64 from 0x{from:X16} to 0x{to:X16}");

                return Size;
            }

            public override bool TryGetRetargetInfo(NativeDetourInfo orig, IntPtr to, int maxSize, out NativeDetourInfo retargetInfo)
            {
                // we can always trivially retarget an abs64 detour (change the absolute constant)
                retargetInfo = orig with { To = to };
                return true;
            }


            public override int DoRetarget(NativeDetourInfo origInfo, IntPtr to, Span<byte> buffer, object? data,
                out IDisposable? allocationHandle, out bool needsRepatch, out bool disposeOldAlloc)
            {
                needsRepatch = true;
                disposeOldAlloc = true;
                // the retarget logic for rel32 is just the same as the normal patch
                // the patcher should re-patch the target method with the new bytes, and dispose the old allocation, if present
                return GetBytes(origInfo.From, to, buffer, data, out allocationHandle);
            }
        }
    }
}
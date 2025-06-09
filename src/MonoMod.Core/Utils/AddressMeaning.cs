using MonoMod.Logs;
using System;

namespace MonoMod.Core.Utils
{
    /// <summary>
    /// An address meaning for use with a <see cref="BytePattern"/>.
    /// </summary>
    public readonly struct AddressMeaning : IEquatable<AddressMeaning>
    {
        /// <summary>
        /// Gets the <see cref="AddressKind"/> associated with this meaning.
        /// </summary>
        public AddressKind Kind { get; }

        /// <summary>
        /// Gets the offset from the match start that an address is relative to, if it is relative.
        /// </summary>
        public int RelativeToOffset { get; }

        /// <summary>
        /// Gets the shift amount to adjust the address by with left shifts being positive and right shifts negative.
        /// </summary>
        public int Shift { get; }

        /// <summary>
        /// Constructs an <see cref="AddressMeaning"/> for the specified <see cref="AddressKind"/>.
        /// </summary>
        /// <param name="kind">The <see cref="AddressKind"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="kind"/> is invalid -OR- <paramref name="kind"/> is relative.</exception>
        public AddressMeaning(AddressKind kind)
        {
            kind.Validate();
            if (!kind.IsAbsolute())
                throw new ArgumentOutOfRangeException(nameof(kind));
            Kind = kind;
            RelativeToOffset = 0;
        }

        /// <summary>
        /// Constructs an <see cref="AddressMeaning"/> for the specified <see cref="AddressKind"/> and relative offset.
        /// </summary>
        /// <param name="kind">The <see cref="AddressKind"/>.</param>
        /// <param name="relativeOffset">The offset relative to the match start.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="kind"/> is invalid
        /// -OR- <paramref name="kind"/> is absolute
        /// -OR- <paramref name="relativeOffset"/> is less than zero.</exception>
        public AddressMeaning(AddressKind kind, int relativeOffset)
        {
            kind.Validate();
            if (!kind.IsRelative())
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (relativeOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(relativeOffset));
            Kind = kind;
            RelativeToOffset = relativeOffset;
        }

        /// <summary>
        /// Constructs an <see cref="AddressMeaning"/> for the specified <see cref="AddressKind"/>, relative offset and shift amount.
        /// </summary>
        /// <param name="kind">The <see cref="AddressKind"/>.</param>
        /// <param name="relativeOffset">The offset relative to the match start.</param>
        /// <param name="shift">The amount to left shift if positive, or right shift if negative the address by.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="kind"/> is invalid
        /// -OR- <paramref name="kind"/> is absolute
        /// -OR- <paramref name="relativeOffset"/> is less than zero.</exception>
        public AddressMeaning(AddressKind kind, int relativeOffset, int shift)
        {
            kind.Validate();
            if (!kind.IsRelative())
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (relativeOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(relativeOffset));
            Kind = kind;
            RelativeToOffset = relativeOffset;
            Shift = shift;
        }

        private static unsafe nint DoProcessAddress(AddressKind kind, nint basePtr, int offset, ulong address, int shift)
        {
            nint addr;
            if (kind.IsAbsolute())
            {
                addr = (nint)address;
            }
            else
            { // IsRelative
                var offs = kind.Is32Bit()
                    ? Unsafe.As<ulong, int>(ref address)
                    : Unsafe.As<ulong, long>(ref address);

                if (shift < 0)
                {
                    offs >>= -shift;
                }
                else
                {
                    offs <<= shift;
                }

                addr = (nint)(basePtr + offset + offs);
            }
            if (kind.IsIndirect())
            {
                addr = *(nint*)addr;
            }
            return addr;
        }

        /// <summary>
        /// Processes an address according to this <see cref="AddressMeaning"/> into an absolute address.
        /// </summary>
        /// <param name="basePtr">The base pointer of the match.</param>
        /// <param name="offset">The offset from the base pointer that the match occurred at.</param>
        /// <param name="address">The address which was extracted from the match.</param>
        /// <returns>The resolved target address.</returns>
        public nint ProcessAddress(nint basePtr, int offset, ulong address)
        {
            return DoProcessAddress(Kind, basePtr, offset + RelativeToOffset, address, Shift);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is AddressMeaning meaning && Equals(meaning);
        }

        /// <inheritdoc/>
        public bool Equals(AddressMeaning other)
        {
            return Kind == other.Kind &&
                   RelativeToOffset == other.RelativeToOffset &&
                   Shift == other.Shift;
        }

        // Force the use of DebugFormatter, because we might be patching DefaultInterpolatedStringHandler
        /// <inheritdoc/>
        public override string ToString() => DebugFormatter.Format($"AddressMeaning({Kind.FastToString()}, offset: {RelativeToOffset}, shift: {Shift})");

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(Kind, RelativeToOffset, Shift);
        }

        /// <summary>
        /// Compares two <see cref="AddressMeaning"/>s for equality.
        /// </summary>
        /// <param name="left">The first <see cref="AddressMeaning"/> to compare.</param>
        /// <param name="right">The second <see cref="AddressMeaning"/> to compare.</param>
        /// <returns><see langword="true"/> if the two <see cref="AddressMeaning"/>s are equal; <see langword="false"/> otherwise.</returns>
        public static bool operator ==(AddressMeaning left, AddressMeaning right) => left.Equals(right);
        /// <summary>
        /// Compares two <see cref="AddressMeaning"/>s for inequality.
        /// </summary>
        /// <param name="left">The first <see cref="AddressMeaning"/> to compare.</param>
        /// <param name="right">The second <see cref="AddressMeaning"/> to compare.</param>
        /// <returns><see langword="true"/> if the two <see cref="AddressMeaning"/>s are not equal; <see langword="false"/> otherwise.</returns>
        public static bool operator !=(AddressMeaning left, AddressMeaning right) => !(left == right);
    }
}

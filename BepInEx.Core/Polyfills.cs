#if NET35

#pragma warning disable CA1815 // Override equals and operator equals on value types
#pragma warning disable CA2231 // Overload operator equals on overriding ValueType.Equals
#pragma warning disable MA0076 // Do not use implicit culture-sensitive ToString in interpolated strings
#pragma warning disable S3427  // Method overloads with default parameter values should not overlap

// BASEDON: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Index.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System.Runtime.CompilerServices
{
    internal static class MethodImplOptionsEx
    {
        public const MethodImplOptions AggressiveInlining = (MethodImplOptions) 256;

        public const MethodImplOptions ForwardRef = (MethodImplOptions) 16;

        public const MethodImplOptions InternalCall = (MethodImplOptions) 4096;

        public const MethodImplOptions NoInlining = (MethodImplOptions) 8;

        public const MethodImplOptions NoOptimization = (MethodImplOptions) 64;

        public const MethodImplOptions PreserveSig = (MethodImplOptions) 128;

        public const MethodImplOptions SecurityMitigations = (MethodImplOptions) 1024;

        public const MethodImplOptions Synchronized = (MethodImplOptions) 32;

        public const MethodImplOptions Unmanaged = (MethodImplOptions) 4;
    }
}

// ReSharper disable once CheckNamespace
namespace System
{
    /// <summary>Represent a type can be used to index a collection either from the start or the end.</summary>
    /// <remarks>
    /// Index is used by the C# compiler to support the new index syntax
    /// <code>
    /// int[] someArray = new int[5] { 1, 2, 3, 4, 5 } ;
    /// int lastElement = someArray[^1]; // lastElement = 5
    /// </code>
    /// </remarks>
    internal readonly struct Index : IEquatable<Index>
    {
        private readonly int _value;

        /// <summary>Construct an Index using a value and indicating if the index is from the start or from the end.</summary>
        /// <param name="value">The index value. it has to be zero or positive number.</param>
        /// <param name="fromEnd">Indicating if the index is from the start or from the end.</param>
        /// <remarks>
        /// If the Index constructed from the end, index value 1 means pointing at the last element and index value 0 means pointing at beyond last element.
        /// </remarks>
        [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
        public Index(int value, bool fromEnd = false)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Value is negative");

            if (fromEnd)
                _value = ~value;
            else
                _value = value;
        }

        // The following private constructors mainly created for perf reason to avoid the checks
        private Index(int value)
        {
            _value = value;
        }

        /// <summary>Create an Index pointing at beyond last element.</summary>
        public static Index End => new(~0);

        /// <summary>Create an Index pointing at first element.</summary>
        public static Index Start => new(0);

        /// <summary>Indicates whether the index is from the start or the end.</summary>
        public bool IsFromEnd => _value < 0;

        /// <summary>Returns the index value.</summary>
        public int Value => _value < 0 ? ~_value : _value;

        /// <summary>Create an Index from the end at the position indicated by the value.</summary>
        /// <param name="value">The index value from the end.</param>
        [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
        public static Index FromEnd(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Value is negative");

            return new Index(~value);
        }

        /// <summary>Create an Index from the start at the position indicated by the value.</summary>
        /// <param name="value">The index value from the start.</param>
        [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
        public static Index FromStart(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Value is negative");

            return new Index(value);
        }

        /// <summary>Converts integer number to an Index.</summary>
        public static implicit operator Index(int value) => FromStart(value);

        /// <summary>Indicates whether the current Index object is equal to another object of the same type.</summary>
        /// <param name="obj">An object to compare with this object</param>
        public override bool Equals(object? obj) => obj is Index index && _value == index._value;

        /// <summary>Indicates whether the current Index object is equal to another Index object.</summary>
        /// <param name="other">An object to compare with this object</param>
        public bool Equals(Index other) => _value == other._value;

        /// <summary>Returns the hash code for this instance.</summary>
        public override int GetHashCode() => _value;

        /// <summary>Calculate the offset from the start using the giving collection length.</summary>
        /// <param name="length">The length of the collection that the Index will be used with. length has to be a positive value</param>
        /// <remarks>
        /// For performance reason, we don't validate the input length parameter and the returned offset value against negative values.
        /// we don't validate either the returned offset is greater than the input length.
        /// It is expected Index will be used with collections which always have non negative length/count. If the returned offset is negative and
        /// then used to index a collection will get out of range exception which will be same affect as the validation.
        /// </remarks>
        [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
        public int GetOffset(int length)
        {
            var offset = _value;
            if (IsFromEnd)
                // offset = length - (~value)
                // offset = length + (~(~value) + 1)
                // offset = length + value + 1

                offset += length + 1;

            return offset;
        }

        /// <summary>Converts the value of the current Index object to its equivalent string representation.</summary>
        public override string ToString()
        {
            if (IsFromEnd) return ToStringFromEnd();

            return $"{(uint) Value}";
        }

        private string ToStringFromEnd() => $"^{Value}";
    }

    /// <summary>Represent a range has start and end indexes.</summary>
    /// <remarks>
    /// Range is used by the C# compiler to support the range syntax.
    /// <code>
    /// int[] someArray = new int[5] { 1, 2, 3, 4, 5 };
    /// int[] subArray1 = someArray[0..2]; // { 1, 2 }
    /// int[] subArray2 = someArray[1..^0]; // { 2, 3, 4, 5 }
    /// </code>
    /// </remarks>
    internal readonly struct Range : IEquatable<Range>
    {
        /// <summary>Construct a Range object using the start and end indexes.</summary>
        /// <param name="start">Represent the inclusive start index of the range.</param>
        /// <param name="end">Represent the exclusive end index of the range.</param>
        public Range(Index start, Index end)
        {
            Start = start;
            End = end;
        }

        /// <summary>Create a Range object starting from first element to the end.</summary>
        public static Range All => new(Index.Start, Index.End);

        /// <summary>Represent the exclusive end index of the Range.</summary>
        public Index End { get; }

        /// <summary>Represent the inclusive start index of the Range.</summary>
        public Index Start { get; }

        /// <summary>Create a Range object starting from first element in the collection to the end Index.</summary>
        /// <param name="end">The position of the last element up to which the Range object will be created.</param>
        public static Range EndAt(Index end) => new(Index.Start, end);

        /// <summary>Create a Range object starting from start index to the end of the collection.</summary>
        /// <param name="start">Returns a new Range instance starting from a specified start index to the end of the collection.</param>
        public static Range StartAt(Index start) => new(start, Index.End);

        /// <summary>Indicates whether the current Range object is equal to another object of the same type.</summary>
        /// <param name="obj">An object to compare with this object</param>
        public override bool Equals(object? obj) =>
            obj is Range r &&
            r.Start.Equals(Start) &&
            r.End.Equals(End);

        /// <summary>Indicates whether the current Range object is equal to another Range object.</summary>
        /// <param name="other">An object to compare with this object</param>
        public bool Equals(Range other) => other.Start.Equals(Start) && other.End.Equals(End);

        /// <summary>Returns the hash code for this instance.</summary>
        public override int GetHashCode() => Start.GetHashCode() * 31 + End.GetHashCode();

        /// <summary>Calculate the start offset and length of range object using a collection length.</summary>
        /// <param name="length">The length of the collection that the range will be used with. length has to be a positive value.</param>
        /// <remarks>
        /// For performance reason, we don't validate the input length parameter against negative values.
        /// It is expected Range will be used with collections which always have non negative length/count.
        /// We validate the range is inside the length scope though.
        /// </remarks>
        [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
        public (int Offset, int Length) GetOffsetAndLength(int length)
        {
            int start;
            var startIndex = Start;
            if (startIndex.IsFromEnd)
                start = length - startIndex.Value;
            else
                start = startIndex.Value;

            int end;
            var endIndex = End;
            if (endIndex.IsFromEnd)
                end = length - endIndex.Value;
            else
                end = endIndex.Value;

            if ((uint) end > (uint) length || (uint) start > (uint) end)
                throw new ArgumentOutOfRangeException(nameof(length),
                                                      $"end:{end} > length: {length} or start: {start} > end: {end}");

            return (start, end - start);
        }

        /// <summary>Converts the value of the current Range object to its equivalent string representation.</summary>
        public override string ToString() => $"{Start}..{End}";
    }
}

#endif

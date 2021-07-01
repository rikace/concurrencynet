using System;

namespace CSharp.Parallelx.Performance
{
    public static class StringEx
    {
        public static  ReadOnlySpan<char> Slice(this string value, int start, int length)
        {
            return value.AsSpan(start, length);
        }

        public static ReadOnlySpan<char> Trim(ReadOnlySpan<char> source)
        {
            if (source.IsEmpty)
            {
                return source;
            }

            int start = 0, end = source.Length - 1;
            char startChar = source[start], endChar = source[end];

            while ((start < end) && (startChar == ' ' || endChar == ' '))
            {
                if (startChar == ' ')
                {
                    start++;
                }

                if (endChar == ' ')
                {
                    end--;
                }

                startChar = source[start];
                endChar = source[end];
            }

            return source.Slice(start, end - start + 1);
        }

    }
}
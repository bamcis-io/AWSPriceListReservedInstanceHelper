using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper
{
    public static class StreamHelper
    {
        /// <summary>
        /// Finds the position in a stream where the specified byte pattern starts
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public static long IndexOf(this Stream stream, byte[] pattern)
        {
            if (pattern == null || pattern.Length == 0)
            {
                throw new ArgumentException("The pattern cannot be null or empty.");
            }

            if (stream == null || stream.Length == 0)
            {
                throw new ArgumentException("The stream cannot be null or empty.");
            }

            long currentPosition = stream.Position;

            int currentByte = 0;
            int index = 0;

            // Make sure we do the index check first
            // so we don't unnecessarily read an extra byte
            while (index < pattern.Length && (currentByte = stream.ReadByte()) != -1)
            {
                if (currentByte == pattern[index])
                {
                    index++;
                }
                else
                {
                    index = 0;
                }
            }

            try
            {
                // Index got incremented on the last match, so if all matched
                // it will now be 1 more than the last index location, which
                // is the same as the pattern length
                if (index == pattern.Length)
                {
                    // The last read advanced the stream to the position ahead,
                    // so it's 1 further than the last matching byte
                    return stream.Position - pattern.Length ;
                }
                else
                {
                    return -1;
                }
            }
            finally
            {
                // Return stream to original position
                stream.Position = currentPosition;
            }
        }
    }
}

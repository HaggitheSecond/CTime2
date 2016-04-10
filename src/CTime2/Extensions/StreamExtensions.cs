﻿using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace CTime2.Extensions
{
    public static class StreamExtensions
    {
        public static async Task<IRandomAccessStream> ToRandomAccessStreamAsync(this Stream self)
        {
            var result = new InMemoryRandomAccessStream();
            using (var input = self.AsInputStream())
            {
                await RandomAccessStream.CopyAsync(input, result);
            }
            result.Seek(0);

            return result;
        }
    }
}
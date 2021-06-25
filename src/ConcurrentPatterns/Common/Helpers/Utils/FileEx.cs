using System.Net;

namespace Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public static class FileEx
    {
        public static async Task<IEnumerable<string>> ReadAllLinesAsync(string path) =>
            await  System.IO.File.ReadAllLinesAsync(path);

        public static async Task WriteAllTextAsync(string path, string contents)
        {
            byte[] encodedText = Encoding.Unicode.GetBytes(contents);
            await WriteAllBytesAsync(path, encodedText);
        }

        public static async Task WriteAllBytesAsync(string path, byte[] bytes)
        {
            using (var sourceStream = new FileStream(path,
                FileMode.Append, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: true))
            {
                await sourceStream.WriteAsync(bytes, 0, bytes.Length);
            };
        }
    }
}

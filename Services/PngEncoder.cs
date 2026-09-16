using System.IO.Compression;

namespace UABEA.Web.Services
{
    // Encoder PNG mínimo, em C# puro, usando só System.IO.Compression
    // (que já vem no runtime .NET/WASM — sem precisar de ImageSharp ou
    // qualquer outra lib de imagem). Gera PNG de 8 bits por canal, RGBA,
    // sem filtro por linha (filtro "None"), sem interlace. Simples e
    // correto, ainda que não seja o mais compacto possível.
    public static class PngEncoder
    {
        private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static byte[] Encode(byte[] rgba, int width, int height)
        {
            using var output = new MemoryStream();
            output.Write(PngSignature);

            WriteChunk(output, "IHDR", BuildIhdr(width, height));
            WriteChunk(output, "IDAT", BuildIdat(rgba, width, height));
            WriteChunk(output, "IEND", Array.Empty<byte>());

            return output.ToArray();
        }

        private static byte[] BuildIhdr(int width, int height)
        {
            using var ms = new MemoryStream();
            WriteBigEndian(ms, (uint)width);
            WriteBigEndian(ms, (uint)height);
            ms.WriteByte(8);  // bit depth
            ms.WriteByte(6);  // color type 6 = RGBA
            ms.WriteByte(0);  // compression method
            ms.WriteByte(0);  // filter method
            ms.WriteByte(0);  // interlace method
            return ms.ToArray();
        }

        private static byte[] BuildIdat(byte[] rgba, int width, int height)
        {
            // Cada linha de scanline precisa de 1 byte de filtro (0 = None)
            // antes dos pixels, conforme a spec do PNG.
            int stride = width * 4;
            using var raw = new MemoryStream();
            for (int y = 0; y < height; y++)
            {
                raw.WriteByte(0); // filtro "None"
                raw.Write(rgba, y * stride, stride);
            }

            using var compressed = new MemoryStream();
            // PNG exige o formato zlib (com header/adler32), não o
            // deflate cru — por isso a montagem manual do zlib abaixo em
            // vez de usar DeflateStream sozinho.
            compressed.WriteByte(0x78); // CMF: deflate, janela 32K
            compressed.WriteByte(0x9C); // FLG: nível padrão, checksum ok

            using (var deflate = new DeflateStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                raw.Position = 0;
                raw.CopyTo(deflate);
            }

            uint adler = Adler32(raw.ToArray());
            WriteBigEndian(compressed, adler);

            return compressed.ToArray();
        }

        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            WriteBigEndian(output, (uint)data.Length);

            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            output.Write(typeBytes);
            output.Write(data);

            uint crc = Crc32(typeBytes, data);
            WriteBigEndian(output, crc);
        }

        private static void WriteBigEndian(Stream s, uint value)
        {
            s.WriteByte((byte)(value >> 24));
            s.WriteByte((byte)(value >> 16));
            s.WriteByte((byte)(value >> 8));
            s.WriteByte((byte)value);
        }

        private static uint Adler32(byte[] data)
        {
            const uint MOD = 65521;
            uint a = 1, b = 0;
            foreach (byte d in data)
            {
                a = (a + d) % MOD;
                b = (b + a) % MOD;
            }
            return (b << 16) | a;
        }

        private static readonly uint[] CrcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        private static uint Crc32(byte[] typeBytes, byte[] data)
        {
            uint c = 0xFFFFFFFF;
            foreach (byte b in typeBytes)
                c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
            foreach (byte b in data)
                c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFF;
        }
    }
}

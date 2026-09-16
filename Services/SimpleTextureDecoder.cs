namespace UABEA.Web.Services
{
    // Decoder de formatos Texture2D da Unity que NÃO usam compressão em
    // blocos. Reimplementação em C# puro, sem nenhuma dependência nativa
    // (diferente do TexturePlugin original, que usa ispc/crunch/PVRTexLib
    // — bibliotecas C/C++ que não rodam em WebAssembly sem recompilação
    // via Emscripten).
    //
    // Cobertura desta primeira versão: formatos "raw" (um valor de pixel
    // por posição, sem compressão em blocos). Não cobre ETC/ETC2, PVRTC,
    // ASTC, DXT/BC — esses são compressão em blocos e exigiriam
    // implementar o algoritmo de decodificação de cada um (viável, mas é
    // trabalho adicional; ver README-PORT.md).
    //
    // Valores de TextureFormat conferem com o enum UnityEngine.TextureFormat
    // (mesmos números usados pelo UABEA original em TextureHelper.cs).
    public enum SimpleTextureFormat
    {
        Alpha8 = 1,
        RGB24 = 3,
        RGBA32 = 4,
        ARGB32 = 5,
        RGB565 = 7,
        R16 = 9,
        BGRA32 = 10,
        RHalf = 15,
        RGHalf = 16,
        RGBAHalf = 17,
        RFloat = 18,
        RGFloat = 19,
        RGBAFloat = 20,
        RGBA4444 = 42,
        ARGB4444 = 43,
        R8 = 63,
        RG16 = 62,
    }

    public static class SimpleTextureDecoder
    {
        // Formatos que este decoder consegue tratar (sem lib nativa).
        public static bool IsSupported(int unityTextureFormat)
            => Enum.IsDefined(typeof(SimpleTextureFormat), unityTextureFormat);

        // Decodifica os bytes crus da textura para RGBA32 (4 bytes por
        // pixel, ordem R,G,B,A), formato que dá para jogar direto num
        // <canvas> via ImageData no navegador.
        public static byte[] DecodeToRgba32(byte[] data, int width, int height, int unityTextureFormat)
        {
            if (!IsSupported(unityTextureFormat))
                throw new NotSupportedException(
                    $"Formato de textura {unityTextureFormat} não é suportado por este " +
                    "decoder (só formatos não-comprimidos). Formatos comprimidos " +
                    "(ETC/ASTC/PVRTC/DXT/BC) exigem TexToolWrap, que não está portado.");

            var format = (SimpleTextureFormat)unityTextureFormat;
            int pixelCount = width * height;
            byte[] outRgba = new byte[pixelCount * 4];

            switch (format)
            {
                case SimpleTextureFormat.RGBA32:
                    // Já está no formato de saída, 1:1.
                    Array.Copy(data, outRgba, Math.Min(data.Length, outRgba.Length));
                    break;

                case SimpleTextureFormat.ARGB32:
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int si = i * 4, di = i * 4;
                        outRgba[di + 0] = data[si + 1]; // R
                        outRgba[di + 1] = data[si + 2]; // G
                        outRgba[di + 2] = data[si + 3]; // B
                        outRgba[di + 3] = data[si + 0]; // A
                    }
                    break;

                case SimpleTextureFormat.BGRA32:
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int si = i * 4, di = i * 4;
                        outRgba[di + 0] = data[si + 2]; // R
                        outRgba[di + 1] = data[si + 1]; // G
                        outRgba[di + 2] = data[si + 0]; // B
                        outRgba[di + 3] = data[si + 3]; // A
                    }
                    break;

                case SimpleTextureFormat.RGB24:
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int si = i * 3, di = i * 4;
                        outRgba[di + 0] = data[si + 0];
                        outRgba[di + 1] = data[si + 1];
                        outRgba[di + 2] = data[si + 2];
                        outRgba[di + 3] = 255;
                    }
                    break;

                case SimpleTextureFormat.Alpha8:
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int di = i * 4;
                        byte a = data[i];
                        outRgba[di + 0] = 255;
                        outRgba[di + 1] = 255;
                        outRgba[di + 2] = 255;
                        outRgba[di + 3] = a;
                    }
                    break;

                case SimpleTextureFormat.R8:
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int di = i * 4;
                        byte r = data[i];
                        outRgba[di + 0] = r;
                        outRgba[di + 1] = r;
                        outRgba[di + 2] = r;
                        outRgba[di + 3] = 255;
                    }
                    break;

                case SimpleTextureFormat.RG16:
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int si = i * 2, di = i * 4;
                        outRgba[di + 0] = data[si + 0];
                        outRgba[di + 1] = data[si + 1];
                        outRgba[di + 2] = 0;
                        outRgba[di + 3] = 255;
                    }
                    break;

                case SimpleTextureFormat.RGB565:
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int si = i * 2, di = i * 4;
                        ushort v = (ushort)(data[si] | (data[si + 1] << 8));
                        int r5 = (v >> 11) & 0x1F;
                        int g6 = (v >> 5) & 0x3F;
                        int b5 = v & 0x1F;
                        outRgba[di + 0] = (byte)((r5 * 255 + 15) / 31);
                        outRgba[di + 1] = (byte)((g6 * 255 + 31) / 63);
                        outRgba[di + 2] = (byte)((b5 * 255 + 15) / 31);
                        outRgba[di + 3] = 255;
                    }
                    break;

                case SimpleTextureFormat.RGBA4444:
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int si = i * 2, di = i * 4;
                        ushort v = (ushort)(data[si] | (data[si + 1] << 8));
                        int r4 = (v >> 12) & 0xF, g4 = (v >> 8) & 0xF, b4 = (v >> 4) & 0xF, a4 = v & 0xF;
                        outRgba[di + 0] = (byte)(r4 * 17);
                        outRgba[di + 1] = (byte)(g4 * 17);
                        outRgba[di + 2] = (byte)(b4 * 17);
                        outRgba[di + 3] = (byte)(a4 * 17);
                    }
                    break;

                case SimpleTextureFormat.ARGB4444:
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int si = i * 2, di = i * 4;
                        ushort v = (ushort)(data[si] | (data[si + 1] << 8));
                        int a4 = (v >> 12) & 0xF, r4 = (v >> 8) & 0xF, g4 = (v >> 4) & 0xF, b4 = v & 0xF;
                        outRgba[di + 0] = (byte)(r4 * 17);
                        outRgba[di + 1] = (byte)(g4 * 17);
                        outRgba[di + 2] = (byte)(b4 * 17);
                        outRgba[di + 3] = (byte)(a4 * 17);
                    }
                    break;

                case SimpleTextureFormat.R16:
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int si = i * 2, di = i * 4;
                        ushort v = (ushort)(data[si] | (data[si + 1] << 8));
                        byte r = (byte)(v >> 8); // downsample 16->8 bits
                        outRgba[di + 0] = r;
                        outRgba[di + 1] = r;
                        outRgba[di + 2] = r;
                        outRgba[di + 3] = 255;
                    }
                    break;

                case SimpleTextureFormat.RFloat:
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int si = i * 4, di = i * 4;
                        float r = BitConverter.ToSingle(data, si);
                        byte rb = FloatToByte(r);
                        outRgba[di + 0] = rb;
                        outRgba[di + 1] = rb;
                        outRgba[di + 2] = rb;
                        outRgba[di + 3] = 255;
                    }
                    break;

                case SimpleTextureFormat.RGFloat:
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int si = i * 8, di = i * 4;
                        float r = BitConverter.ToSingle(data, si);
                        float g = BitConverter.ToSingle(data, si + 4);
                        outRgba[di + 0] = FloatToByte(r);
                        outRgba[di + 1] = FloatToByte(g);
                        outRgba[di + 2] = 0;
                        outRgba[di + 3] = 255;
                    }
                    break;

                case SimpleTextureFormat.RGBAFloat:
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int si = i * 16, di = i * 4;
                        float r = BitConverter.ToSingle(data, si);
                        float g = BitConverter.ToSingle(data, si + 4);
                        float b = BitConverter.ToSingle(data, si + 8);
                        float a = BitConverter.ToSingle(data, si + 12);
                        outRgba[di + 0] = FloatToByte(r);
                        outRgba[di + 1] = FloatToByte(g);
                        outRgba[di + 2] = FloatToByte(b);
                        outRgba[di + 3] = FloatToByte(a);
                    }
                    break;

                case SimpleTextureFormat.RHalf:
                case SimpleTextureFormat.RGHalf:
                case SimpleTextureFormat.RGBAHalf:
                    // Half-float (16 bits IEEE 754 binary16). .NET tem
                    // System.Half nativo desde o .NET 5+.
                    DecodeHalfFormats(data, outRgba, pixelCount, format);
                    break;

                default:
                    throw new NotSupportedException($"Formato {format} listado como suportado mas sem decodificador implementado.");
            }

            return outRgba;
        }

        private static void DecodeHalfFormats(byte[] data, byte[] outRgba, int pixelCount, SimpleTextureFormat format)
        {
            int channels = format switch
            {
                SimpleTextureFormat.RHalf => 1,
                SimpleTextureFormat.RGHalf => 2,
                SimpleTextureFormat.RGBAHalf => 4,
                _ => 1
            };
            int stride = channels * 2; // 2 bytes por canal (half)

            for (int i = 0; i < pixelCount; i++)
            {
                int si = i * stride, di = i * 4;
                float r = 0, g = 0, b = 0, a = 1f;

                r = ReadHalf(data, si);
                if (channels >= 2) g = ReadHalf(data, si + 2);
                if (channels >= 4)
                {
                    b = ReadHalf(data, si + 4);
                    a = ReadHalf(data, si + 6);
                }
                if (channels == 1) { g = r; b = r; }

                outRgba[di + 0] = FloatToByte(r);
                outRgba[di + 1] = FloatToByte(g);
                outRgba[di + 2] = FloatToByte(b);
                outRgba[di + 3] = FloatToByte(a);
            }
        }

        private static float ReadHalf(byte[] data, int offset)
        {
            ushort bits = (ushort)(data[offset] | (data[offset + 1] << 8));
            return (float)BitConverter.UInt16BitsToHalf(bits);
        }

        private static byte FloatToByte(float v)
        {
            v = Math.Clamp(v, 0f, 1f);
            return (byte)Math.Round(v * 255f);
        }
    }
}

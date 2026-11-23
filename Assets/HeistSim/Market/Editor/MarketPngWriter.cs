using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace HeistSim.Market.Editor
{
    /// <summary>Minimal PNG encoder, mirroring the encoder used by the arena capture.</summary>
    internal static class MarketPngWriter
    {
        private static readonly uint[] CrcTable = BuildCrcTable();

        /// <summary>Encodes a readable texture as an RGB8 PNG.</summary>
        public static byte[] Encode(Texture2D texture)
        {
            var width = texture.width;
            var height = texture.height;
            var pixels = texture.GetPixels32();
            var stride = width * 3 + 1;
            var raw = new byte[stride * height];

            for (var y = 0; y < height; y++)
            {
                var row = height - 1 - y;
                var source = row * width;
                var destination = y * stride;
                raw[destination] = 0;
                for (var x = 0; x < width; x++)
                {
                    var pixel = pixels[source + x];
                    raw[destination + 1 + x * 3] = pixel.r;
                    raw[destination + 2 + x * 3] = pixel.g;
                    raw[destination + 3 + x * 3] = pixel.b;
                }
            }

            using var output = new MemoryStream();
            output.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);
            WriteChunk(output, "IHDR", Header(width, height));
            WriteChunk(output, "IDAT", Zlib(raw));
            WriteChunk(output, "IEND", Array.Empty<byte>());
            return output.ToArray();
        }

        private static byte[] Header(int width, int height)
        {
            var header = new byte[13];
            WriteBigEndian(header, 0, (uint)width);
            WriteBigEndian(header, 4, (uint)height);
            header[8] = 8;
            header[9] = 2;
            header[10] = 0;
            header[11] = 0;
            header[12] = 0;
            return header;
        }

        private static byte[] Zlib(byte[] raw)
        {
            using var compressed = new MemoryStream();
            compressed.WriteByte(0x78);
            compressed.WriteByte(0x9C);
            using (var deflate = new DeflateStream(compressed, System.IO.Compression.CompressionLevel.Optimal, true))
            {
                deflate.Write(raw, 0, raw.Length);
            }

            var adler = Adler32(raw);
            compressed.WriteByte((byte)(adler >> 24));
            compressed.WriteByte((byte)(adler >> 16));
            compressed.WriteByte((byte)(adler >> 8));
            compressed.WriteByte((byte)adler);
            return compressed.ToArray();
        }

        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            var length = new byte[4];
            WriteBigEndian(length, 0, (uint)data.Length);
            output.Write(length, 0, 4);

            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            output.Write(typeBytes, 0, 4);
            output.Write(data, 0, data.Length);

            var crcInput = new byte[4 + data.Length];
            Buffer.BlockCopy(typeBytes, 0, crcInput, 0, 4);
            Buffer.BlockCopy(data, 0, crcInput, 4, data.Length);

            var crc = new byte[4];
            WriteBigEndian(crc, 0, Crc32(crcInput));
            output.Write(crc, 0, 4);
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1;
            uint b = 0;
            foreach (var value in data)
            {
                a = (a + value) % 65521;
                b = (b + a) % 65521;
            }

            return (b << 16) | a;
        }

        private static uint Crc32(byte[] data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var value in data)
            {
                crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
            }

            return crc ^ 0xFFFFFFFFu;
        }

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (var i = 0; i < table.Length; i++)
            {
                var value = (uint)i;
                for (var bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                }

                table[i] = value;
            }

            return table;
        }

        private static void WriteBigEndian(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }
    }
}

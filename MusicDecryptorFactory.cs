using System;
using System.IO;

namespace AudioDecrypt
{
    /// <summary>
    /// 音乐解密工厂
    /// </summary>
    public class MusicDecryptorFactory
    {
        // NCM文件魔术头
        private static readonly byte[] NCM_HEADER = new byte[] { 0x43, 0x54, 0x45, 0x4E, 0x46, 0x44, 0x41, 0x4D, 0x01 };
        // KGM文件魔术头
        private static readonly byte[] KGM_HEADER = new byte[] { 0x7C, 0xD5, 0x32, 0xEB, 0x86, 0x02, 0x7F, 0x4B, 0xA8, 0xAF, 0xA6, 0x8E, 0x0F, 0xFF, 0x99, 0x14 };
        // VPR文件魔术头
        private static readonly byte[] VPR_HEADER = new byte[] { 0x05, 0x28, 0xBC, 0x96, 0xE9, 0xE4, 0x5A, 0x43, 0x91, 0xAA, 0xBD, 0xD0, 0x7A, 0xF5, 0x36, 0x31 };

        /// <summary>
        /// 创建解密器
        /// </summary>
        public static MusicDecryptorBase Create(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("文件不存在", filePath);
            }
            
            byte[] header = new byte[16];
            using (FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                ReadExact(file, header, 0, header.Length);
            }
            
            if (IsNcm(header))
            {
                return new NcmDecryptor();
            }
            else if (IsKgm(header))
            {
                return new KgmDecryptor();
            }
            else if (IsVpr(header))
            {
                return new VprDecryptor();
            }
            else
            {
                throw new Exception($"不支持的文件格式: {filePath}");
            }
        }

        /// <summary>
        /// 判断是否为NCM文件
        /// </summary>
        private static bool IsNcm(byte[] header)
        {
            for (int i = 0; i < NCM_HEADER.Length; i++)
            {
                if (header[i] != NCM_HEADER[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 判断是否为KGM文件
        /// </summary>
        private static bool IsKgm(byte[] header)
        {
            for (int i = 0; i < KGM_HEADER.Length; i++)
            {
                if (header[i] != KGM_HEADER[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 判断是否为VPR文件
        /// </summary>
        private static bool IsVpr(byte[] header)
        {
            for (int i = 0; i < VPR_HEADER.Length; i++)
            {
                if (header[i] != VPR_HEADER[i])
                    return false;
            }
            return true;
        }

        private static void ReadExact(Stream stream, byte[] buffer, int offset, int count)
        {
            int read;
            while (count > 0 && (read = stream.Read(buffer, offset, count)) > 0)
            {
                offset += read;
                count -= read;
            }
            if (count > 0)
                throw new EndOfStreamException("未能读取到足够的数据");
        }
    }
} 
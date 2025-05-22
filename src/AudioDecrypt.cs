using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AudioDecrypt
{
    #region 通用工具类

    /// <summary>
    /// AES加密工具类
    /// </summary>
    public class AesUtil
    {
        /// <summary>
        /// AES ECB模式解密
        /// </summary>
        public static byte[] EcbDecrypt(byte[] cipherText, byte[] key)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.PKCS7;
                
                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
                }
            }
        }
    }

    /// <summary>
    /// Base64编解码工具类
    /// </summary>
    public class Base64Util
    {
        /// <summary>
        /// Base64解码
        /// </summary>
        public static byte[] Decode(byte[] base64Data)
        {
            string base64String = Encoding.UTF8.GetString(base64Data);
            return Convert.FromBase64String(base64String);
        }
    }

    #endregion

    #region 基础类

    /// <summary>
    /// 音乐解密配置
    /// </summary>
    public class MusicDecryptConfig
    {
        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 输出目录
        /// </summary>
        public string OutputDir { get; set; }

        /// <summary>
        /// 是否写入云音乐标识
        /// </summary>
        public bool EnableCloudKey { get; set; } = true;
    }

    /// <summary>
    /// 解密基类
    /// </summary>
    public abstract class MusicDecryptorBase
    {
        /// <summary>
        /// 解密文件
        /// </summary>
        public abstract void Decrypt(MusicDecryptConfig config);
    }

    #endregion
} 
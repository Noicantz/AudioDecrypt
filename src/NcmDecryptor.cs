using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace AudioDecrypt
{
    /// <summary>
    /// 网易云音乐NCM格式解密
    /// </summary>
    public class NcmDecryptor : MusicDecryptorBase
    {
        // NCM文件魔术头
        private static readonly byte[] NCM_HEADER = new byte[] { 0x43, 0x54, 0x45, 0x4E, 0x46, 0x44, 0x41, 0x4D, 0x01 };
        // 核心密钥 - 使用十六进制正确表达
        private static readonly byte[] CORE_KEY = new byte[] { 
            0x68, 0x7A, 0x48, 0x52, 0x41, 0x6D, 0x73, 0x6F, 0x35, 0x6B, 
            0x49, 0x6E, 0x62, 0x61, 0x78, 0x57 
        };
        // 元数据密钥 - 使用十六进制正确表达
        private static readonly byte[] META_KEY = new byte[] { 
            0x23, 0x31, 0x34, 0x6C, 0x6A, 0x6B, 0x5F, 0x21, 0x5C, 0x5D, 
            0x26, 0x30, 0x55, 0x3C, 0x27, 0x28 
        };

        private string _filePath;
        private string _outputFilePath;
        private FileStream _fileStream;
        private bool _enableCloudKey = true;

        // 元数据
        private string _title = "";
        private List<string> _artists = new List<string>();
        private string _fileFormat = "";
        private string _album = "";
        private string _cloudKey = "";
        private byte[] _cover = new byte[0];
        
        // RC4密钥
        private byte[] _rc4Key = new byte[0];

        /// <summary>
        /// 获取解密后的输出文件路径
        /// </summary>
        public string OutputFilePath => _outputFilePath;

        /// <summary>
        /// 解密NCM文件
        /// </summary>
        public override void Decrypt(MusicDecryptConfig config)
        {
            _filePath = config.FilePath;
            _enableCloudKey = config.EnableCloudKey;

            try
            {
                _fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read);

                // 跳过文件头
                JumpHeader();
                
                // 解密RC4密钥
                DecryptRc4Key();
                
                // 解密元数据信息
                DecryptMetaInfo();
                
                // 跳过CRC码
                JumpCrcCode();
                
                // 跳过5字节未知数据
                _fileStream.Seek(5, SeekOrigin.Current);
                
                // 获取封面
                GetCover();
                
                // 生成输出文件路径
                _outputFilePath = GenerateOutputFilePath(config.OutputDir);
                
                // 如果输出文件已存在，先删除
                if (File.Exists(_outputFilePath))
                {
                    File.Delete(_outputFilePath);
                }
                
                // 解密音乐数据
                DecryptMusicData();
                
                // 写入元数据
                // 此部分需要TagLib#库，实际使用时请添加对应的引用
                // WriteMetaInfo();
            }
            finally
            {
                _fileStream?.Close();
            }
        }

        /// <summary>
        /// 跳过文件头
        /// </summary>
        private void JumpHeader()
        {
            _fileStream.Seek(10, SeekOrigin.Begin);
        }

        /// <summary>
        /// 解密RC4密钥
        /// </summary>
        private void DecryptRc4Key()
        {
            byte[] keyData = ReadBlock();
            
            // 异或解密
            for (int i = 0; i < keyData.Length; i++)
            {
                keyData[i] ^= 0x64;
            }
            
            // AES解密
            byte[] rc4Key = AesUtil.EcbDecrypt(keyData, CORE_KEY);
            
            // 去除前17位 (neteasecloudmusic)
            _rc4Key = new byte[rc4Key.Length - 17];
            Array.Copy(rc4Key, 17, _rc4Key, 0, rc4Key.Length - 17);
        }

        /// <summary>
        /// 解密元数据
        /// </summary>
        private void DecryptMetaInfo()
        {
            byte[] metaData = ReadBlock();
            
            // 异或解密
            for (int i = 0; i < metaData.Length; i++)
            {
                metaData[i] ^= 0x63;
            }
            
            // 保存cloudkey
            _cloudKey = Encoding.UTF8.GetString(metaData);
            
            // 去除前22位
            byte[] modifiedData = new byte[metaData.Length - 22];
            Array.Copy(metaData, 22, modifiedData, 0, metaData.Length - 22);
            
            // Base64解码
            byte[] decodedData = Base64Util.Decode(modifiedData);
            
            // AES解密
            byte[] metaInfo = AesUtil.EcbDecrypt(decodedData, META_KEY);
            
            // 去除前6位
            byte[] metaInfoJson = new byte[metaInfo.Length - 6];
            Array.Copy(metaInfo, 6, metaInfoJson, 0, metaInfo.Length - 6);
            
            // 解析JSON
            string json = Encoding.UTF8.GetString(metaInfoJson);
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                JsonElement root = doc.RootElement;
                
                // 音乐名
                if (root.TryGetProperty("musicName", out JsonElement musicNameElem))
                {
                    _title = musicNameElem.GetString();
                }
                
                // 后缀名
                if (root.TryGetProperty("format", out JsonElement formatElem))
                {
                    _fileFormat = formatElem.GetString();
                }
                
                // 歌手
                if (root.TryGetProperty("artist", out JsonElement artistElem) && artistElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement artist in artistElem.EnumerateArray())
                    {
                        _artists.Add(artist[0].GetString());
                    }
                }
                
                // 专辑
                if (root.TryGetProperty("album", out JsonElement albumElem))
                {
                    _album = albumElem.GetString();
                }
            }
        }

        /// <summary>
        /// 跳过CRC码
        /// </summary>
        private void JumpCrcCode()
        {
            _fileStream.Seek(4, SeekOrigin.Current);
        }

        /// <summary>
        /// 获取封面
        /// </summary>
        private void GetCover()
        {
            _cover = ReadBlock();
        }

        /// <summary>
        /// 生成输出文件路径
        /// </summary>
        private string GenerateOutputFilePath(string outputDir)
        {
            string directory = string.IsNullOrEmpty(outputDir) ? Path.GetDirectoryName(_filePath) : outputDir;
            
            // 创建艺术家字符串
            string artistStr = string.Join(",", _artists.Take(3));
            
            // 创建文件名
            string fileName = $"{_title} - {artistStr}.{_fileFormat}";
            
            // 替换非法字符
            fileName = fileName
                .Replace('?', '？')
                .Replace(':', '：')
                .Replace('*', '＊')
                .Replace('"', '＂')
                .Replace('<', '＜')
                .Replace('>', '＞')
                .Replace('|', '｜')
                .Replace('/', '／')
                .Replace('\\', '＼');
            
            return Path.Combine(directory, fileName);
        }

        /// <summary>
        /// 解密音乐数据 - 重新实现，按照NCM的自定义解密算法
        /// </summary>
        private void DecryptMusicData()
        {
            // 生成S盒
            byte[] S = new byte[256];
            
            // 初始化S-box
            for (int i = 0; i < 256; i++)
            {
                S[i] = (byte)i;
            }
            
            // RC4-KSA初始化阶段
            int j = 0;
            for (int i = 0; i < 256; i++)
            {
                j = (j + S[i] + _rc4Key[i % _rc4Key.Length]) & 0xFF;
                byte temp = S[i];
                S[i] = S[j];
                S[j] = temp;
            }
            
            // 创建输出文件
            using (FileStream outputStream = new FileStream(_outputFilePath, FileMode.Create, FileAccess.Write))
            {
                // 读取和解密音乐数据，使用更大的缓冲区提高性能
                byte[] buffer = new byte[512 * 1024]; // 512KB缓冲区
                int bytesRead;
                
                while ((bytesRead = _fileStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    // 使用NCM自定义的解密方法
                    for (int idx = 0; idx < bytesRead; idx++)
                    {
                        int i = (idx + 1) % 256;
                        int k = (S[i] + S[(i + S[i]) % 256]) % 256;
                        buffer[idx] ^= S[k]; // 注意这里不是标准的RC4-PRGA
                    }
                    
                    outputStream.Write(buffer, 0, bytesRead);
                }
            }
            
            // 自动修正音频头
            FixAudioHeader(_outputFilePath);
        }

        /// <summary>
        /// 修正音频头，去除无用前缀，确保文件以fLaC/ID3/MP3帧头开头
        /// </summary>
        private void FixAudioHeader(string filePath)
        {
            byte[] FLAC = Encoding.ASCII.GetBytes("fLaC");
            byte[] ID3 = Encoding.ASCII.GetBytes("ID3");
            // MP3帧头常见开头: 0xFF 0xFB/0xF3/0xF2
            byte[] MP3_1 = new byte[] { 0xFF, 0xFB };
            byte[] MP3_2 = new byte[] { 0xFF, 0xF3 };
            byte[] MP3_3 = new byte[] { 0xFF, 0xF2 };
            const int SCAN_LEN = 4096;
            byte[] data = File.ReadAllBytes(filePath);
            int maxScan = Math.Min(SCAN_LEN, data.Length - 4);
            int found = -1;
            for (int i = 0; i < maxScan; i++)
            {
                if (i + 4 <= data.Length && data[i] == FLAC[0] && data[i + 1] == FLAC[1] && data[i + 2] == FLAC[2] && data[i + 3] == FLAC[3])
                {
                    found = i; break;
                }
                if (i + 3 <= data.Length && data[i] == ID3[0] && data[i + 1] == ID3[1] && data[i + 2] == ID3[2])
                {
                    found = i; break;
                }
                if (i + 2 <= data.Length && data[i] == MP3_1[0] && data[i + 1] == MP3_1[1])
                {
                    found = i; break;
                }
                if (i + 2 <= data.Length && data[i] == MP3_2[0] && data[i + 1] == MP3_2[1])
                {
                    found = i; break;
                }
                if (i + 2 <= data.Length && data[i] == MP3_3[0] && data[i + 1] == MP3_3[1])
                {
                    found = i; break;
                }
            }
            if (found > 0)
            {
                // 截断前面的无用数据
                File.WriteAllBytes(filePath, data.Skip(found).ToArray());
            }
        }

        /// <summary>
        /// 读取数据块
        /// </summary>
        private byte[] ReadBlock()
        {
            byte[] lenBytes = new byte[4];
            ReadExact(_fileStream, lenBytes, 0, 4);
            int length = BitConverter.ToInt32(lenBytes, 0);
            
            byte[] buffer = new byte[length];
            ReadExact(_fileStream, buffer, 0, length);
            
            return buffer;
        }

        private void ReadExact(Stream stream, byte[] buffer, int offset, int count)
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
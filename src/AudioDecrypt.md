# 音乐加密文件解密算法文档

本文档详细介绍了三种常见音乐加密格式（NCM、KGM、VPR）的解密算法原理及实现，以C#代码为例进行说明。

## 目录

- [概述](#概述)
- [文件格式说明](#文件格式说明)
- [NCM格式解密](#ncm格式解密)
- [KGM格式解密](#kgm格式解密)
- [VPR格式解密](#vpr格式解密)
- [工厂模式实现](#工厂模式实现)
- [使用示例](#使用示例)

## 概述

本项目实现了三种常见加密音乐文件格式的解密算法：
- 网易云音乐NCM格式
- 酷狗音乐KGM格式
- 酷狗音乐VPR格式

每种格式都采用特定的加密算法，本文档将详细解释其解密原理和实现方法。

## 文件格式说明

### NCM格式
NCM是网易云音乐使用的加密音乐格式，其基本结构：
- 文件头标识：`43 54 45 4E 46 44 41 4D 01`
- RC4密钥块
- 元数据块（包含标题、作者、专辑信息等）
- CRC码
- 封面图片
- 加密的音频数据

### KGM/KGMA格式
KGM是酷狗音乐使用的加密音乐格式，其基本结构：
- 文件头标识：`7C D5 32 EB 86 02 7F 4B A8 AF A6 8E 0F FF 99 14`
- 头部长度信息
- 解密密钥
- 加密的音频数据

### VPR格式
VPR也是酷狗音乐使用的加密格式，与KGM类似，但使用不同的文件头和额外的解密步骤：
- 文件头标识：`05 28 BC 96 E9 E4 5A 43 91 AA BD D0 7A F5 36 31`
- 头部长度信息
- 解密密钥
- 加密的音频数据

## NCM格式解密

NCM格式的解密流程包括以下步骤：

### 1. 读取并解密RC4密钥
```csharp
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
    
    // 去除前17位
    _rc4Key = new byte[rc4Key.Length - 17];
    Array.Copy(rc4Key, 17, _rc4Key, 0, rc4Key.Length - 17);
}
```

### 2. 解密元数据
```csharp
private void DecryptMetaInfo()
{
    byte[] metaData = ReadBlock();
    
    // 异或解密
    for (int i = 0; i < metaData.Length; i++)
    {
        metaData[i] ^= 0x63;
    }
    
    // 获取cloudkey并处理元数据
    _cloudKey = Encoding.UTF8.GetString(metaData);
    byte[] modifiedData = new byte[metaData.Length - 22];
    Array.Copy(metaData, 22, modifiedData, 0, metaData.Length - 22);
    
    // Base64解码
    byte[] decodedData = Base64Util.Decode(modifiedData);
    
    // AES解密
    byte[] metaInfo = AesUtil.EcbDecrypt(decodedData, META_KEY);
    
    // 解析JSON元数据
    // ...解析歌曲名称、作者、专辑等信息
}
```

### 3. 解密音频数据
NCM使用RC4变种算法解密音频数据:

```csharp
private void DecryptMusicData()
{
    // 初始化S-box
    byte[] S = new byte[256];
    byte[] K = new byte[256];
    
    for (int i = 0; i < 256; i++)
    {
        S[i] = (byte)i;
    }
    
    // 初始替换
    int j = 0;
    for (int i = 0; i < 256; i++)
    {
        j = (j + S[i] + _rc4Key[i % _rc4Key.Length]) & 0xFF;
        byte temp = S[i];
        S[i] = S[j];
        S[j] = temp;
    }
    
    // 生成密钥流
    for (int i = 0; i < 256; i++)
    {
        int a = (i + 1) & 0xFF;
        int b = (S[a] + S[(a + S[a]) & 0xFF]) & 0xFF;
        K[i] = (byte)(S[(S[a] + b) & 0xFF] & 0xFF);
    }
    
    // 解密并写入文件
    using (FileStream outputStream = new FileStream(_outputFilePath, FileMode.Create, FileAccess.Write))
    {
        byte[] buffer = new byte[256];
        int bytesRead;
        
        while ((bytesRead = _fileStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
            {
                buffer[i] ^= K[i];  // 使用密钥流进行异或解密
            }
            
            outputStream.Write(buffer, 0, bytesRead);
        }
    }
}
```

## KGM格式解密

KGM格式的解密主要依赖特定的解密表和算法：

### 1. 核心解密逻辑
```csharp
public override void Decrypt(MusicDecryptConfig config)
{
    // ... 初始化工作

    // 正式解密
    _fileStream.Seek(_headerLen, SeekOrigin.Begin);
    long pos = 0, offset = 0;
    byte med8, msk8;
    byte[] buffer = new byte[4096];
    int bytesRead;
    
    while ((bytesRead = _fileStream.Read(buffer, 0, buffer.Length)) > 0)
    {
        for (int i = 0; i < bytesRead; i++)
        {
            // 第一步：密钥异或
            med8 = (byte)((_key[pos % 17]) ^ buffer[i]);
            med8 ^= (byte)((med8 & 15) << 4);
            
            // 第二步：掩码计算
            msk8 = 0;
            offset = pos >> 4;
            
            while (offset >= 0x11)
            {
                msk8 ^= TABLE1[offset % 272];
                offset >>= 4;
                msk8 ^= TABLE2[offset % 272];
                offset >>= 4;
            }
            
            // 第三步：应用预定义掩码
            msk8 = (byte)(MASK_V2_PRE_DEF[pos % 272] ^ msk8);
            msk8 ^= (byte)((msk8 & 15) << 4);
            
            // 最终异或得到解密数据
            buffer[i] = (byte)(med8 ^ msk8);
            pos++;
        }
        
        _outputFileStream.Write(buffer, 0, bytesRead);
    }
    
    // ... 结束工作
}
```

### 2. 关键数据表
KGM解密使用了三个重要的数据表:
- `TABLE1`：第一级偏移计算表
- `TABLE2`：第二级偏移计算表
- `MASK_V2_PRE_DEF`：预定义掩码表

这些表是解密过程的核心，通过特定索引计算和异或操作实现解密。

## VPR格式解密

VPR格式的解密与KGM基本相同，但多了一步额外的异或操作：

```csharp
// 与KGM相同的解密步骤...

// VPR格式特有的额外异或操作
buffer[i] ^= VPR_MASK_DIFF[pos % 17];
```

其中`VPR_MASK_DIFF`是特定于VPR格式的掩码差异数组：
```csharp
protected static readonly byte[] VPR_MASK_DIFF = new byte[] { 
    0x25, 0xDF, 0xE8, 0xA6, 0x75, 0x1E, 0x75, 0x0E, 
    0x2F, 0x80, 0xF3, 0x2D, 0xB8, 0xB6, 0xE3, 0x11, 0x00 
};
```

## 工厂模式实现

本项目使用工厂模式根据文件头自动选择适合的解密器：

```csharp
public static MusicDecryptorBase Create(string filePath)
{
    if (!File.Exists(filePath))
    {
        throw new FileNotFoundException("文件不存在", filePath);
    }
    
    byte[] header = new byte[16];
    using (FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
    {
        file.Read(header, 0, header.Length);
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
```

## 使用示例

以下是如何使用解密库的简单示例：

```csharp
// 创建解密配置
MusicDecryptConfig config = new MusicDecryptConfig
{
    FilePath = "加密文件路径.ncm",  // 或 .kgm 或 .kgma
    OutputDir = "输出目录路径",
    EnableCloudKey = true  // 是否保留网易云ID信息
};

// 创建解密器
MusicDecryptorBase decryptor = MusicDecryptorFactory.Create(config.FilePath);

// 执行解密
decryptor.Decrypt(config);
```

## 注意事项

1. 本解密库处理元数据时省略了TagLib#库的依赖，实际应用中应添加此依赖以正确处理音频标签。
2. 解密后的文件可能为MP3或FLAC格式，程序会自动判断并设置正确的扩展名。
3. 解密过程中的异常（如不支持的文件格式、文件损坏等）会被适当处理和报告。 
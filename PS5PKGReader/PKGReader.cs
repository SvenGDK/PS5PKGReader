using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using static PS5PKGReader.MainWindow;
using static PS5PKGReader.PS5ParamClass;

namespace PS5PKGReader
{
    public class PKGReader
    {
        public class PS5PKG
        {
            static readonly byte[] PNGSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
            static readonly byte[] IENDSequence = [0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82];

            public enum PKGType
            {
                Meta,
                FullRetail,
                FullDebug
            }

            public class PKGPNGImageEntry()
            {
                public string? FileName { get; set; }
                public int? Width { get; set; }
                public int? Height { get; set; }
                public byte[]? FileData { get; set; }
            }

            public static PKGType? GetPKGType(string sourceFile)
            {
                using var NewBinaryReader = new BinaryReader(new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read));
                string magic = Encoding.ASCII.GetString(NewBinaryReader.ReadBytes(4));
                if (magic.Length >= 4 && magic[1..] == "CNT")
                {
                    return PKGType.Meta;
                }
                else if (magic.Length >= 4 && magic[1..] == "FIH")
                {
                    NewBinaryReader.BaseStream.Seek(5, SeekOrigin.Begin);
                    var SignedByte = NewBinaryReader.ReadByte();
                    uint SignedValue = SignedByte;
                    if (SignedValue == 128)
                    {
                        return PKGType.FullRetail;
                    }
                    else if (SignedValue == 0)
                    {
                        return PKGType.FullDebug;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }

            public static PS5Param? GetRetailPKGParamJSON(string pkgFilePath)
            {
                var PKGParamBytes = GetRetailPKGParamBytes(pkgFilePath);
                if (PKGParamBytes != null)
                {
                    string FinalParamJSONString = "";
                    using (var ParamJSONFileStream = new FileStream(pkgFilePath, FileMode.Open, FileAccess.Read))
                    {
                        var NewUTF8Encoding = new UTF8Encoding(false, false);
                        string ExtractedData = NewUTF8Encoding.GetString(PKGParamBytes);
                        List<string> ParamJSONData = [.. ExtractedData.Split(["\r\n"], StringSplitOptions.None)];

                        if (PKGParamBytes.Length > 5000) // Occurs in PKGs that have multiple param.json - get only 1
                        {
                            // Pick only the first param.json
                            string versionFileUriMark = "versionFileUri";
                            int versionFileUriLine = ParamJSONData.FindIndex(line => line != null && line.Contains(versionFileUriMark, StringComparison.OrdinalIgnoreCase));
                            if (versionFileUriLine >= 0)
                            {
                                int ClosingLineIndex = ParamJSONData.FindIndex(versionFileUriLine, line => line != null && line.Trim() == "}");
                                ParamJSONData = [.. ParamJSONData.Take(ClosingLineIndex + 1)];
                            }
                        }

                        if (PKGParamBytes.Length > 5000)
                        {
                            //Adjust the output
                            ParamJSONData.RemoveAt(0);
                            ParamJSONData.Insert(0, "{");
                            ParamJSONData[^1] += "\"";

                            // Additional cleanup for previously cutted param.json
                            string versionFileUriMark = "versionFileUri";
                            int RecheckedversionFileUriLine = ParamJSONData.FindIndex(line => line != null && line.Contains(versionFileUriMark, StringComparison.OrdinalIgnoreCase));
                            if (RecheckedversionFileUriLine >= 0)
                            {
                                int ClosingLineIndex = ParamJSONData.FindIndex(RecheckedversionFileUriLine, line => line != null && line.Trim() == "}");
                                ParamJSONData[RecheckedversionFileUriLine] = ParamJSONData[RecheckedversionFileUriLine].Replace("\"\"", "\"");
                            }
                        }
                        else
                        {
                            //Adjust the output
                            ParamJSONData.RemoveAt(0);
                            ParamJSONData.Insert(0, "{");
                            ParamJSONData[^1] += "\"";
                            ParamJSONData.Add("}");
                        }

                        FinalParamJSONString = string.Join("\n", ParamJSONData);
                    }
                    if (!string.IsNullOrEmpty(FinalParamJSONString))
                    {
                        // Extra JSON cleanup
                        FinalParamJSONString = FinalParamJSONString.Trim();
                        int lastBrace = FinalParamJSONString.LastIndexOf('}');
                        if (lastBrace > 0)
                            FinalParamJSONString = FinalParamJSONString[..(lastBrace + 1)].Trim();

                        // Return PS5Param
                        try
                        {
                            var ParamData = JsonConvert.DeserializeObject<PS5Param>(FinalParamJSONString);
                            if (ParamData is not null) { return ParamData; }
                            else { return null; }
                        }
                        catch { return null; }
                    }
                    else { return null; }
                }
                else { return null; }
                // A problem here is that the found offsets values are around the actual param.json because the PKG is not propperly read. However, with some fixes it will actually parse.
            }

            public static (PS5Param? ParamFile, XDocument? CurrentConfigurationXML, byte[]? PKGIcon, byte[]? PKGBackground, byte[]? PKGSoundtrack)? GetDebugPKGFiles(string pkgFilePath)
            {
                string ExtractedPKGConfigurationData = "";
                string PKGConfigurationStartString = "<package-configuration version=\"1.0\" type=\"package-info\">";
                string PKGConfigurationEndString = "</package-configuration>";

                byte[] PKGConfigurationStartBytes = Encoding.UTF8.GetBytes(PKGConfigurationStartString);
                byte[] PKGConfigurationEndBytes = Encoding.UTF8.GetBytes(PKGConfigurationEndString);

                long PKGConfigurationStartOffset = -1;
                long PKGConfigurationEndOffset = -1;

                // Return values
                XDocument? CurrentConfigurationXML = null;
                PS5Param? CurrentParamJSON = null;
                byte[]? CurrentIcon0 = null;
                byte[]? CurrentPic0 = null;
                byte[]? CurrentSnd0 = null;

                // Get the PKG configuration
                using (var PKGReader = new FileStream(pkgFilePath, FileMode.Open, FileAccess.Read))
                {
                    var buffer = new byte[4097];
                    long fileLength = PKGReader.Length;
                    long totalBytesRead = fileLength;

                    // Read backwards to find the end string first
                    while (totalBytesRead > 0)
                    {
                        int bytesRead = (int)Math.Min(buffer.Length, totalBytesRead);
                        PKGReader.Seek(totalBytesRead - bytesRead, SeekOrigin.Begin);
                        PKGReader.ReadExactly(buffer, 0, bytesRead);
                        totalBytesRead -= bytesRead;

                        // Check buffer from end to start
                        bool exitWhile1 = false;
                        for (int i = bytesRead - 1; i >= 0; i -= 1)
                        {
                            // Check for end string
                            if (PKGConfigurationEndOffset == -1 && MatchBytes(buffer, i, PKGConfigurationEndBytes))
                            {
                                PKGConfigurationEndOffset = totalBytesRead + i + PKGConfigurationEndBytes.Length;
                            }

                            // Check for start string
                            if (PKGConfigurationStartOffset == -1 && MatchBytes(buffer, i, PKGConfigurationStartBytes))
                            {
                                PKGConfigurationStartOffset = totalBytesRead + i;
                            }

                            if (PKGConfigurationStartOffset != -1 && PKGConfigurationEndOffset != -1)
                            {
                                exitWhile1 = true;
                                break;
                            }
                        }

                        if (exitWhile1)
                        {
                            break;
                        }
                    }

                    if (PKGConfigurationStartOffset != -1 & PKGConfigurationEndOffset != -1 && PKGConfigurationEndOffset > PKGConfigurationStartOffset)
                    {
                        PKGConfigurationStartOffset -= PKGConfigurationStartBytes.Length;
                        PKGConfigurationEndOffset -= PKGConfigurationEndBytes.Length - 1;

                        // Calculate the size of the pkg configuration data
                        long PKGConfigurationDataSize = PKGConfigurationEndOffset - PKGConfigurationStartOffset;

                        // Move to the start offset
                        PKGReader.Seek(PKGConfigurationStartOffset, SeekOrigin.Begin);

                        // Read the pkg configuration data
                        var data = new byte[((int)PKGConfigurationDataSize)];
                        PKGReader.ReadExactly(data, 0, (int)PKGConfigurationDataSize);

                        // Convert the data to a XML string
                        ExtractedPKGConfigurationData = Encoding.UTF8.GetString(data);
                        ExtractedPKGConfigurationData = string.Concat("<?xml version=\"1.0\" encoding=\"utf-8\"?>", ExtractedPKGConfigurationData);
                    }
                }

                // Process the retrieved PKG configuration data
                if (!string.IsNullOrEmpty(ExtractedPKGConfigurationData))
                {
                    // Load the XML file
                    XDocument PKGConfigurationXML = XDocument.Parse(ExtractedPKGConfigurationData);
                    CurrentConfigurationXML = PKGConfigurationXML;
                    if (PKGConfigurationXML != null)
                    {
                        // Get the PKG Mount Image Container Offset
                        string PKGMountImageContainerOffset = PKGConfigurationXML.Descendants("mount-image").First().Element("container-offset")!.Value;
                        using var PKGReader = new FileStream(pkgFilePath, FileMode.Open, FileAccess.Read);
                        PKGReader.Seek(0, SeekOrigin.End);

                        long ContainerOffsetDecValue = 0;
                        long EntryOffsetDecValue = 0;
                        int EntrySizeDecValue = 0;

                        long ParamJSONOffsetPosition = 0;
                        long Icon0OffsetPosition = 0;
                        long Pic0OffsetPosition = 0;
                        long Snd0OffsetPosition = 0;

                        var ParamJsonPKGEntry = new PS5PKGEntry();
                        var Icon0PKGEntry = new PS5PKGEntry();
                        var Pic0PKGEntry = new PS5PKGEntry();
                        var Snd0PKGEntry = new PS5PKGEntry();

                        // Get the param.json, icon0.png, pic0.png & snd0.at9 PKG entry info
                        foreach (XElement PKGEntry in PKGConfigurationXML.Descendants("entries").Descendants("entry"))
                        {
                            if (PKGEntry != null)
                            {
                                if (PKGEntry.Attribute("name")!.Value == "param.json")
                                {
                                    ParamJsonPKGEntry.EntryOffset = PKGEntry.Attribute("offset")!.Value;
                                    ParamJsonPKGEntry.EntrySize = PKGEntry.Attribute("size")!.Value;
                                    ParamJsonPKGEntry.EntryName = PKGEntry.Attribute("name")!.Value;
                                }
                                if (PKGEntry.Attribute("name")!.Value == "icon0.png")
                                {
                                    Icon0PKGEntry.EntryOffset = PKGEntry.Attribute("offset")!.Value;
                                    Icon0PKGEntry.EntrySize = PKGEntry.Attribute("size")!.Value;
                                    Icon0PKGEntry.EntryName = PKGEntry.Attribute("name")!.Value;
                                }
                                if (PKGEntry.Attribute("name")!.Value == "pic0.png")
                                {
                                    Pic0PKGEntry.EntryOffset = PKGEntry.Attribute("offset")!.Value;
                                    Pic0PKGEntry.EntrySize = PKGEntry.Attribute("size")!.Value;
                                    Pic0PKGEntry.EntryName = PKGEntry.Attribute("name")!.Value;
                                }
                                if (PKGEntry.Attribute("name")!.Value == "snd0.at9")
                                {
                                    Snd0PKGEntry.EntryOffset = PKGEntry.Attribute("offset")!.Value;
                                    Snd0PKGEntry.EntrySize = PKGEntry.Attribute("size")!.Value;
                                    Snd0PKGEntry.EntryName = PKGEntry.Attribute("name")!.Value;
                                }
                            }
                        }

                        // param.json
                        if (!string.IsNullOrEmpty(ParamJsonPKGEntry.EntryOffset) && !string.IsNullOrEmpty(ParamJsonPKGEntry.EntrySize))
                        {
                            // Get decimal offset values
                            if (!string.IsNullOrEmpty(PKGMountImageContainerOffset))
                            {
                                ContainerOffsetDecValue = Convert.ToInt64(PKGMountImageContainerOffset, 16);
                            }
                            if (!string.IsNullOrEmpty(ParamJsonPKGEntry.EntryOffset))
                            {
                                EntryOffsetDecValue = Convert.ToInt64(ParamJsonPKGEntry.EntryOffset, 16);
                            }
                            if (!string.IsNullOrEmpty(ParamJsonPKGEntry.EntrySize))
                            {
                                EntrySizeDecValue = Convert.ToInt32(ParamJsonPKGEntry.EntrySize, 16);
                            }
                            ParamJSONOffsetPosition = ContainerOffsetDecValue + EntryOffsetDecValue;

                            // Seek to the beginning of the param.json file and read
                            byte[] ParamFileBuffer = new byte[EntrySizeDecValue];
                            PKGReader.Seek(ParamJSONOffsetPosition, SeekOrigin.Begin);
                            PKGReader.ReadExactly(ParamFileBuffer);

                            if (!string.IsNullOrWhiteSpace(Encoding.UTF8.GetString(ParamFileBuffer)))
                            {
                                var ParamData = JsonConvert.DeserializeObject<PS5ParamClass.PS5Param>(Encoding.UTF8.GetString(ParamFileBuffer));
                                if (ParamData is not null)
                                {
                                    CurrentParamJSON = ParamData;
                                }
                            }
                        }

                        // icon0.png
                        if (!string.IsNullOrEmpty(Icon0PKGEntry.EntryOffset) && !string.IsNullOrEmpty(Icon0PKGEntry.EntrySize))
                        {
                            if (!string.IsNullOrEmpty(PKGMountImageContainerOffset))
                            {
                                ContainerOffsetDecValue = Convert.ToInt64(PKGMountImageContainerOffset, 16);
                            }
                            if (!string.IsNullOrEmpty(Icon0PKGEntry.EntryOffset))
                            {
                                EntryOffsetDecValue = Convert.ToInt64(Icon0PKGEntry.EntryOffset, 16);
                            }
                            if (!string.IsNullOrEmpty(Icon0PKGEntry.EntrySize))
                            {
                                EntrySizeDecValue = Convert.ToInt32(Icon0PKGEntry.EntrySize, 16);
                            }
                            Icon0OffsetPosition = ContainerOffsetDecValue + EntryOffsetDecValue;

                            byte[] Icon0FileBuffer = new byte[EntrySizeDecValue];
                            PKGReader.Seek(Icon0OffsetPosition, SeekOrigin.Begin);
                            PKGReader.ReadExactly(Icon0FileBuffer);

                            if (Icon0FileBuffer is not null)
                            {
                                CurrentIcon0 = Icon0FileBuffer;
                            }
                        }

                        // pic0.png
                        if (!string.IsNullOrEmpty(Pic0PKGEntry.EntryOffset) && !string.IsNullOrEmpty(Pic0PKGEntry.EntrySize))
                        {
                            if (!string.IsNullOrEmpty(PKGMountImageContainerOffset))
                            {
                                ContainerOffsetDecValue = Convert.ToInt64(PKGMountImageContainerOffset, 16);
                            }
                            if (!string.IsNullOrEmpty(Pic0PKGEntry.EntryOffset))
                            {
                                EntryOffsetDecValue = Convert.ToInt64(Pic0PKGEntry.EntryOffset, 16);
                            }
                            if (!string.IsNullOrEmpty(Pic0PKGEntry.EntrySize))
                            {
                                EntrySizeDecValue = Convert.ToInt32(Pic0PKGEntry.EntrySize, 16);
                            }
                            Pic0OffsetPosition = ContainerOffsetDecValue + EntryOffsetDecValue;

                            byte[] Pic0FileBuffer = new byte[EntrySizeDecValue];
                            PKGReader.Seek(Pic0OffsetPosition, SeekOrigin.Begin);
                            PKGReader.ReadExactly(Pic0FileBuffer);

                            if (Pic0FileBuffer is not null)
                            {
                                CurrentPic0 = Pic0FileBuffer;
                            }
                        }

                        // snd0.at9
                        if (!string.IsNullOrEmpty(Snd0PKGEntry.EntryOffset) && !string.IsNullOrEmpty(Snd0PKGEntry.EntrySize))
                        {
                            if (!string.IsNullOrEmpty(PKGMountImageContainerOffset))
                            {
                                ContainerOffsetDecValue = Convert.ToInt64(PKGMountImageContainerOffset, 16);
                            }
                            if (!string.IsNullOrEmpty(Snd0PKGEntry.EntryOffset))
                            {
                                EntryOffsetDecValue = Convert.ToInt64(Snd0PKGEntry.EntryOffset, 16);
                            }
                            if (!string.IsNullOrEmpty(Snd0PKGEntry.EntrySize))
                            {
                                EntrySizeDecValue = Convert.ToInt32(Snd0PKGEntry.EntrySize, 16);
                            }
                            Snd0OffsetPosition = ContainerOffsetDecValue + EntryOffsetDecValue;

                            byte[] Snd0FileBuffer = new byte[EntrySizeDecValue];
                            PKGReader.Seek(Snd0OffsetPosition, SeekOrigin.Begin);
                            PKGReader.ReadExactly(Snd0FileBuffer);

                            if (Snd0FileBuffer is not null)
                            {
                                CurrentSnd0 = Snd0FileBuffer;
                            }
                        }

                        PKGReader.Close();
                    }
                }
                else
                {
                    return (null, null, null, null, null);
                }

                return (CurrentParamJSON, CurrentConfigurationXML, CurrentIcon0, CurrentPic0, CurrentSnd0);
            }

            public static byte[] GetRetailPKGParamBytes(string pkgFilePath)
            {
                var pkgFile = new FileInfo(pkgFilePath);
                byte[] startBytes = Encoding.UTF8.GetBytes(".json");
                byte[] endBytes = Encoding.UTF8.GetBytes("version.xml");

                const int defaultBufferSize = 1 * 1024 * 1024; // 1 MB, tune between 256KB..4MB
                int bufferSize = defaultBufferSize;

                int maxPattern = Math.Max(startBytes.Length, endBytes.Length);
                int overlap = maxPattern - 1;

                byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                byte[] prevTail = ArrayPool<byte>.Shared.Rent(overlap);
                int prevTailLen = 0;

                try
                {
                    long fileLength = pkgFile.Length;
                    long offsetFromEnd = 0;
                    long startOffset = -1;
                    long endOffset = -1;

                    // Ensure PKG is correct and ends with zeroes
                    const int bytesToCheck = 10;
                    if (fileLength == 0) return [];
                    int PKGCheck = (int)Math.Min(bytesToCheck, fileLength);
                    byte[] tail = ArrayPool<byte>.Shared.Rent(PKGCheck);
                    try
                    {
                        using var fsCheck = new FileStream(pkgFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess);
                        fsCheck.Position = fileLength - PKGCheck;
                        int read = ReadFull(fsCheck, tail, 0, PKGCheck);
                        if (read != PKGCheck) return []; // unexpected short read

                        for (int i = 0; i < PKGCheck; i++)
                        {
                            if (tail[i] != 0)
                            {
                                return [];
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(tail, clearArray: false);
                    }

                    using (var fs = new FileStream(pkgFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.RandomAccess))
                    {
                        while (offsetFromEnd < fileLength)
                        {
                            int toRead = (int)Math.Min(bufferSize, fileLength - offsetFromEnd);
                            long chunkStart = fileLength - offsetFromEnd - toRead;

                            fs.Position = chunkStart;
                            int read = ReadFull(fs, buffer, 0, toRead);
                            if (read == 0) break;

                            int combinedLen = read + prevTailLen;
                            byte[] combined = ArrayPool<byte>.Shared.Rent(combinedLen);
                            try
                            {
                                Buffer.BlockCopy(buffer, 0, combined, 0, read);
                                if (prevTailLen > 0)
                                    Buffer.BlockCopy(prevTail, 0, combined, read, prevTailLen);

                                var combinedSpan = new ReadOnlySpan<byte>(combined, 0, combinedLen);

                                if (endOffset == -1)
                                {
                                    var endSpan = new ReadOnlySpan<byte>(endBytes);
                                    int idx = combinedSpan.LastIndexOf(endSpan);
                                    if (idx >= 0)
                                    {
                                        endOffset = chunkStart + idx + endBytes.Length;
                                    }
                                }

                                if (startOffset == -1)
                                {
                                    var startSpan = new ReadOnlySpan<byte>(startBytes);
                                    int idx = combinedSpan.LastIndexOf(startSpan);
                                    if (idx >= 0)
                                    {
                                        startOffset = chunkStart + idx;
                                    }
                                }

                                if (startOffset != -1 && endOffset != -1)
                                    break;

                                int copyLen = Math.Min(overlap, combinedLen);
                                Buffer.BlockCopy(combined, combinedLen - copyLen, prevTail, 0, copyLen);
                                prevTailLen = copyLen;
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(combined, clearArray: false);
                            }

                            offsetFromEnd += read;
                        }
                    }

                    if (startOffset < 0 || endOffset < 0 || endOffset <= startOffset)
                        return Array.Empty<byte>();

                    long length = endOffset - startOffset;
                    if (length <= 0 || length > int.MaxValue)
                        return Array.Empty<byte>();

                    byte[] result = new byte[length];
                    using (var fs2 = new FileStream(pkgFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.RandomAccess))
                    {
                        fs2.Position = startOffset;
                        int readTotal = ReadFull(fs2, result, 0, (int)length);
                        if (readTotal != length) return Array.Empty<byte>();
                    }

                    return result;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer, clearArray: false);
                    ArrayPool<byte>.Shared.Return(prevTail, clearArray: false);
                }
            }

            public static (List<PKGPNGImageEntry> PKGBackgrounds, List<PKGPNGImageEntry> PKGIcons) GetRetailPKGPNGs(string pkgFilePath)
            {
                const int bufferSize = 4096;
                int sigOverlap = PNGSignature.Length - 1;
                int iendOverlap = IENDSequence.Length - 1;

                byte[] scanBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                byte[] prevTail = ArrayPool<byte>.Shared.Rent(sigOverlap);
                int prevTailLen = 0;

                var results = new List<byte[]>();

                try
                {
                    long fileLen;
                    using (var fsLen = new FileStream(pkgFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        fileLen = fsLen.Length;
                    }

                    if (fileLen == 0) return ([], []);

                    // Check if valid PKG
                    int toCheck = (int)Math.Min(10, fileLen);
                    byte[] tail = ArrayPool<byte>.Shared.Rent(toCheck);
                    try
                    {
                        using var fsCheck = new FileStream(pkgFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        fsCheck.Position = fileLen - toCheck;
                        int read = ReadFull(fsCheck, tail, 0, toCheck);
                        if (read != toCheck) return ([], []);
                        for (int i = 0; i < toCheck; i++)
                        {
                            if (tail[i] != 0) return ([], []);
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(tail, clearArray: false);
                    }

                    // 1. Find first PNG nearest to file end
                    long pos = fileLen;
                    long lastStart = -1;

                    using (var fs = new FileStream(pkgFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan))
                    {
                        while (pos > 0)
                        {
                            int toRead = (int)Math.Min(bufferSize, pos);
                            pos -= toRead;

                            fs.Position = pos;
                            int read = ReadFull(fs, scanBuffer, 0, toRead);
                            if (read == 0) break;

                            int combinedLen = read + prevTailLen;
                            byte[] combined = ArrayPool<byte>.Shared.Rent(Math.Max(1, combinedLen));
                            try
                            {
                                if (read > 0) Buffer.BlockCopy(scanBuffer, 0, combined, 0, read);
                                if (prevTailLen > 0) Buffer.BlockCopy(prevTail, 0, combined, read, prevTailLen);

                                var combinedSpan = new ReadOnlySpan<byte>(combined, 0, combinedLen);
                                int idx = combinedSpan.LastIndexOf(new ReadOnlySpan<byte>(PNGSignature));
                                if (idx >= 0)
                                {
                                    lastStart = pos + idx;
                                    break;
                                }

                                int copyLen = Math.Min(sigOverlap, combinedLen);
                                if (copyLen > 0) Buffer.BlockCopy(combined, combinedLen - copyLen, prevTail, 0, copyLen);
                                prevTailLen = copyLen;
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(combined, clearArray: false);
                            }
                        }
                    }

                    if (lastStart < 0) return ([], []); // No PNG signature found

                    long FindIENDAbsolute(long startAbs)
                    {
                        byte[] iendPrev = ArrayPool<byte>.Shared.Rent(iendOverlap);
                        int iendPrevLen = 0;
                        try
                        {
                            using var fs2 = new FileStream(pkgFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
                            fs2.Position = startAbs;
                            long currentAbsolute = startAbs;

                            while (true)
                            {
                                int read = ReadFull(fs2, scanBuffer, 0, bufferSize);
                                if (read == 0) break;

                                int combinedLen = iendPrevLen + read;
                                byte[] combined = ArrayPool<byte>.Shared.Rent(Math.Max(1, combinedLen));
                                try
                                {
                                    if (iendPrevLen > 0) Buffer.BlockCopy(iendPrev, 0, combined, 0, iendPrevLen);
                                    if (read > 0) Buffer.BlockCopy(scanBuffer, 0, combined, iendPrevLen, read);

                                    var combinedSpan = new ReadOnlySpan<byte>(combined, 0, combinedLen);
                                    int idx = combinedSpan.IndexOf(new ReadOnlySpan<byte>(IENDSequence));
                                    if (idx >= 0)
                                    {
                                        long iendAbsolute = currentAbsolute - iendPrevLen + idx;
                                        return iendAbsolute;
                                    }

                                    int copyLen = Math.Min(iendOverlap, combinedLen);
                                    if (copyLen > 0)
                                    {
                                        Buffer.BlockCopy(combined, combinedLen - copyLen, iendPrev, 0, copyLen);
                                        iendPrevLen = copyLen;
                                    }
                                    else
                                    {
                                        iendPrevLen = 0;
                                    }
                                }
                                finally
                                {
                                    ArrayPool<byte>.Shared.Return(combined, clearArray: false);
                                }

                                currentAbsolute += read;
                            }
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(iendPrev, clearArray: false);
                        }

                        return -1;
                    }

                    byte[]? ExtractFoundPNG(long pngStart, long iendStart)
                    {
                        long pngEndExclusive = iendStart + IENDSequence.Length;
                        long pngLength = pngEndExclusive - pngStart;
                        if (pngLength <= 0 || pngLength > int.MaxValue) return null;

                        byte[] result = new byte[pngLength];
                        using (var fs3 = new FileStream(pkgFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan))
                        {
                            fs3.Position = pngStart;
                            int readTotal = ReadFull(fs3, result, 0, (int)pngLength);
                            if (readTotal != pngLength) return null;
                        }
                        return result;
                    }

                    // 2. Backward-collect PNGs up to scanLimit
                    var starts = new List<long> { lastStart };
                    long scanLimit = lastStart - 84934656;
                    if (scanLimit < 0) scanLimit = 0;

                    Array.Clear(prevTail, 0, prevTail.Length);
                    prevTailLen = 0;
                    pos = lastStart;

                    using (var fs = new FileStream(pkgFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan))
                    {
                        while (pos > scanLimit)
                        {
                            int toRead = (int)Math.Min(bufferSize, pos - scanLimit);
                            pos -= toRead;

                            fs.Position = pos;
                            int read = ReadFull(fs, scanBuffer, 0, toRead);
                            if (read == 0) break;

                            int combinedLen = read + prevTailLen;
                            byte[] combined = ArrayPool<byte>.Shared.Rent(Math.Max(1, combinedLen));
                            try
                            {
                                if (read > 0) Buffer.BlockCopy(scanBuffer, 0, combined, 0, read);
                                if (prevTailLen > 0) Buffer.BlockCopy(prevTail, 0, combined, read, prevTailLen);

                                var combinedSpan = new ReadOnlySpan<byte>(combined, 0, combinedLen);
                                var sigSpan = new ReadOnlySpan<byte>(PNGSignature);

                                int searchIdx = combinedLen;
                                while (true)
                                {
                                    int idx = combinedSpan.Slice(0, searchIdx).LastIndexOf(sigSpan);
                                    if (idx < 0) break;
                                    long foundStart = pos + idx;
                                    if (foundStart < starts[0])
                                    {
                                        starts.Insert(0, foundStart);
                                    }
                                    searchIdx = idx;
                                }

                                int copyLen = Math.Min(sigOverlap, combinedLen);
                                if (copyLen > 0)
                                {
                                    Buffer.BlockCopy(combined, combinedLen - copyLen, prevTail, 0, copyLen);
                                    prevTailLen = copyLen;
                                }
                                else
                                {
                                    prevTailLen = 0;
                                }
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(combined, clearArray: false);
                            }
                        }
                    }

                    // 3. Extract all collected PNGs
                    foreach (var start in starts)
                    {
                        long iendStart = FindIENDAbsolute(start);
                        if (iendStart < 0) continue;
                        var png = ExtractFoundPNG(start, iendStart);
                        if (png != null) results.Add(png);
                    }

                    long firstIend = FindIENDAbsolute(lastStart);
                    if (firstIend < 0) return ([], []);

                    long fixedForwardLimit = firstIend + IENDSequence.Length + 84934656;
                    if (fixedForwardLimit > fileLen) fixedForwardLimit = fileLen;

                    long searchPos = firstIend + IENDSequence.Length;
                    if (searchPos < fileLen && searchPos < fixedForwardLimit)
                    {
                        while (searchPos < fixedForwardLimit)
                        {
                            long foundNextStart = -1;
                            using (var fsSearch = new FileStream(pkgFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan))
                            {
                                fsSearch.Position = searchPos;
                                byte[] forwardPrev = ArrayPool<byte>.Shared.Rent(sigOverlap);
                                int forwardPrevLen = 0;
                                try
                                {
                                    while (searchPos < fixedForwardLimit)
                                    {
                                        int toRead = (int)Math.Min(bufferSize, fixedForwardLimit - searchPos);
                                        int read = ReadFull(fsSearch, scanBuffer, 0, toRead);
                                        if (read == 0) break;

                                        int combinedLen = forwardPrevLen + read;
                                        byte[] combined = ArrayPool<byte>.Shared.Rent(Math.Max(1, combinedLen));
                                        try
                                        {
                                            if (forwardPrevLen > 0) Buffer.BlockCopy(forwardPrev, 0, combined, 0, forwardPrevLen);
                                            if (read > 0) Buffer.BlockCopy(scanBuffer, 0, combined, forwardPrevLen, read);

                                            var combinedSpan = new ReadOnlySpan<byte>(combined, 0, combinedLen);
                                            int idx = combinedSpan.IndexOf(new ReadOnlySpan<byte>(PNGSignature));
                                            if (idx >= 0)
                                            {
                                                long sigAbs = searchPos - forwardPrevLen + idx;
                                                if (sigAbs < fixedForwardLimit)
                                                {
                                                    foundNextStart = sigAbs;
                                                }
                                                break;
                                            }

                                            int copyLen = Math.Min(sigOverlap, combinedLen);
                                            if (copyLen > 0)
                                            {
                                                Buffer.BlockCopy(combined, combinedLen - copyLen, forwardPrev, 0, copyLen);
                                                forwardPrevLen = copyLen;
                                            }
                                            else forwardPrevLen = 0;
                                        }
                                        finally
                                        {
                                            ArrayPool<byte>.Shared.Return(combined, clearArray: false);
                                        }

                                        searchPos += read;
                                    }
                                }
                                finally
                                {
                                    ArrayPool<byte>.Shared.Return(forwardPrev, clearArray: false);
                                }
                            }

                            if (foundNextStart < 0) break; // No more PNG found

                            long nextIend = FindIENDAbsolute(foundNextStart);
                            if (nextIend < 0) break;

                            long nextEndExclusive = nextIend + IENDSequence.Length;
                            if (nextEndExclusive > fixedForwardLimit) break;

                            var nextPng = ExtractFoundPNG(foundNextStart, nextIend);
                            if (nextPng != null) results.Add(nextPng);

                            // Advance searchPos to the end of this PNG and continue searching
                            searchPos = nextEndExclusive;
                        }
                    }

                    return CleanedPKGPNGs(results);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(scanBuffer, clearArray: false);
                    if (prevTail != null) ArrayPool<byte>.Shared.Return(prevTail, clearArray: false);
                }
            }

            public static (List<PKGPNGImageEntry> PKGBackgrounds, List<PKGPNGImageEntry> PKGIcons) CleanedPKGPNGs(List<byte[]> retrievedPNGs) // Remove empty backgrounds/icons from valid PNGs
            {
                var foundBackgrounds = new List<PKGPNGImageEntry>();
                var foundIcons = new List<PKGPNGImageEntry>();
                int backgroundCounter = 0;
                int iconCounter = 0;

                foreach (var FoundImageData in retrievedPNGs)
                {
                    var PNGInfos = ReadPNGInfo(FoundImageData);
                    if (PNGInfos.Width == 3840 && PNGInfos.Height == 2160) // Background
                    {
                        if (PNGInfos.Size > 200)
                        {
                            string name = $"icon{backgroundCounter}.png";
                            foundBackgrounds.Add(new PKGPNGImageEntry() { FileName = name, Width = PNGInfos.Width, Height = PNGInfos.Height, FileData = FoundImageData });
                            backgroundCounter++;
                        }
                    }
                    else if (PNGInfos.Width == 512 && PNGInfos.Height == 512) // Icon
                    {
                        if (PNGInfos.Size > 10)
                        {
                            string name = $"pic{iconCounter}.png";
                            foundIcons.Add(new PKGPNGImageEntry() { FileName = name, Width = PNGInfos.Width, Height = PNGInfos.Height, FileData = FoundImageData });
                            iconCounter++;
                        }
                    }
                }
                // Return as proper list of backgrounds & icons
                return (foundBackgrounds, foundIcons);
            }

            public static (int Width, int Height, long Size) ReadPNGInfo(byte[] pngBytes)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (pngBytes[i] != PNGSignature[i]) throw new ArgumentException("Not a PNG (invalid signature).", nameof(pngBytes));
                }

                int offset = 8;
                if (pngBytes.Length < offset + 8 + 13) throw new ArgumentException("PNG too short to contain IHDR.", nameof(pngBytes));

                uint length = (uint)((pngBytes[offset] << 24) | (pngBytes[offset + 1] << 16) | (pngBytes[offset + 2] << 8) | pngBytes[offset + 3]);
                offset += 4;

                string chunkType = Encoding.ASCII.GetString(pngBytes, offset, 4);
                offset += 4;

                if (chunkType != "IHDR") throw new ArgumentException("First chunk is not IHDR; PNG may be malformed.", nameof(pngBytes));
                if (length < 13) throw new ArgumentException("IHDR length invalid.", nameof(pngBytes));

                int width = (pngBytes[offset] << 24) | (pngBytes[offset + 1] << 16) | (pngBytes[offset + 2] << 8) | pngBytes[offset + 3];
                int height = (pngBytes[offset + 4] << 24) | (pngBytes[offset + 5] << 16) | (pngBytes[offset + 6] << 8) | pngBytes[offset + 7];

                long size = pngBytes.LongLength;
                return (width, height, size);
            }

            public static int ReadFull(Stream s, byte[] buffer, int offset, int count)
            {
                int total = 0;
                while (total < count)
                {
                    int n = s.Read(buffer, offset + total, count - total);
                    if (n == 0) break;
                    total += n;
                }
                return total;
            }

            public static bool MatchBytes(byte[] buffer, int position, byte[] pattern)
            {
                if (position + 1 < pattern.Length)
                {
                    return false;
                }

                for (int i = 0, loopTo = pattern.Length - 1; i <= loopTo; i++)
                {
                    if (buffer[position - i] != pattern[pattern.Length - 1 - i])
                    {
                        return false;
                    }
                }

                return true;
            }

        }
    }
}

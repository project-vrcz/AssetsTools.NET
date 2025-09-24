using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using AssetsTools.NET.Extra;
using CliWrap;
using SevenZip.Compression.LZMA;

namespace AssetsTools.NET.Compression.Lzma;

public static class LzmaHelper
{
    // public static void Compress(Stream inStream, Stream outStream, int inSize = -1, int outSize = -1)
    // {
    //     var encoder = new Encoder();
    //     encoder.SetCoderProperties(LzmaProperties.PropIDs, LzmaProperties.GetProperties(LZMACompressionLevel.Normal));
    //     encoder.WriteCoderProperties(outStream);
    //
    //     encoder.Code(inStream, outStream, inSize, outSize, null);
    // }

    public static void Compress(Stream inStream, Stream outStream)
    {
        var lzmaPath = GetLzmaExePath();
    
        var result = Cli.Wrap(lzmaPath)
            .WithArguments(["e", "-si", "-so", "-mt64", "-a1", "-fb32"])
            .WithStandardInputPipe(PipeSource.FromStream(inStream))
            .WithStandardOutputPipe(PipeTarget.ToStream(outStream))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(error =>
            {
                Console.WriteLine(error);
            }))
            .ExecuteAsync()
            .GetAwaiter().GetResult();
        
        if (!result.IsSuccess)
            throw new Exception("lzma.exe exited with error code " + result.ExitCode);
    }
    
    public static void Decompress(Stream inStream, Stream outStream, int outSize = -1, int inSize = -1)
    {
        var inputStream = inSize == -1 ? inStream : new SegmentStream(inStream, inStream.Position, inSize);
        
        var lzmaPath = GetLzmaExePath();

        var result = Cli.Wrap(lzmaPath)
            .WithArguments(["d", "-si", "-so", "-mt64", "-a0"])
            .WithStandardInputPipe(PipeSource.FromStream(inputStream))
            .WithStandardOutputPipe(PipeTarget.ToStream(outStream))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(error =>
            {
                Console.WriteLine(error);
            }))
            .ExecuteAsync()
            .GetAwaiter().GetResult();
        
        if (!result.IsSuccess)
            throw new Exception("lzma.exe exited with error code " + result.ExitCode);
    }

    // public static void Decompress(Stream inStream, Stream outStream, int outSize = -1, int inSize = -1)
    // {
    //     var decoder = new Decoder();
    //
    //     var properties = new byte[5];
    //     if (inStream.Read(properties, 0, 5) != 5)
    //         throw new InvalidDataException("Input .lzma file is too short");
    //
    //     inSize = inSize == -1 ? (int)(inStream.Length - inStream.Position) : inSize;
    //
    //     decoder.SetDecoderProperties(properties);
    //     decoder.Code(inStream, outStream, inSize, outSize, null);
    // }

    private static string GetLzmaExePath()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new NotSupportedException("Unsupported architecture for lzma.exe")
        };
        
        var lzmaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "bin-lzma", arch, "lzma.exe"));
        if (!File.Exists(lzmaPath))
            throw new FileNotFoundException("lzma.exe not found", lzmaPath);
        
        return lzmaPath;
    }
}
using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;

namespace AssetsTools.NET.Compression.Lzma;

public static class LzmaHelper
{
    public static void Compress(Stream inStream, Stream outStream)
    {
        var lzmaPath = GetLzmaExePath();

        var result = Cli.Wrap(lzmaPath)
            .WithArguments(["e", "-si", "-so", "-mt8", "-a1", "-mfbt5"])
            .WithStandardInputPipe(PipeSource.FromStream(inStream))
            .WithStandardOutputPipe(PipeTarget.Create((stream, token) =>
                HandleLzmaCompressStream(stream, outStream, token)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(error => { Console.WriteLine(error); }))
            .ExecuteAsync()
            .GetAwaiter().GetResult();

        if (!result.IsSuccess)
            throw new Exception("lzma.exe exited with error code " + result.ExitCode);
    }

    private static async Task HandleLzmaCompressStream(Stream stream, Stream outStream, CancellationToken cts)
    {
        // Read Header
        var headerBuffer = new byte[5].AsMemory();
        await stream.ReadExactlyAsync(headerBuffer, cts)
            .ConfigureAwait(false);

        await outStream
            .WriteAsync(headerBuffer, cts)
            .ConfigureAwait(false);
        await outStream.FlushAsync(cts).ConfigureAwait(false);

        // Skip Junk Original Size Placeholder (Unity AssetsBundle quirk)
        var junkBuffer = new byte[8].AsMemory();
        await stream.ReadExactlyAsync(junkBuffer, cts)
            .ConfigureAwait(false);

        using var buffer = MemoryPool<byte>.Shared.Rent(81920);
        while (true)
        {
            var bytesRead = await stream
                .ReadAsync(buffer.Memory, cts)
                .ConfigureAwait(false);

            if (bytesRead <= 0)
                break;

            await outStream
                .WriteAsync(buffer.Memory[..bytesRead], cts)
                .ConfigureAwait(false);

            await outStream.FlushAsync(cts).ConfigureAwait(false);
        }
    }

    public static void Decompress(Stream inStream, Stream outStream, int outSize, int inSize = -1)
    {
        var inputStream = inSize == -1 ? inStream : new SegmentStream(inStream, inStream.Position, inSize);

        var lzmaPath = GetLzmaExePath();

        var result = Cli.Wrap(lzmaPath)
            .WithArguments(["d", "-si", "-so", "-mt8"])
            .WithStandardInputPipe(
                PipeSource.Create((stream, token) => HandleLzmaDecompressStream(inputStream, stream, outSize, token)))
            .WithStandardOutputPipe(PipeTarget.ToStream(outStream))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(error => { Console.WriteLine(error); }))
            .ExecuteAsync()
            .GetAwaiter().GetResult();

        if (!result.IsSuccess)
            throw new Exception("lzma.exe exited with error code " + result.ExitCode);
    }
    
    private static async Task HandleLzmaDecompressStream(Stream stream, Stream outStream, int outSize, CancellationToken cts)
    {
        // Read Header
        var headerBuffer = new byte[5].AsMemory();
        await stream.ReadExactlyAsync(headerBuffer, cts)
            .ConfigureAwait(false);

        await outStream
            .WriteAsync(headerBuffer, cts)
            .ConfigureAwait(false);
        await outStream.FlushAsync(cts).ConfigureAwait(false);

        // Write Original Size Header
        await outStream.WriteAsync(BitConverter.GetBytes((ulong)outSize), cts)
            .ConfigureAwait(false);
        await outStream.FlushAsync(cts).ConfigureAwait(false);

        using var buffer = MemoryPool<byte>.Shared.Rent(81920);
        while (true)
        {
            var bytesRead = await stream
                .ReadAsync(buffer.Memory, cts)
                .ConfigureAwait(false);

            if (bytesRead <= 0)
                break;

            await outStream
                .WriteAsync(buffer.Memory[..bytesRead], cts)
                .ConfigureAwait(false);

            await outStream.FlushAsync(cts).ConfigureAwait(false);
        }
    }

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
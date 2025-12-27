using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;

/// <summary>
/// アンマネージドバッファを管理するクラス（処理後に確実に破棄）
/// </summary>
internal sealed unsafe class UnmanagedBuffer : IDisposable
{
    private byte* _bufferPtr;
    private nuint _bufferSize;
    private bool _disposed;

    public int BufferSize => (int)_bufferSize;

    public UnmanagedBuffer(int bufferSize)
    {
        _bufferSize = (nuint)bufferSize;
        _bufferPtr = (byte*)NativeMemory.Alloc(_bufferSize);
    }

    /// <summary>
    /// アンマネージドバッファをSpanとして取得
    /// </summary>
    public Span<byte> GetBufferSpan()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(UnmanagedBuffer));
        return new Span<byte>(_bufferPtr, (int)_bufferSize);
    }

    /// <summary>
    /// アンマネージドバッファのポインタを取得
    /// </summary>
    public byte* GetBufferPtr()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(UnmanagedBuffer));
        return _bufferPtr;
    }

    /// <summary>
    /// FileStreamからバッファに読み込み
    /// </summary>
    public int ReadFromStream(FileStream stream, int offset, int count)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(UnmanagedBuffer));

        Span<byte> span = new Span<byte>(_bufferPtr + offset, count);
        return stream.Read(span);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_bufferPtr != null)
            {
                NativeMemory.Free(_bufferPtr);
                _bufferPtr = null;
            }
            _disposed = true;
        }
    }
}

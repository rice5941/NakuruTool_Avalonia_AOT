using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;

/// <summary>
/// バイナリ読み取りの共通ヘルパークラス
/// </summary>
public static class BinaryReaderHelper
{
    /// <summary>
    /// osu!形式の文字列を読み込む
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadStringFromSpan(ReadOnlySpan<byte> buffer, ref int pos)
    {
        byte prefix = buffer[pos++];

        if (prefix == 0x00)
        {
            return string.Empty;
        }
        else if (prefix == 0x0b)
        {
            uint length = ReadULEB128FromSpan(buffer, ref pos);
            if (length == 0)
                return string.Empty;
            string str = Encoding.UTF8.GetString(buffer.Slice(pos, (int)length));
            pos += (int)length;
            return str;
        }
        return string.Empty;
    }

    /// <summary>
    /// ULEB128形式の整数を読み込む
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReadULEB128FromSpan(ReadOnlySpan<byte> buffer, ref int pos)
    {
        uint result = 0;
        int shift = 0;

        while (true)
        {
            byte b = buffer[pos++];
            result |= (uint)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                break;

            shift += 7;
        }

        return result;
    }

    /// <summary>
    /// DateTimeを読み込む
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime ReadDateTimeFromSpan(ReadOnlySpan<byte> buffer, ref int pos)
    {
        long ticks = BitConverter.ToInt64(buffer.Slice(pos, 8));
        pos += 8;
        try { return new DateTime(ticks, DateTimeKind.Utc); }
        catch { return DateTime.MinValue; }
    }
}

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace NakuruTool_Avalonia_AOT;

/// <summary>
/// アプリケーションの多重起動を防止する。
/// 名前付き Mutex を保持し、既に起動済みの場合は既存インスタンスのウィンドウを前面に表示する。
/// </summary>
internal sealed partial class SingleInstanceGuard : IDisposable
{
    // Local\ プレフィックスによりログオンセッション単位で判定する
    private const string MutexName = @"Local\NakuruTool_SingleInstance_8E2B6C1F-4A3D-4F5E-9B7A-2C1D0E3F4A5B";

    private readonly Mutex _mutex;
    private bool _hasHandle;

    private SingleInstanceGuard(Mutex mutex, bool hasHandle)
    {
        _mutex = mutex;
        _hasHandle = hasHandle;
    }

    /// <summary>
    /// 最初のインスタンスであれば true を返す
    /// </summary>
    public bool IsFirstInstance => _hasHandle;

    public static SingleInstanceGuard Acquire()
    {
        var mutex = new Mutex(false, MutexName);
        bool hasHandle;
        try
        {
            hasHandle = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // 前回のインスタンスが異常終了した場合も所有権は取得できている
            hasHandle = true;
        }

        return new SingleInstanceGuard(mutex, hasHandle);
    }

    /// <summary>
    /// 既に起動しているインスタンスのメインウィンドウを前面に表示する
    /// </summary>
    public static void ActivateExistingInstance()
    {
        try
        {
            using var current = Process.GetCurrentProcess();
            foreach (var process in Process.GetProcessesByName(current.ProcessName))
            {
                using (process)
                {
                    if (process.Id == current.Id)
                    {
                        continue;
                    }

                    var hWnd = process.MainWindowHandle;
                    if (hWnd == IntPtr.Zero)
                    {
                        continue;
                    }

                    if (IsIconic(hWnd))
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                    }

                    SetForegroundWindow(hWnd);
                    return;
                }
            }
        }
        catch
        {
            // 前面表示はベストエフォートのため失敗しても無視する
        }
    }

    public void Dispose()
    {
        if (_hasHandle)
        {
            _mutex.ReleaseMutex();
            _hasHandle = false;
        }

        _mutex.Dispose();
    }

    private const int SW_RESTORE = 9;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr hWnd);
}

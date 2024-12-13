using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using UnityEngine;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;

#nullable enable

[NodeType(
    Id = "031cae45-c30d-45b3-8e8e-6443a077a48e",
    Title = "YouTube Stream Receiver のコンソールにエラーが表示されたらそのプロセスを終了するよ",
    Category = "CATEGORY_DEBUG",
    Width = 1f
)]
class YouTubeStreamReceiverConsoleWatcherNode : Node
{
    private readonly Regex regex = new(@"^Error: undefined", RegexOptions.Multiline);

    private float nextTime;
    private string? consoleText;
    private string? lastExitTime;

    [DataInput]
    [Label("ENABLE")]
    public bool Enabled = false;

    [DataInput]
    [Label("動作周期 [秒]")]
    public int Interval = 10;

    [DataOutput]
    [Label("最後に終了した時刻")]
    public string? LastExitTime() => lastExitTime;

    [DataOutput]
    public string? ConsoleText() => consoleText;

    [FlowOutput]
    [Label("終了")]
    public Continuation Exit = default!;

    [FlowOutput]
    [Label("Watch")]
    public Continuation OnWatch = default!;

    protected override void OnCreate()
    {
        WatchAll(new[] { nameof(Enabled), nameof(Interval) }, ResetTimer);
        ResetTimer();
    }

    public override async void OnUpdate()
    {
        if (!this.Enabled)
        {
            return;
        }

        var time = Time.time;
        if (nextTime == 0)
        {
            nextTime = time + Interval;
            return;
        }

        if (nextTime <= time)
        {
            nextTime = time + Interval;
            await Watch();
        }
    }

    private void ResetTimer()
    {
        nextTime = 0;
    }

    private async UniTask Watch()
    {
        this.InvokeFlow(nameof(OnWatch));
        
        var result = await Ex.GetYouTubeStreamReceiverConsoleAsync(default);
        if (result == null)
        {
            this.consoleText = null;
            return;
        }

        var (process, text) = result.Value;
        this.consoleText = text;

        if (regex.IsMatch(text))
        {
            this.lastExitTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            this.InvokeFlow(nameof(Exit));
            process.Kill();
        }
    }

    static class Ex
    {
        [ThreadStatic]
        private static StringBuilder? stringBuilderBuffer;

        private static StringBuilder StringBuilderBuffer => stringBuilderBuffer ??= new StringBuilder(10240);

        [ThreadStatic]
        private static char[]? consoleReadBuffer;

        public static UniTask<(Process Process, string Text)?> GetYouTubeStreamReceiverConsoleAsync(CancellationToken cancellationToken)
        {
            return UniTask.RunOnThreadPool(() => GetYouTubeStreamReceiverConsole(cancellationToken), cancellationToken: cancellationToken);
        }

        public static (Process Process, string Text)? GetYouTubeStreamReceiverConsole(CancellationToken cancellationToken)
        {
            var youtubeStreamReceiverProcesses = Process.GetProcessesByName("YouTubeStreamReceiver");
            foreach (var process in youtubeStreamReceiverProcesses)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!AttachConsole(process.Id))
                {
                    throw new Win32Exception();
                }

                try
                {
                    var stdHandle = GetStdHandle(STD_HANDLE.STD_OUTPUT_HANDLE);
                    using var _ = new SafeFileHandle(stdHandle, ownsHandle: false);
                    return (process, GetConsoleText(stdHandle));
                }
                finally
                {
                    FreeConsole();
                }
            }

            return null;
        }

        static string GetConsoleText(nint stdHandle)
        {
            var consoleScreenBufferInfo = new CONSOLE_SCREEN_BUFFER_INFOEX()
            {
                cbSize = Marshal.SizeOf<CONSOLE_SCREEN_BUFFER_INFOEX>(),
            };

            if (!GetConsoleScreenBufferInfoEx(stdHandle, ref consoleScreenBufferInfo))
            {
                throw new Win32Exception();
            }

            var bufferLength = consoleScreenBufferInfo.dwSize.X * consoleScreenBufferInfo.dwSize.Y;
            var buffer = consoleReadBuffer?.Length == bufferLength ? consoleReadBuffer : consoleReadBuffer = new char[bufferLength];

            int charsRead;
            var bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var pBuffer = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0);
                if (!ReadConsoleOutputCharacter(stdHandle, pBuffer, buffer.Length, new COORD(), out charsRead))
                {
                    throw new Win32Exception();
                }
            }
            finally
            {
                bufferHandle.Free();
            }

            var sb = StringBuilderBuffer.Clear();
            var addedCharIndex = 0;
            var asciiCount = 0;
            for (var i = 0; i < charsRead; i++)
            {
                var isAscii = buffer[i] <= 0x7E;
                asciiCount += isAscii ? 1 : 2;

                if (asciiCount >= consoleScreenBufferInfo.dwSize.X)
                {
                    sb.Append(buffer, addedCharIndex, i + 1 - addedCharIndex);
                    sb.AppendLine();

                    addedCharIndex = i + 1;
                    asciiCount = 0;
                }
            }

            if (addedCharIndex < charsRead)
            {
                sb.Append(buffer, addedCharIndex, (int)charsRead - addedCharIndex);
            }

            return sb.ToString();
        }

        [DllImport("KERNEL32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AttachConsole(int dwProcessId);

        [DllImport("KERNEL32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool FreeConsole();

        [DllImport("KERNEL32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern nint GetStdHandle(STD_HANDLE nStdHandle);

        [DllImport("KERNEL32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetConsoleScreenBufferInfoEx(nint hConsoleOutput, ref CONSOLE_SCREEN_BUFFER_INFOEX lpConsoleScreenBufferInfoEx);

        [DllImport("KERNEL32.dll", ExactSpelling = true, EntryPoint = "ReadConsoleOutputCharacterW", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ReadConsoleOutputCharacter(nint hConsoleOutput, /* char* */ nint lpCharacter, int nLength, COORD dwReadCoord, out int lpNumberOfCharsRead);

        internal enum STD_HANDLE : uint
        {
            STD_INPUT_HANDLE = 4294967286U,
            STD_OUTPUT_HANDLE = 4294967285U,
            STD_ERROR_HANDLE = 4294967284U,
        }

        internal partial struct CONSOLE_SCREEN_BUFFER_INFOEX
        {
            /// <summary>The size of this structure, in bytes.</summary>
            internal int cbSize;

            /// <summary>A [**COORD**](coord-str.md) structure that contains the size of the console screen buffer, in character columns and rows.</summary>
            internal COORD dwSize;

            /// <summary>A [**COORD**](coord-str.md) structure that contains the column and row coordinates of the cursor in the console screen buffer.</summary>
            internal COORD dwCursorPosition;

            /// <summary>The attributes of the characters written to a screen buffer by the [**WriteFile**](/windows/win32/api/fileapi/nf-fileapi-writefile) and [**WriteConsole**](writeconsole.md) functions, or echoed to a screen buffer by the [**ReadFile**](/windows/win32/api/fileapi/nf-fileapi-readfile) and [**ReadConsole**](readconsole.md) functions. For more information, see [Character Attributes](console-screen-buffers.md#character-attributes).</summary>
            internal ushort wAttributes;

            /// <summary>A [**SMALL\_RECT**](small-rect-str.md) structure that contains the console screen buffer coordinates of the upper-left and lower-right corners of the display window.</summary>
            internal SMALL_RECT srWindow;

            /// <summary>A [**COORD**](coord-str.md) structure that contains the maximum size of the console window, in character columns and rows, given the current screen buffer size and font and the screen size.</summary>
            internal COORD dwMaximumWindowSize;

            /// <summary>The fill attribute for console pop-ups.</summary>
            internal ushort wPopupAttributes;

            /// <summary>If this member is `TRUE`, full-screen mode is supported; otherwise, it is not. This will always be `FALSE` for systems after Windows Vista with the [WDDM driver model](/windows-hardware/drivers/display/introduction-to-the-windows-vista-and-later-display-driver-model) as true direct VGA access to the monitor is no longer available.</summary>
            internal int bFullscreenSupported;

            /// <summary>An array of [**COLORREF**](/windows/win32/gdi/colorref) values that describe the console's color settings.</summary>
            internal __COLORREF_16 ColorTable;
        }

        internal partial struct COORD
        {
            /// <summary>The horizontal coordinate or column value. The units depend on the function call.</summary>
            internal short X;

            /// <summary>The vertical coordinate or row value. The units depend on the function call.</summary>
            internal short Y;
        }

        internal partial struct SMALL_RECT
        {
            /// <summary>The x-coordinate of the upper left corner of the rectangle.</summary>
            internal short Left;

            /// <summary>The y-coordinate of the upper left corner of the rectangle.</summary>
            internal short Top;

            /// <summary>The x-coordinate of the lower right corner of the rectangle.</summary>
            internal short Right;

            /// <summary>The y-coordinate of the lower right corner of the rectangle.</summary>
            internal short Bottom;
        }

        internal readonly struct __COLORREF_16
        {
            readonly uint _0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13, _14, _15;
        }
    }
}
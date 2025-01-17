﻿// Read S SmartImage ConsoleUtil.cs
// 2023-01-30 @ 7:45 PM

#region

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Kantan.Console;
using Novus.Win32;
using Novus.Win32.Structures.Kernel32;
using Novus.Win32.Structures.User32;
using SmartImage.App;
using Terminal.Gui;

#endregion

namespace SmartImage.Utilities;

[SupportedOSPlatform(Compat.OS)]
internal static class ConsoleUtil
{
	internal const int CODE_ERR = -1;

	internal const int CODE_OK = 0;
	// internal static SearchEngineOptions[] EngineOptions => (SearchEngineOptions[]) Cache[nameof(EngineOptions)];

	internal static readonly nint HndWindow = Native.GetConsoleWindow();
	internal static readonly nint StdOut    = Native.GetStdHandle(StandardHandle.STD_OUTPUT_HANDLE);
	internal static readonly nint StdIn     = Native.GetStdHandle(StandardHandle.STD_INPUT_HANDLE);

	internal static ConsoleModes _oldMode;

	static ConsoleUtil()
	{
		// Cache[nameof(EngineOptions)] = Enum.GetValues<SearchEngineOptions>();

	}

	internal static void SetConsoleMenu()
	{
		nint sysMenu = Native.GetSystemMenu(HndWindow, false);

		Native.DeleteMenu(sysMenu, (int) SysCommand.SC_MAXIMIZE, Native.MF_BYCOMMAND);
		Native.DeleteMenu(sysMenu, (int) SysCommand.SC_SIZE, Native.MF_BYCOMMAND);
	}

	private const int CON_WIDTH = 150;

	private const int CON_HEIGHT = 35;

	internal static void SetConsoleMode()
	{
		// Clipboard.Open();
		try {
			Console.SetWindowSize(CON_WIDTH, CON_HEIGHT);
		}
		catch (Exception e) {
			Debug.WriteLine($"{e.Message}");
		}

		Console.InputEncoding = Console.OutputEncoding = Encoding.UTF8;
		// Console.InputEncoding = Console.OutputEncoding = Encoding.Unicode;

		Native.GetConsoleMode(StdIn, out ConsoleModes lpMode);

		_oldMode = lpMode;

		/*Native.SetConsoleMode(StdIn, lpMode | ConsoleModes.ENABLE_MOUSE_INPUT &
		                             ~ConsoleModes.ENABLE_QUICK_EDIT_MODE |
		                             ConsoleModes.ENABLE_EXTENDED_FLAGS |
		                             ConsoleModes.ENABLE_ECHO_INPUT |
		                             ConsoleModes.ENABLE_VIRTUAL_TERMINAL_PROCESSING);*/

		Native.SetConsoleMode(StdIn, lpMode | ConsoleModes.ENABLE_MOUSE_INPUT &
		                             ~ConsoleModes.ENABLE_QUICK_EDIT_MODE |
		                             ConsoleModes.ENABLE_EXTENDED_FLAGS |
		                             ConsoleModes.ENABLE_ECHO_INPUT |
		                             ConsoleModes.ENABLE_VIRTUAL_TERMINAL_PROCESSING |
		                             ConsoleModes.ENABLE_PROCESSED_OUTPUT);

		// Console.SetWindowSize(150, 35);
		// Console.BufferWidth = 150;

	}

	internal static void FlashTaskbar()
	{
		var pwfi = new FLASHWINFO()
		{
			cbSize    = (uint) Marshal.SizeOf<FLASHWINFO>(),
			hwnd      = HndWindow,
			dwFlags   = FlashWindowType.FLASHW_TRAY,
			uCount    = 8,
			dwTimeout = 75
		};

		Native.FlashWindowEx(ref pwfi);
	}
}
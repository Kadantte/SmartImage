﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using RestSharp;
using SmartImage.Engines;
using SmartImage.Engines.SauceNao;
using SmartImage.Model;
using SmartImage.Utilities;

namespace SmartImage
{
	public static class Program
	{
		// @"C:\Users\Deci\Desktop\test.jpg";
		
		// C:\Users\Deci\RiderProjects\SmartImage\SmartImage\bin\Release\netcoreapp3.0\win10-x64\publish
		// C:\Users\Deci\RiderProjects\SmartImage\SmartImage\bin\Debug\netcoreapp3.0\win10-x64
		// copy SmartImage.exe C:\Users\Deci\AppData\Local\SmartImage /Y
		// dotnet publish -c Release -r win10-x64
		
		// copy SmartImage.exe C:\Library /Y

		// Computer\HKEY_CLASSES_ROOT\*\shell\SmartImage
		// Computer\HKEY_CURRENT_USER\Software\SmartImage

		// Computer\HKEY_CLASSES_ROOT\*\shell\SmartImage
		// "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment"


		private static void Main(string[] args)
		{
			Cli.Init();

			if (args == null || args.Length < 1) {
				Cli.WriteError("Image or command not specified!");
				Cli.WriteHelp();
				return;
			}


			// Run the command if one was parsed
			var cmd = Cli.ReadCommand(args[0]);

			if (cmd != null) {
				cmd.Action(args);
				return;
			}

			var auth = Config.ImgurAuth;
			bool useImgur = !auth.IsNull;

			var engines  = Config.SearchEngines;
			var priority = Config.PriorityEngines;

			if (engines == SearchEngines.None) {
				Cli.WriteError("Please configure search engine preferences!");
				return;
			}
			
			Cli.WriteInfo("Engines: {0}", engines);
			Cli.WriteInfo("Priority engines: {0}", priority);

			var img = args[0];

			if (!Images.IsFileValid(img)) {
				return;
			}

			Cli.WriteInfo("Source image: {0}", img);

			string imgUrl = Images.Upload(img, useImgur);

			Cli.WriteInfo("Temporary image url: {0}", imgUrl);

			Console.WriteLine();

			//
			// 
			//
			
			// Where the actual searching occurs
			var results = Search.RunSearches(imgUrl, engines);

			ConsoleKeyInfo cki;

			do {
				Console.Clear();

				for (int i = 0; i < results.Length; i++) {
					var r   = results[i];
					var str = r.Format((i+1).ToString());

					Console.WriteLine(str);
				}

				Console.WriteLine();

				Cli.WriteSuccess("Enter the result number to open or escape to quit.");

				while (!Console.KeyAvailable) {
					// Block until input is entered.
				}

				// Key was read

				cki = Console.ReadKey(true);
				var keyChar = cki.KeyChar;

				if (Char.IsNumber(keyChar)) {
					var idx = (int) Char.GetNumericValue(cki.KeyChar) - 1;

					if (idx < results.Length) {
						var res = results[idx];
						Common.OpenUrl(res.Url);
					}
				}
			} while (cki.Key != ConsoleKey.Escape);
		}
	}
}
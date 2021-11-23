﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using JetBrains.Annotations;
using Kantan.Cli;
using Kantan.Cli.Controls;
using Kantan.Diagnostics;
using Kantan.Net;
using Kantan.Utilities;
using Novus.Utilities;
using Novus.Win32;
using SmartImage.Core;
using SmartImage.Lib.Engines;
using SmartImage.Lib.Searching;
using SmartImage.Lib.Utilities;
using static Kantan.Cli.Controls.ConsoleOption;
// ReSharper disable SuggestVarOrType_SimpleTypes

// ReSharper disable InconsistentNaming

// ReSharper disable PossibleNullReferenceException

namespace SmartImage.UI;

internal static class ConsoleUIFactory
{
	/*
	 * todo: this is all glue code :(
	 */

	internal static ConsoleOption CreateConfigOption(string f, string name)
	{
		return new()
		{
			Name  = name,
			Color = AppInterface.Elements.ColorOther,
			Function = () =>
			{
				var enumOptions = FromEnum<SearchEngineOptions>();

				var selected = (new ConsoleDialog
					               {
						               Options        = enumOptions,
						               SelectMultiple = true
					               }).ReadInput();

				var enumValue = EnumHelper.ReadFromSet<SearchEngineOptions>(selected.Output);
				var field     = Program.Config.GetType().GetAnyResolvedField(f);
				field.SetValue(Program.Config, enumValue);

				Console.WriteLine(enumValue);

				ConsoleManager.WaitForSecond();

				Debug.Assert((SearchEngineOptions) field.GetValue(Program.Config) == enumValue);

				AppConfig.UpdateConfig();

				return null;
			}
		};
	}

	internal static ConsoleOption CreateConfigOption(PropertyInfo member, string name, int i, Action<bool> fn)
	{
		bool initVal = (bool) member.GetValue(Program.Config);

		return new ConsoleOption
		{
			Name = AppInterface.Elements.GetName(name, initVal),
			Function = () =>
			{
				var pi = member.DeclaringType.GetProperty(member.Name);

				bool curVal = (bool) pi.GetValue(null);

				fn(curVal);

				bool newVal = (bool) pi.GetValue(null);

				AppInterface.MainMenuOptions[i].Name = AppInterface.Elements.GetName(name, newVal);

				Debug.Assert((bool) pi.GetValue(null) == newVal);

				return null;
			}
		};
	}

	internal static ConsoleOption CreateConfigOption(string m, string name, int i)
	{
		bool initVal = (bool) (Program.Config).GetType().GetAnyResolvedField(m).GetValue(Program.Config);

		return new ConsoleOption
		{
			Name = AppInterface.Elements.GetName(name, initVal),
			Function = () =>
			{
				var    fi     = Program.Config.GetType().GetAnyResolvedField(m);
				object curVal = fi.GetValue(Program.Config);
				bool   newVal = !(bool) curVal;
				fi.SetValue(Program.Config, newVal);

				AppInterface.MainMenuOptions[i].Name = AppInterface.Elements.GetName(name, newVal);

				Debug.Assert((bool) fi.GetValue(Program.Config) == newVal);

				AppConfig.UpdateConfig();

				return null;
			}
		};
	}

	internal static ConsoleOption CreateResultOption(SearchResult result)
	{
		var color = AppInterface.Elements.EngineColorMap[result.Engine.EngineOption];

		var option = new ConsoleOption
		{
			Functions = new Dictionary<ConsoleModifiers, ConsoleOptionFunction>
			{
				[NC_FN_MAIN] = CreateOpenFunction(result.PrimaryResult is { Url: { } }
					                                  ? result.PrimaryResult.Url
					                                  : result.RawUri),

				[ConsoleModifiers.Shift] = CreateOpenFunction(result.RawUri),
				[ConsoleModifiers.Alt] = () =>
				{
					if (result.OtherResults.Any()) {

						var options = CreateResultOptions(result.OtherResults, $"Other result");

						(new ConsoleDialog
								{
									Options = options
								}).ReadInput();
					}

					return null;
				},


			},


			Color = color,

			Name = result.Engine.Name,
			Data = result,
		};

		option.Functions[ConsoleModifiers.Control | ConsoleModifiers.Alt] =
			CreateDownloadFunction(() => result.PrimaryResult.Direct);

		option.Functions[ConsoleModifiers.Control] = () =>
		{
			var cts = new CancellationTokenSource();
			if (OperatingSystem.IsWindows()) {
				ConsoleProgressIndicator.Start(cts);

			}
			result.OtherResults.AsParallel().ForAll(f => f.FindDirectImages());

			var other = result.OtherResults.FirstOrDefault(x => x.Direct is { });
			result.PrimaryResult = other ?? result.PrimaryResult;

			cts.Cancel();
			cts.Dispose();

			option.Data = result;

			return null;
		};

		return option;
	}

	[StringFormatMethod("n")]
	internal static ConsoleOption[] CreateResultOptions(IEnumerable<ImageResult> result, string n,
	                                                    Color c = default)
	{
		if (c == default) {
			c = AppInterface.Elements.ColorOther;
		}

		int i = 0;
		return result.Select(r => CreateResultOption(r, $"{n} #{i++}", c)).ToArray();
	}

	internal static ConsoleOption CreateResultOption(ImageResult result, string n, Color c,
	                                                 float correction = -.3f)
	{
		var option = new ConsoleOption
		{
			Color = c.ChangeBrightness(correction),
			Name  = n,
			Data  = result,
			Functions =
			{
				[NC_FN_MAIN] = CreateOpenFunction(result.Url),

				[ConsoleModifiers.Control | ConsoleModifiers.Alt] = CreateDownloadFunction(() => result.Direct)
			}
		};

		return option;
	}

	internal static ConsoleOptionFunction CreateDownloadFunction(Func<Uri> d)
	{
		// Because of value type and pointer semantics, a func needs to be used here to ensure the
		// Direct field of ImageResult is updated.
		// ImageResult is a struct so updates cannot be seen by these functions

		return () =>
		{
			var direct = d();

			if (direct != null) {
				var path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

				var file = ImageHelper.Download(direct, path);

				FileSystem.ExploreFile(file);

				Debug.WriteLine($"Download: {file}", LogCategories.C_INFO);
			}

			return null;
		};
	}

	internal static ConsoleOptionFunction CreateOpenFunction(Uri url)
	{
		return () =>
		{
			if (url != null) {
				WebUtilities.OpenUrl(url.ToString());
			}

			return null;
		};
	}
}
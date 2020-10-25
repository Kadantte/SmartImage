﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using SimpleCore.CommandLine;
using SimpleCore.Net;
using SimpleCore.Utilities;
using SimpleCore.Win32;
using SmartImage.Searching.Engines.Imgur;
using SmartImage.Searching.Engines.Other;
using SmartImage.Searching.Engines.SauceNao;
using SmartImage.Searching.Engines.TraceMoe;
using SmartImage.Searching.Model;
using SmartImage.Utilities;

// ReSharper disable ConvertIfStatementToReturnStatement

#pragma warning disable HAA0502, HAA0302, HAA0601, HAA0101, HAA0301, HAA0603

namespace SmartImage.Searching
{
	// todo: replace threads with tasks and async


	/// <summary>
	///     Searching client
	/// </summary>
	public class SearchClient
	{
		private const string ORIGINAL_IMAGE_NAME = "(Original image)";


		private static readonly string InterfacePrompt =
			$"Enter the option number to open or {NConsoleIO.NC_GLOBAL_EXIT_KEY} to exit.\n" +
			$"Hold down {NConsoleOption.NC_ALT_FUNC_MODIFIER} to show more info ({SearchResult.ATTR_EXTENDED_RESULTS}).\n" +
			$"Hold down {NConsoleOption.NC_CTRL_FUNC_MODIFIER} to download ({SearchResult.ATTR_DOWNLOAD}).\n";

		private readonly SearchEngineOptions m_engines;

		private readonly FileInfo m_img;

		private readonly string m_imgUrl;

		private readonly Thread m_monitor;

		private readonly Task[] m_tasks;

		private List<SearchResult> m_results;

		public bool Complete { get; private set; }


		/// <summary>
		///     Searching client
		/// </summary>
		public static SearchClient Client { get; } = new SearchClient(SearchConfig.Config.Image);

		/// <summary>
		///     Search results
		/// </summary>
		public ref List<SearchResult> Results => ref m_results;

		public NConsoleUI Interface { get; }

		private SearchClient(string img)
		{
			if (!IsFileValid(img)) {
				throw new SmartImageException("Invalid image");
			}

			string auth = SearchConfig.Config.ImgurAuth;
			bool useImgur = !String.IsNullOrWhiteSpace(auth);

			var engines = SearchConfig.Config.SearchEngines;

			if (engines == SearchEngineOptions.None) {
				engines = SearchConfig.ENGINES_DEFAULT;
			}

			m_engines = engines;
			m_img = new FileInfo(img);
			m_imgUrl = Upload(img, useImgur);
			m_tasks = CreateSearchTasks();

			// Joining each thread isn't necessary as this object is disposed upon program exit
			// Background threads won't prevent program termination

			m_monitor = new Thread(SearchMonitor)
			{
				Priority = ThreadPriority.Highest,
				IsBackground = true
			};

			Complete = false;

			Interface = new NConsoleUI(Results)
			{
				SelectMultiple = false,
				Prompt = InterfacePrompt
			};
		}

		/// <summary>
		///     Process a <see cref="SearchResult" /> to determine its MIME type, its proper URLs, and other aspects.
		/// </summary>
		/// <remarks>
		///     Organizes <see cref="SearchResult.Url" />, <see cref="SearchResult.RawUrl" />, <see cref="SearchResult.RootUrl" />
		/// </remarks>
		public static void RunProcessingTask(SearchResult result)
		{
			// todo: move image functions into separate class or something
			// todo

			var task = new Task(InspectTask);
			task.Start();

			void InspectTask()
			{
				if (!result.IsProcessed) {
					string? type = Network.IdentifyType(result.Url);
					bool isImage = Network.IsImage(type);

					result.MimeType = type;
					result.IsImage = isImage;

					result.IsProcessed = true;

					Debug.WriteLine("Process {0}", new object[] {result.Name});
				}
			}

			NConsoleIO.Refresh();
		}

		private void SearchMonitor()
		{
			Task.WaitAll(m_tasks);

			//SystemSounds.Exclamation.Play();

			var p = new SoundPlayer(RuntimeInfo.RuntimeResources.SND_HINT);
			p.Play();

			Complete = true;

			NConsoleIO.Refresh();
		}

		private static int CompareResults(SearchResult x, SearchResult y)
		{
			float xSim = x?.Similarity ?? 0;
			float ySim = y?.Similarity ?? 0;

			if (xSim > ySim) {
				return -1;
			}

			if (xSim < ySim) {
				return 1;
			}

			if (x?.ExtendedResults.Count > y?.ExtendedResults.Count) {
				return -1;
			}

			if (x?.ExtendedInfo.Count > y?.ExtendedInfo.Count) {
				return -1;
			}

			return 0;
		}

		private static BaseSauceNaoClient GetSauceNaoClient()
		{
			// SauceNao API works without API key

			// bool apiConfigured = !string.IsNullOrWhiteSpace(SearchConfig.Config.SauceNaoAuth);
			//
			// if (apiConfigured) {
			// 	return new FullSauceNaoClient();
			// }
			// else {
			// 	return new AltSauceNaoClient();
			// }

			return new FullSauceNaoClient();
		}

		/// <summary>
		///     Starts search
		/// </summary>
		public void Start()
		{
			// Display config
			NConsole.WriteInfo(SearchConfig.Config);

			NConsole.WriteInfo("Temporary image url: {0}", m_imgUrl);

			m_monitor.Start();

			foreach (var thread in m_tasks) {
				thread.Start();
			}
		}

		private SearchResult GetOriginalImageResult()
		{
			var result = new SearchResult(Color.White, ORIGINAL_IMAGE_NAME, m_imgUrl)
			{
				Similarity = 100.0f,
				IsProcessed = true,
				IsImage = true
			};

			result.ExtendedInfo.Add(String.Format("Location: {0}", m_img));

			var fileFormat = FileOperations.ResolveFileType(m_img.FullName);

			const float magnitude = 1024f;

			double fileSizeMegabytes =
				Math.Round(FileOperations.GetFileSize(m_img.FullName) / magnitude / magnitude, 2);

			var dim = Images.GetImageDimensions(m_img.FullName);

			result.Width = dim.Width;
			result.Height = dim.Height;

			string infoStr = String.Format("Info: {0} ({1} MB) ({2})",
				m_img.Name, fileSizeMegabytes, fileFormat.Name);

			result.ExtendedInfo.Add(infoStr);

			return result;
		}


		private Task[] CreateSearchTasks()
		{
			// todo: improve, hacky :(

			var availableEngines = GetAllEngines()
				.Where(e => m_engines.HasFlag(e.Engine))
				.ToArray();

			m_results = new List<SearchResult>(availableEngines.Length + 1)
			{
				GetOriginalImageResult()
			};


			var threads = new List<Task>();

			foreach (var currentEngine in availableEngines) {

				var task = new Task(() => RunSearchTask(currentEngine));

				threads.Add(task);
			}

			return threads.ToArray();
		}

		private void RunSearchTask(ISearchEngine currentEngine)
		{
			var result = currentEngine.GetResult(m_imgUrl);

			//resultsCopy[iCopy] = result;
			m_results.Add(result);

			RunProcessingTask(result);

			// If the engine is priority, open its result in the browser
			if (SearchConfig.Config.PriorityEngines.HasFlag(currentEngine.Engine)) {
				Network.OpenUrl(result.Url);
			}

			//Update();

			// Sort results
			m_results.Sort(CompareResults);

			// Reload console UI
			//NConsoleIO.Refresh();
		}


		private static IEnumerable<ISearchEngine> GetAllEngines()
		{
			var engines = new ISearchEngine[]
			{
				//
				GetSauceNaoClient(),
				new IqdbClient(),
				new YandexClient(),
				new TraceMoeClient(),

				//
				new ImgOpsClient(),
				new GoogleImagesClient(),
				new TinEyeClient(),
				new BingClient(),
				new KarmaDecayClient()
			};

			return engines;
		}

		private static bool IsFileValid(string img)
		{
			if (String.IsNullOrWhiteSpace(img)) {
				return false;
			}

			if (!File.Exists(img)) {
				NConsole.WriteError("File does not exist: {0}", img);
				return false;
			}

			bool isImageType = FileOperations.ResolveFileType(img).Type == FileType.Image;

			if (!isImageType) {
				return NConsoleIO.ReadConfirm("File format is not recognized as a common image format. Continue?");
			}


			return true;
		}

		private static string Upload(string img, bool useImgur)
		{
			string imgUrl;

			if (useImgur) {
				try {
					UploadImgur();
				}
				catch (Exception e) {
					NConsole.WriteError("Error uploading with Imgur: {0}", e.Message);
					NConsole.WriteInfo("Using ImgOps instead");
					UploadImgOps();
				}
			}
			else {
				UploadImgOps();
			}


			void UploadImgur()
			{
				NConsole.WriteInfo("Using Imgur for image upload");
				var imgur = new ImgurClient();
				imgUrl = imgur.Upload(img);
			}

			void UploadImgOps()
			{
				NConsole.WriteInfo("Using ImgOps for image upload (2 hour cache)");
				var imgOps = new ImgOpsClient();
				imgUrl = imgOps.UploadTempImage(img, out _);
			}


			return imgUrl;
		}
	}
}
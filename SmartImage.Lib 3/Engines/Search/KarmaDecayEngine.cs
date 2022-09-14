﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Dom;

namespace SmartImage.Lib.Engines.Search
{
	public sealed class KarmaDecayEngine : WebContentSearchEngine
	{
		public KarmaDecayEngine() : base("http://karmadecay.com/search/?q=") { }

		#region Overrides of BaseSearchEngine

		public override SearchEngineOptions EngineOption => SearchEngineOptions.KarmaDecay;

		public override void Dispose() { }

		#endregion

		#region Overrides of WebContentSearchEngine

		protected override async Task<IList<INode>> GetNodesAsync(IDocument doc)
		{
			var results = doc.QuerySelectorAll("tr.result").Cast<INode>().ToList();

			return await Task.FromResult(results);
		}

		protected override async Task<SearchResultItem> ParseResultItemAsync(INode n, SearchResult r)
		{

			return default;
		}

		#endregion
	}
}
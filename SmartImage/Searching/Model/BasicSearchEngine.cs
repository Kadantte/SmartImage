#region

#endregion

using System;
using System.Drawing;

namespace SmartImage.Searching.Model
{
	public abstract class BasicSearchEngine : ISearchEngine
	{
		protected readonly string BaseUrl;

		protected BasicSearchEngine(string baseUrl)
		{
			BaseUrl = baseUrl;
		}

		

		public abstract SearchEngines Engine { get; }

		public abstract string Name { get; }

		public abstract Color Color { get; }

		public virtual SearchResult GetResult(string url)
		{
			string rawUrl = GetRawResultUrl(url);
			
			return new SearchResult(this, rawUrl);
		}

		public virtual string GetRawResultUrl(string url)
		{
			return BaseUrl + url;
		}
	}
}
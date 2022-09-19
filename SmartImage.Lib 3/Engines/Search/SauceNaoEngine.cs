﻿// ReSharper disable UnusedMember.Global

using System.Diagnostics;
using System.Json;
using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.XPath;
using Flurl.Http;
using Kantan.Net.Utilities;
using Kantan.Text;
using static Kantan.Diagnostics.LogCategories;
using JsonArray = System.Json.JsonArray;
using JsonObject = System.Json.JsonObject;

// ReSharper disable PossibleNullReferenceException

// ReSharper disable PropertyCanBeMadeInitOnly.Local
// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable PossibleMultipleEnumeration
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable ParameterTypeCanBeEnumerable.Local

namespace SmartImage.Lib.Engines.Search;

public sealed class SauceNaoEngine : ClientSearchEngine
{
	private const string BASE_URL = "https://saucenao.com/";

	private const string BASE_ENDPOINT = BASE_URL + "search.php";

	private const string BASIC_RESULT = $"{BASE_ENDPOINT}?url=";

	/*
	 * Excerpts adapted from https://github.com/Lazrius/SharpNao/blob/master/SharpNao.cs#L53
	 * https://github.com/luk1337/SauceNAO/blob/master/app/src/main/java/com/luk/saucenao/MainActivity.java
	 */

	public SauceNaoEngine(string authentication) : base(BASIC_RESULT, BASE_ENDPOINT)
	{
		Authentication = authentication;
	}

	public SauceNaoEngine() : this(null) { }

	public string Authentication { get; set; }

	public bool UsingAPI => !String.IsNullOrWhiteSpace(Authentication);

	public override SearchEngineOptions EngineOption => SearchEngineOptions.SauceNao;

	public override async Task<SearchResult> GetResultAsync(SearchQuery query, CancellationToken? token = null)
	{
		var result = await base.GetResultAsync(query, token);

		IEnumerable<SauceNaoDataResult> dataResults;

		try {
			if (UsingAPI) {
				dataResults = await GetAPIResultsAsync(query);
				Debug.WriteLine($"{Name} API key: {Authentication}");
			}
			else {
				dataResults = await GetWebResultsAsync(query);
			}
		}
		catch (Exception e) {
			result.ErrorMessage = e.Message;
			result.Status       = SearchResultStatus.Failure;
			return result;
		}

		if (!dataResults.Any()) {
			result.ErrorMessage = "Daily search limit (50) exceeded";
			result.Status       = SearchResultStatus.Cooldown;
			//return sresult;
			goto ret;
		}

		var imageResults = dataResults.Where(o => o != null)
		                              // .AsParallel()
		                              .Select((x) => ConvertToImageResult(x, result))
		                              .Where(o => o != null)
		                              .OrderByDescending(e => e.Similarity)
		                              .ToList();

		if (!imageResults.Any()) {
			// No good results
			//return sresult;
			goto ret;
		}

		result.Results.AddRange(imageResults);

		// result.Results[0] = (imageResults.First());

		// result.Url ??= imageResults.FirstOrDefault(x => x.Url != null)?.Url;

		ret:

		return result;
	}

	public override void Dispose()
	{
		base.Dispose();

	}

	private async Task<IEnumerable<SauceNaoDataResult>> GetWebResultsAsync(SearchQuery query)
	{
		Trace.WriteLine($"{Name}: | Parsing HTML", C_INFO);

		var docp = new HtmlParser();

		string         html     = null;
		IFlurlResponse response = null;

		try {
			response = await EndpointUrl.AllowHttpStatus().PostMultipartAsync(m =>
			{
				m.AddString("url", query.IsUrl ? query.Value : String.Empty);

				if (query.IsUrl) { }
				else if (query.IsFile) {
					m.AddFile("file", query.Value, fileName: "image.png");
				}

				return;
			});
			html = await response.GetStringAsync();
		}
		catch (FlurlHttpException e) {

			/*
			 * Daily Search Limit Exceeded.
			 * <IP>, your IP has exceeded the unregistered user's daily limit of 100 searches.
			 */

			if (e.StatusCode == (int) HttpStatusCode.TooManyRequests) {
				Trace.WriteLine($"On cooldown!", Name);
				return await Task.FromResult(Enumerable.Empty<SauceNaoDataResult>());
			}

			html = await e.GetResponseStringAsync();
		}

		var doc = await docp.ParseDocumentAsync(html);

		const string RESULT_NODE = "//div[@class='result']";

		var results = doc.Body.SelectNodes(RESULT_NODE);

		static SauceNaoDataResult Parse(INode result)
		{
			if (result == null) {
				return null;
			}

			const string HIDDEN_ID_VAL = "result-hidden-notification";

			if (result.TryGetAttribute("id") == HIDDEN_ID_VAL) {
				return null;
			}

			var resulttablecontent = result.FirstChild
			                               .FirstChild
			                               .FirstChild
			                               .ChildNodes[1];

			var resultmatchinfo      = resulttablecontent.FirstChild;
			var resultsimilarityinfo = resultmatchinfo.FirstChild;

			// Contains links
			var resultmiscinfo      = resultmatchinfo.ChildNodes[1];
			var resultcontent       = resulttablecontent.ChildNodes[1];
			var resultcontentcolumn = resultcontent.ChildNodes[1];
			// var resulttitle = resultcontent.ChildNodes[0];

			string link = null;

			var element = resultcontentcolumn.ChildNodes.GetElementsByTagName("a")
			                                 .FirstOrDefault(x => x.GetAttribute("href") != null);

			if (element != null) {
				link = element.GetAttribute("href");
			}

			if (resultmiscinfo != null) {
				link ??= resultmiscinfo.ChildNodes.GetElementsByTagName("a")
				                       .FirstOrDefault(x => x.GetAttribute("href") != null)?
				                       .GetAttribute("href");
			}

			//	//div[contains(@class, 'resulttitle')]
			//	//div/node()[self::strong]

			INode  resulttitle = resultcontent.ChildNodes[0];
			string rti         = resulttitle?.TextContent;

			// INode  resultcontentcolumn1 = resultcontent.ChildNodes[1];
			string rcci = resultcontentcolumn?.TextContent;

			var synonyms = new[] { "Creator: ", "Member: ", "Artist: " };

			string material1 = rcci?.SubstringAfter("Material: ");

			string creator1 = rcci;

			// creator1 = creator1.SubstringAfter("Creator: ");
			// resultcontentcolumn.GetNodes(true, (IElement element) => element.LocalName == "strong");

			// resultcontentcolumn.GetNodes(deep:true, predicate: (INode n)=>n.TryGetAttribute() )
			var t = resultcontentcolumn.ChildNodes[0].TextContent;

			// resultcontentcolumn.ChildNodes[1].TryGetAttribute("href");

			float similarity = Single.Parse(resultsimilarityinfo.TextContent.Replace("%", String.Empty));

			var dataResult = new SauceNaoDataResult
			{
				Urls       = new[] { link },
				Similarity = similarity,
				Creator    = creator1,
				Title      = rti,
				Material   = material1

			};

			return dataResult;

		}

		return results.Select(Parse).ToList();
	}

	private async Task<IEnumerable<SauceNaoDataResult>> GetAPIResultsAsync(SearchQuery url)
	{
		Trace.WriteLine($"{Name} | API");

		// var client = new HttpClient();

		const string dbIndex = "999";
		const string numRes  = "6";

		var values = new Dictionary<string, string>
		{
			{ "db", dbIndex },
			{ "output_type", "2" },
			{ "api_key", Authentication },
			{ "url", url.ToString() },
			{ "numres", numRes }
		};

		var content = new FormUrlEncodedContent(values);

		var res = await BASE_ENDPOINT.AllowAnyHttpStatus().PostAsync(content);
		var c   = await res.GetStringAsync();

		if (res.ResponseMessage.StatusCode == HttpStatusCode.Forbidden) {
			return null;
		}

		// Excerpts of code adapted from https://github.com/Lazrius/SharpNao/blob/master/SharpNao.cs

		const string KeySimilarity = "similarity";
		const string KeyUrls       = "ext_urls";
		const string KeyIndex      = "index_id";
		const string KeyCreator    = "creator";
		const string KeyCharacters = "characters";
		const string KeyMaterial   = "material";
		const string KeyResults    = "results";
		const string KeyHeader     = "header";
		const string KeyData       = "data";

		var jsonString = JsonValue.Parse(c);

		if (jsonString is JsonObject jsonObject) {
			var jsonArray = jsonObject[KeyResults];

			for (int i = 0; i < jsonArray.Count; i++) {
				var    header = jsonArray[i][KeyHeader];
				var    data   = jsonArray[i][KeyData];
				string obj    = header.ToString();
				obj          =  obj.Remove(obj.Length - 1);
				obj          += data.ToString().Remove(0, 1).Insert(0, ",");
				jsonArray[i] =  JsonValue.Parse(obj);

			}

			string json = jsonArray.ToString();

			var buffer      = new List<SauceNaoDataResult>();
			var resultArray = JsonValue.Parse(json);

			for (int i = 0; i < resultArray.Count; i++) {
				var   result     = resultArray[i];
				float similarity = Single.Parse(result[KeySimilarity]);

				string[] strings = result.ContainsKey(KeyUrls)
					                   ? (result[KeyUrls] as JsonArray)!
					                     .Select(j => j.ToString().CleanString())
					                     .ToArray()
					                   : null;

				var index = (SauceNaoSiteIndex) Int32.Parse(result[KeyIndex].ToString());

				var item = new SauceNaoDataResult
				{
					Urls       = strings,
					Similarity = similarity,
					Index      = index,
					Creator    = result.TryGetKeyValue(KeyCreator)?.ToString().CleanString(),
					Character  = result.TryGetKeyValue(KeyCharacters)?.ToString().CleanString(),
					Material   = result.TryGetKeyValue(KeyMaterial)?.ToString().CleanString()
				};

				buffer.Add(item);
			}

			return await Task.FromResult(buffer.ToArray());
		}

		return null;
	}

	private static SearchResultItem ConvertToImageResult(SauceNaoDataResult sn, SearchResult r)
	{
		string url = sn.Urls?.FirstOrDefault(u => u != null);

		string siteName = sn.Index != 0 ? sn.Index.ToString() : null;

		var imageResult = new SearchResultItem(r)
		{
			Url         = url,
			Similarity  = MathF.Round(sn.Similarity, 2),
			Description = Strings.NormalizeNull(sn.WebsiteTitle),
			Artist      = Strings.NormalizeNull(sn.Creator),
			Source      = Strings.NormalizeNull(sn.Material),
			Character   = Strings.NormalizeNull(sn.Character),
			Site        = Strings.NormalizeNull(siteName)
		};

		return imageResult;

	}

	/// <summary>
	/// Origin result
	/// </summary>
	private class SauceNaoDataResult
	{
		/// <summary>
		///     The url(s) where the source is from. Multiple will be returned if the exact same image is found in multiple places
		/// </summary>
		public string[] Urls { get; internal set; }

		/// <summary>
		///     The search index of the image
		/// </summary>
		public SauceNaoSiteIndex Index { get; internal set; }

		/// <summary>
		///     How similar is the image to the one provided (Percentage)?
		/// </summary>
		public float Similarity { get; internal set; }

		public string WebsiteTitle { get; internal set; }

		public string Title { get; internal set; }

		public string Character { get; internal set; }

		public string Material { get; internal set; }

		public string Creator { get; internal set; }
	}
}

public enum SauceNaoSiteIndex
{
	DoujinshiMangaLexicon = 3,
	Pixiv                 = 5,
	PixivArchive          = 6,
	NicoNicoSeiga         = 8,
	Danbooru              = 9,
	Drawr                 = 10,
	Nijie                 = 11,
	Yandere               = 12,
	OpeningsMoe           = 13,
	FAKKU                 = 16,
	nHentai               = 18,
	TwoDMarket            = 19,
	MediBang              = 20,
	AniDb                 = 21,
	IMDB                  = 23,
	Gelbooru              = 25,
	Konachan              = 26,
	SankakuChannel        = 27,
	AnimePictures         = 28,
	e621                  = 29,
	IdolComplex           = 30,
	BcyNetIllust          = 31,
	BcyNetCosplay         = 32,
	PortalGraphics        = 33,
	DeviantArt            = 34,
	Pawoo                 = 35,
	MangaUpdates          = 36,

	//
	ArtStation = 39,

	FurAffinity = 40,
	Twitter     = 41
}
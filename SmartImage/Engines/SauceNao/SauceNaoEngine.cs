using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Json;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using RestSharp;
using SimpleCore.Utilities;
using SmartImage.Core;
using SmartImage.Searching;
using JsonObject = System.Json.JsonObject;

#nullable enable
// ReSharper disable InconsistentNaming
// ReSharper disable ParameterTypeCanBeEnumerable.Local
#pragma warning disable HAA0502, HAA0601, HAA0102, HAA0401
namespace SmartImage.Engines.SauceNao
{
	// https://github.com/RoxasShadow/SauceNao-Windows
	// https://github.com/LazDisco/SharpNao

	// NOTE: It seems that the SauceNao API works regardless of whether or not an API key is used

	/// <summary>
	/// SauceNao API client
	/// </summary>
	public sealed class SauceNaoEngine : BasicSearchEngine
	{
		private const string BASE_URL = "https://saucenao.com/";

		private const string BASIC_RESULT = "https://saucenao.com/search.php?url=";

		public override string Name => "SauceNao";

		public override SearchEngineOptions Engine => SearchEngineOptions.SauceNao;

		public override Color Color => Color.OrangeRed;
		
		public override float? FilterThreshold => 70.00F;

		private struct SauceNaoSimpleResult : ISearchResult
		{
			public string? Caption    { get; set; }
			public bool   Filter     { get; set; }
			public string  Url        { get; set; }
			public float?  Similarity { get; set; }
			public int?    Width      { get; set; }
			public int?    Height     { get; set; }

			public SauceNaoSimpleResult(string? title, string url, float? similarity)
			{
				Caption    = title;
				Url        = url;
				Similarity = similarity;
				Width      = null;
				Height     = null;
				Filter     = false;//set later
			}

			public override string ToString()
			{
				return $"{Caption} {Url} {Similarity}";
			}
		}


		private const string ENDPOINT = BASE_URL + "search.php";


		private readonly string m_apiKey;

		private readonly RestClient m_client;

		private SauceNaoEngine(string apiKey) : base(BASIC_RESULT)
		{
			m_client = new RestClient(ENDPOINT);
			m_apiKey = apiKey;
		}

		public SauceNaoEngine() : this(SearchConfig.Config.SauceNaoAuth) { }

		private ISearchResult[] ConvertResults(SauceNaoDataResult[] results)
		{
			var rg = new List<ISearchResult>();

			foreach (var sn in results) {
				if (sn.Url != null) {
					var url = sn.Url.FirstOrDefault(u => u != null);
					var x   = new SauceNaoSimpleResult(sn.WebsiteTitle, url, sn.Similarity);
					x.Filter = x.Similarity < FilterThreshold;
					rg.Add(x);
				}
			}

			return rg.ToArray();
		}

		public override FullSearchResult GetResult(string url)
		{
			FullSearchResult result = base.GetResult(url);

			try {
				var orig = GetResults(url);

				var sn = orig
					.OrderByDescending(r => r.Similarity)
					.ToArray();

				var extended = ConvertResults(sn);

				var best = extended
					.Where(e => e.Url != null)
					.OrderByDescending(e => e.Similarity)
					.First();
				
				// Copy
				result.Url        = best.Url;
				result.Similarity = best.Similarity;
				result.Caption    = best.Caption;
				result.Filter     = best.Filter;

				result.AddExtendedResults(extended);

				if (!string.IsNullOrWhiteSpace(m_apiKey)) {
					result.ExtendedInfo.Add("Using API");
				}

			}
			catch (Exception e) {

				result.ExtendedInfo.Add(e.StackTrace);
			}


			return result;
		}
		
		
		private IEnumerable<SauceNaoDataResult>? GetResults(string url)
		{

			var req = new RestRequest();
			req.AddQueryParameter("db", "999");
			req.AddQueryParameter("output_type", "2");
			req.AddQueryParameter("numres", "16");
			req.AddQueryParameter("api_key", m_apiKey);
			req.AddQueryParameter("url", url);

			var res = m_client.Execute(req);

			string c = res.Content;

			return ReadResults(c);
		}


		private static IEnumerable<SauceNaoDataResult>? ReadResults(string js)
		{
			// todo: rewrite this using Newtonsoft

			// From https://github.com/Lazrius/SharpNao/blob/master/SharpNao.cs

			var jsonString = JsonValue.Parse(js);

			if (jsonString is JsonObject jsonObject) {
				var jsonArray = jsonObject["results"];

				for (int i = 0; i < jsonArray.Count; i++) {
					var    header = jsonArray[i]["header"];
					var    data   = jsonArray[i]["data"];
					string obj    = header.ToString();
					obj          =  obj.Remove(obj.Length - 1);
					obj          += data.ToString().Remove(0, 1).Insert(0, ",");
					jsonArray[i] =  JsonValue.Parse(obj);
				}

				string json = jsonArray.ToString();
				json = json.Insert(json.Length - 1, "}").Insert(0, "{\"results\":");

				using var stream =
					JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json),
						XmlDictionaryReaderQuotas.Max);
				var serializer = new DataContractJsonSerializer(typeof(SauceNaoDataResponse));
				var result     = serializer.ReadObject(stream) as SauceNaoDataResponse;
				stream.Dispose();

				if (result is null)
					return null;

				foreach (var t in result.Results) {
					t.WebsiteTitle = Strings.SplitPascalCase(t.Index.ToString());
				}

				return result.Results;
			}

			return null;
		}
	}
}
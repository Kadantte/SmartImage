using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using RestSharp;
using RestSharp.Serialization.Json;

namespace SmartImage.Utilities
{
	// https://github.com/Auo/ImgurSharp

	public sealed class Imgur
	{
		private readonly string m_apiKey;

		private Imgur(string apiKey)
		{
			m_apiKey = apiKey;
		}
		
		public static Imgur Value { get; private set; } = new Imgur(Config.ImgurAuth.Item1);
		
		public string Upload(string path)
		{
			using var w = new WebClient();
			w.Headers.Add("Authorization: Client-ID " + m_apiKey);
			var values = new NameValueCollection
			{
				{"image", Convert.ToBase64String(File.ReadAllBytes(@path))}
			};

			string response =
				System.Text.Encoding.UTF8.GetString(w.UploadValues("https://api.imgur.com/3/upload", values));
			//Console.WriteLine(response);

			var res = new RestResponse();
			res.Content = response;

			//dynamic dynObj = JsonConvert.DeserializeObject(response);
			//return dynObj.data.link;

			var des = new JsonDeserializer();
			return des.Deserialize<ResponseRootObject<Image>>(res).Data.Link;
		}
	}
}
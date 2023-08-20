﻿using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flurl.Http;
using Jint.Native.Json;
using SmartImage.Lib.Results;
using JsonSerializer = System.Text.Json.JsonSerializer;

#pragma warning disable CS0649

// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable CollectionNeverUpdated.Local

namespace SmartImage.Lib.Engines.Impl.Search;

public sealed class RepostSleuthEngine : BaseSearchEngine, IHttpClient
{
	public RepostSleuthEngine() : base("https://repostsleuth.com/search?url=")
	{
		Timeout = TimeSpan.FromSeconds(4.5);
	}

	#region

	public string EndpointUrl => "https://api.repostsleuth.com/image";

	#endregion

	public override SearchEngineOptions EngineOption => SearchEngineOptions.RepostSleuth;

	public override void Dispose() { }

	#region Overrides of BaseSearchEngine

	public override async Task<SearchResult> GetResultAsync(SearchQuery query, CancellationToken token = default)
	{
		var sr = await base.GetResultAsync(query, token);

		Root obj = null;

		try {
			var s = await SearchClient.Client.Request(EndpointUrl).SetQueryParams(new
			{

				filter               = true,
				url                  = query.Upload,
				same_sub             = false,
				filter_author        = true,
				only_older           = false,
				include_crossposts   = false,
				meme_filter          = false,
				target_match_percent = 90,
				filter_dead_matches  = false,
				target_days_old      = 0
			}).GetStringAsync(cancellationToken: token);

			var js = new JsonSerializerOptions(JsonSerializerDefaults.Web)
			{
				IncludeFields = true
			};
			obj = JsonSerializer.Deserialize<Root>(s, js);
		}
		catch (FlurlHttpException e) {
			sr.ErrorMessage = e.Message;
			sr.Status       = SearchResultStatus.Unavailable;

			goto ret;
		}

		if (!obj.matches.Any()) {
			sr.Status = SearchResultStatus.NoResults;
			goto ret;
		}

		SearchResultItem Func(Match m)
			=> new(sr)
			{
				Similarity = m.hamming_match_percent,
				Artist     = m.post.author,
				Site       = m.post.subreddit,
				Url        = m.post.url,
				Title      = m.post.title,
				Time       = DateTimeOffset.FromUnixTimeSeconds((long) m.post.created_at).LocalDateTime
			};

		foreach (SearchResultItem sri in obj.matches.Select(Func)) {
			sr.Results.Add(sri);
		}

		ret:
		sr.Update();
		return sr;
	}

	#endregion

	#region Objects

	private class ClosestMatch
	{
		public int    hamming_distance;
		public double annoy_distance;
		public double hamming_match_percent;
		public int    hash_size;
		public string searched_url;
		public Post   post;
		public int    title_similarity;
	}

	private class Match
	{
		public int    hamming_distance;
		public double annoy_distance;
		public double hamming_match_percent;
		public int    hash_size;
		public string searched_url;
		public Post   post;
		public int    title_similarity;
	}

	private class Post
	{
		public string post_id;
		public string url;
		public object shortlink;
		public string perma_link;
		public string title;
		public string dhash_v;
		public string dhash_h;
		public double created_at;
		public string author;
		public string subreddit;
	}

	private class Root
	{
		public object         meme_template;
		public ClosestMatch   closest_match;
		public string         checked_url;
		public object         checked_post;
		public SearchSettings search_settings;
		public SearchTimes    search_times;
		public List<Match>    matches;
	}

	private class SearchSettings
	{
		public bool   filter_crossposts;
		public bool   filter_same_author;
		public bool   only_older_matches;
		public bool   filter_removed_matches;
		public bool   filter_dead_matches;
		public int    max_days_old;
		public bool   same_sub;
		public int    max_matches;
		public object target_title_match;
		public string search_scope;
		public bool   check_title;
		public int    max_depth;
		public bool   meme_filter;
		public int    target_annoy_distance;
		public int    target_meme_match_percent;
		public int    target_match_percent;
	}

	private class SearchTimes
	{
		public double pre_annoy_filter_time;
		public double index_search_time;
		public double meme_filter_time;
		public double meme_detection_time;
		public double set_match_post_time;
		public double remove_duplicate_time;
		public double set_match_hamming;
		public double image_search_api_time;
		public double filter_removed_posts_time;
		public double filter_deleted_posts_time;
		public double set_meme_hash_time;
		public double set_closest_meme_hash_time;
		public double distance_filter_time;
		public double get_closest_match_time;
		public double total_search_time;
		public double total_filter_time;
		public double set_title_similarity_time;
	}

	#endregion
}

public class NonPublicMembersConverter<T> : JsonConverter<T> where T : class
{
	public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		T instance = (T) Activator.CreateInstance(typeToConvert, nonPublic: true);

		while (reader.Read()) {
			if (reader.TokenType == JsonTokenType.EndObject) {
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName) {
				throw new JsonException();
			}

			string propertyName = reader.GetString();

			PropertyInfo propertyInfo =
				typeToConvert.GetProperty(propertyName,
				                          BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

			if (propertyInfo != null && propertyInfo.CanWrite) {
				reader.Read(); // Move to the property value
				object value = JsonSerializer.Deserialize(ref reader, propertyInfo.PropertyType, options);
				propertyInfo.SetValue(instance, value);
			}
			else {
				reader.Skip();
			}
		}

		return instance;
	}

	public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
	{
		JsonSerializer.Serialize(writer, value, options);
	}
}
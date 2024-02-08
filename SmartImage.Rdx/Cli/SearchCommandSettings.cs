﻿// Deci SmartImage.Rdx SearchCommandSettings.cs
// $File.CreatedYear-$File.CreatedMonth-26 @ 0:56

using System.ComponentModel;
using System.Globalization;
using SmartImage.Lib;
using SmartImage.Lib.Engines;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SmartImage.Rdx.Cli;

internal sealed class SearchCommandSettings : CommandSettings
{

	[Description("Query")]
	[CommandArgument(0, "[query]")]
	public string? Query { get; init; }

	[CommandOption("-e|--search-engines")]
	[DefaultValue(SearchConfig.SE_DEFAULT)]

	// [TypeConverter(typeof(FlagsEnumTypeConverter<SearchEngineOptions>))]
	public SearchEngineOptions SearchEngines { get; init; }

	[CommandOption("-p|--priority-engines")]
	[DefaultValue(SearchConfig.PE_DEFAULT)]

	// [TypeConverter(typeof(FlagsEnumTypeConverter<SearchEngineOptions>))]
	public SearchEngineOptions PriorityEngines { get; init; }

	[CommandOption("-a|--autosearch")]
	[DefaultValue(SearchConfig.AUTOSEARCH_DEFAULT)]
	public bool AutoSearch { get; init; }

	[CommandOption("-x|--interactive")]
	[DefaultValue(false)]
	public bool Interactive { get; init; }

	[CommandOption("-f|--result-format")]
	[DefaultValue(ResultGridFormat.Name
	              | ResultGridFormat.Similarity
	              | ResultGridFormat.Url)]
	public ResultGridFormat Format { get; init; }

	public override ValidationResult Validate()
	{
		var result = base.Validate();

		if (!SearchQuery.IsValidSourceType(Query)) {
			return ValidationResult.Error($"Invalid query");
		}

		return result;
	}

}
﻿// <auto-generated />
//
// To parse this JSON data, add NuGet 'Newtonsoft.Json' then do:
//
//    using openHAbot.openhab;
//
//    var haBotResponse = HaBotResponse.FromJson(jsonString);

namespace openHAbot.openhab
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public partial class HaBotResponse
    {
        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("query")]
        public string Query { get; set; }

        [JsonProperty("answer")]
        public string Answer { get; set; }

        [JsonProperty("hint")]
        public string Hint { get; set; }

        [JsonProperty("intent")]
        public Intent Intent { get; set; }

        [JsonProperty("matchedItemNames")]
        public List<string> MatchedItemNames { get; set; }

        [JsonProperty("card")]
        public Card Card { get; set; }
    }

    public partial class Card
    {
        [JsonProperty("uid")]
        public Guid Uid { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("subtitle")]
        public string Subtitle { get; set; }

        [JsonProperty("objects")]
        public List<string> Objects { get; set; }

        [JsonProperty("locations")]
        public List<string> Locations { get; set; }

        [JsonProperty("tags")]
        public List<object> Tags { get; set; }

        [JsonProperty("bookmarked")]
        public bool Bookmarked { get; set; }

        [JsonProperty("notReuseableInChat")]
        public bool NotReuseableInChat { get; set; }

        [JsonProperty("addToDeckDenied")]
        public bool AddToDeckDenied { get; set; }

        [JsonProperty("ephemeral")]
        public bool Ephemeral { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("component")]
        public string Component { get; set; }

        [JsonProperty("config")]
        public CardConfig Config { get; set; }

        [JsonProperty("slots")]
        public Slots Slots { get; set; }
    }

    public partial class CardConfig
    {
        [JsonProperty("bigger")]
        public bool Bigger { get; set; }
    }

    public partial class Slots
    {
        [JsonProperty("right")]
        public List<Right> Right { get; set; }
    }

    public partial class Right
    {
        [JsonProperty("component")]
        public string Component { get; set; }

        [JsonProperty("config")]
        public RightConfig Config { get; set; }
    }

    public partial class RightConfig
    {
        [JsonProperty("item")]
        public string Item { get; set; }
    }

    public partial class Intent
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("entities")]
        public Entities Entities { get; set; }
    }

    public partial class Entities
    {
        [JsonProperty("location")]
        public string Location { get; set; }

        [JsonProperty("object")]
        public string Object { get; set; }
    }

    public partial class HaBotResponse
    {
        public static HaBotResponse FromJson(string json) => JsonConvert.DeserializeObject<HaBotResponse>(json, openHAbot.openhab.Converter.Settings);
    }

    public static class Serialize
    {
        public static string ToJson(this HaBotResponse self) => JsonConvert.SerializeObject(self, openHAbot.openhab.Converter.Settings);
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }
}

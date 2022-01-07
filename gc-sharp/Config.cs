using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace gc
{
    internal class Config
    {
        [JsonPropertyName("db")]
        public DB DB { get; set; }
        [JsonPropertyName("files")]
        public Files Files { get; set; }
        [JsonPropertyName("debug")]
        public bool Debug { get; set; }
    }

    internal class DB
    {
        [JsonPropertyName("host")]
        public string Host { get; set; }
        [JsonPropertyName("port")]
        public string Port { get; set; }
        [JsonPropertyName("user")]
        public string User { get; set; }
        [JsonPropertyName("password")]
        public string Password { get; set; }
        [JsonPropertyName("db_name")]
        public string DBName { get; set; }
    }

    internal class Files
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }
        [JsonPropertyName("table")]
        public string Table { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("SimpleKillFeed Discord", "D3AD_SHOT", "1.0.4")]
    [Description("Forwards SimpleKillFeed kill messages to a Discord channel using a webhook.")]
    public class SimpleKillFeedDiscord : CovalencePlugin
    {
        private ConfigData _config;

        // Called by SimpleKillFeed:
        // Interface.Call("OnKillFeedMessageReceived", text);
        private void OnKillFeedMessageReceived(string text)
        {
            if (_config == null || !_config.Enabled) return;
            if (string.IsNullOrWhiteSpace(_config.WebhookUrl)) return;
            if (string.IsNullOrWhiteSpace(text)) return;

            string msg = text;

            if (_config.StripColorTags)
                msg = StripRustColorTags(msg);

            msg = msg.Replace("\n", " ").Replace("\r", " ").Trim();
            if (msg.Length == 0) return;

            // Discord max content length is 2000 chars
            if (msg.Length > 2000)
                msg = msg.Substring(0, 1997) + "...";

            if (!string.IsNullOrWhiteSpace(_config.Prefix))
                msg = $"{_config.Prefix}{msg}";

            if (_config.CooldownSeconds > 0 && !CanSendNow())
                return;

            SendToDiscord(msg);
        }

        #region Discord

        private void SendToDiscord(string content)
        {
            var payload = new WebhookPayload
            {
                content = content,
                username = string.IsNullOrWhiteSpace(_config.Username) ? null : _config.Username,
                avatar_url = string.IsNullOrWhiteSpace(_config.AvatarUrl) ? null : _config.AvatarUrl
            };

            string json = JsonConvert.SerializeObject(payload);

            webrequest.Enqueue(
                _config.WebhookUrl,
                json,
                (code, response) =>
                {
                    if (code == 204 || code == 200) return;

                    if (_config.LogFailures)
                        PrintWarning($"Discord webhook failed. HTTP {code}. Response: {response}");
                },
                this,
                RequestMethod.POST,
                new Dictionary<string, string>
                {
                    ["Content-Type"] = "application/json"
                }
            );
        }

        private class WebhookPayload
        {
            public string content;
            public string username;
            public string avatar_url;
        }

        #endregion

        #region Cooldown

        private DateTime _nextAllowedSendUtc = DateTime.MinValue;

        private bool CanSendNow()
        {
            var now = DateTime.UtcNow;
            if (now < _nextAllowedSendUtc) return false;

            _nextAllowedSendUtc = now.AddSeconds(_config.CooldownSeconds);
            return true;
        }

        #endregion

        #region Helpers

        private static string StripRustColorTags(string input)
        {
            // Removes <color=...> and </color>
            return Regex.Replace(input, @"<\/?color[^>]*>", "", RegexOptions.IgnoreCase);
        }

        #endregion

        #region Config

        private class ConfigData
        {
            [JsonProperty("Enabled")]
            public bool Enabled = true;

            [JsonProperty("Discord Webhook URL")]
            public string WebhookUrl = "";

            [JsonProperty("Webhook Username")]
            public string Username = "Kill Feed";

            [JsonProperty("Webhook Avatar URL")]
            public string AvatarUrl = "";

            [JsonProperty("Message Prefix")]
            public string Prefix = "";

            [JsonProperty("Strip <color> tags")]
            public bool StripColorTags = true;

            [JsonProperty("Cooldown Seconds (0 = no cooldown)")]
            public int CooldownSeconds = 1;

            [JsonProperty("Log webhook failures")]
            public bool LogFailures = true;
        }

        protected override void LoadDefaultConfig()
        {
            _config = new ConfigData();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<ConfigData>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                PrintWarning("Config error, generating new config.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        #endregion
    }
}

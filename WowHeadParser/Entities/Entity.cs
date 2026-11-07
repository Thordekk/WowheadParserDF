/*
 * Base class and SQL staging support for the existing entity parsers.
 */
using Sql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using WOWSharp.Community;
using WOWSharp.Community.Wow;

namespace WowHeadParser.Entities
{
    public class Entity
    {
        private static readonly Regex EntityUrlPattern = new Regex(@"/(item|npc|object|quest|zone)=(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public const string StagingSchemaSql = @"-- WowheadParser safe staging table. Raw Wowhead data is preserved; no TrinityCore game tables are modified.
CREATE TABLE IF NOT EXISTS `wowhead_parser_stage` (
  `locale` VARCHAR(8) NOT NULL,
  `category` VARCHAR(16) NOT NULL,
  `entity_id` INT UNSIGNED NOT NULL,
  `source_version` VARCHAR(32) NOT NULL,
  `source_url` VARCHAR(512) NOT NULL,
  `captured_at_utc` DATETIME NOT NULL,
  `raw_payload` LONGTEXT NOT NULL,
  `legacy_sql` LONGTEXT NOT NULL,
  PRIMARY KEY (`locale`, `category`, `entity_id`),
  KEY `idx_wowhead_parser_stage_version` (`source_version`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

";

        public Entity()
        {
            m_hasZoneData = false;
            isError = false;
            webClient = null;
        }

        public static string GetWowheadBaseUrl()
        {
            if (m_baseWowheadUrl == "")
                ReloadWowheadBaseUrl();
            return m_baseWowheadUrl;
        }

        public static void ReloadWowheadBaseUrl()
        {
            m_baseWowheadUrl = "https://" + Properties.Settings.Default.wowheadLocale + ".wowhead.com";
        }

        public virtual String GetWowheadUrl() { return ""; }
        public virtual String GetVersion() { return Properties.Settings.Default.version; }
        public virtual List<Entity> GetIdsFromZone(String zoneId, String zoneHtml) { return new List<Entity>(); }
        public virtual bool ParseSingleJson(int id = 0) { return false; }
        public virtual String GetSQLRequest() { return ""; }

        /// <summary>
        /// Produces an INSERT into an isolated staging table in the selected MySQL
        /// database. The page payload is kept verbatim for later, reviewed mapping.
        /// </summary>
        public string GetStagingSQL(string legacySql)
        {
            string url = GetWowheadUrl();
            Match match = EntityUrlPattern.Match(url ?? string.Empty);
            if (!match.Success)
                return string.Empty;

            string payload;
            if (!Tools.TryTakeCapturedResponse(url, out payload) || string.IsNullOrEmpty(payload))
                return string.Empty;

            int entityId;
            if (!Int32.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out entityId))
                return string.Empty;
            return CreateStagingSQL(match.Groups[1].Value.ToLowerInvariant(), entityId, url, payload, legacySql);
        }

        public static string CreateStagingSQL(string category, int entityId, string url, string payload, string legacySql)
        {
            if (string.IsNullOrWhiteSpace(category) || category.Length > 16 || entityId <= 0 ||
                string.IsNullOrEmpty(url) || payload == null)
                return string.Empty;
            string locale = Properties.Settings.Default.wowheadLocale ?? "www";
            return "INSERT INTO `wowhead_parser_stage` " +
                "(`locale`,`category`,`entity_id`,`source_version`,`source_url`,`captured_at_utc`,`raw_payload`,`legacy_sql`) VALUES (" +
                SqlText(locale) + "," + SqlText(category) + "," + entityId.ToString(CultureInfo.InvariantCulture) + "," +
                SqlText(Properties.Settings.Default.version ?? string.Empty) + "," + SqlText(url) + ",UTC_TIMESTAMP()," + SqlText(payload) + "," +
                SqlText(legacySql ?? string.Empty) + ") ON DUPLICATE KEY UPDATE " +
                "`source_version`=VALUES(`source_version`),`source_url`=VALUES(`source_url`)," +
                "`captured_at_utc`=VALUES(`captured_at_utc`),`raw_payload`=VALUES(`raw_payload`)," +
                "`legacy_sql`=VALUES(`legacy_sql`);" + Environment.NewLine;
        }

        private static string SqlText(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            var hex = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
                hex.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            return "CONVERT(X'" + hex + "' USING utf8mb4)";
        }

        public void SetIsError() { isError = true; }

        public bool IsCheckboxChecked(String checkboxName)
        {
            for (int i = 0; i < Properties.Settings.Default.checkedList.Count; ++i)
                if (Properties.Settings.Default.checkedList[i] == checkboxName)
                    return true;
            return false;
        }

        protected bool isError;
        protected bool m_hasZoneData;
        static private String m_baseWowheadUrl = "";
        public HttpClient webClient;
        public WowClient WowClient;
        public ICacheManager CacheManager;
    }
}

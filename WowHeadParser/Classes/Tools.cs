/*
 * Created by Traesh for AshamaneProject (https://github.com/AshamaneProject)
 */
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using WOWSharp.Community;

namespace WowHeadParser
{

    public class CurrencyTypes
    {
        public uint ID { get; set; }
        public string Name_lang { get; set; }
        public string Description_lang { get; set; }
        public int CategoryID { get; set; }
        public int InventoryIconFileID { get; set; }
        public int SpellWeight { get; set; }
        public int SpellCategory { get; set; }
        public int MaxQty { get; set; }
        public int MaxEarnablePerWeek { get; set; }
        public int Quality { get; set; }
        public int FactionID { get; set; }
        public int ItemGroupSoundsID { get; set; }
        public int XpQuestDifficulty { get; set; }
        public int AwardConditionID { get; set; }
        public int MaxQtyWorldStateID { get; set; }
        public int RechargingAmountPerCycle { get; set; }
        public int RechargingCycleDurationMS { get; set; }
        public int Flags0 { get; set; }
        public int Flags1 { get; set; }

        public bool HasPrecision() { return (Flags0 & (int)CurrencyFlags.CURRENCY_FLAG_HIGH_PRECISION) != 0; }
        public bool HasSeasonCount() { return (Flags0 & (int)CurrencyFlags.CURRENCY_FLAG_HAS_SEASON_COUNT) != 0; }
        public float GetPrecision() { return HasPrecision() ? 100.0f : 1.0f; }
    }

    public class CurrencyTypesClassMap : ClassMap<CurrencyTypes>
    {
        public CurrencyTypesClassMap()
        {
            Map(m => m.ID).Name("ID");
            Map(m => m.Name_lang).Name("Name_lang");
            Map(m => m.Description_lang).Name("Description_lang");
            Map(m => m.CategoryID).Name("CategoryID");
            Map(m => m.InventoryIconFileID).Name("InventoryIconFileID");
            Map(m => m.SpellWeight).Name("SpellWeight");
            Map(m => m.SpellCategory).Name("SpellCategory");
            Map(m => m.MaxQty).Name("MaxQty");
            Map(m => m.MaxEarnablePerWeek).Name("MaxEarnablePerWeek");
            Map(m => m.Quality).Name("Quality");
            Map(m => m.FactionID).Name("FactionID");
            Map(m => m.ItemGroupSoundsID).Name("ItemGroupSoundsID");
            Map(m => m.XpQuestDifficulty).Name("XpQuestDifficulty");
            Map(m => m.AwardConditionID).Name("AwardConditionID");
            Map(m => m.MaxQtyWorldStateID).Name("MaxQtyWorldStateID");
            Map(m => m.RechargingAmountPerCycle).Name("RechargingAmountPerCycle");
            Map(m => m.RechargingCycleDurationMS).Name("RechargingCycleDurationMS");
            Map(m => m.Flags0).Name("Flags[0]");
            Map(m => m.Flags1).Name("Flags[1]");
        }
    }

    public class ItemExtendedCostEntry
    {
        public uint ID { get; set; }
        public int RequiredArenaRating { get; set; }
        public int ArenaBracket { get; set; }
        public int Flags { get; set; }
        public int MinFactionID { get; set; }
        public int MinReputation { get; set; }
        public int RequiredAchievement { get; set; }
        public List<uint> ItemID { get; set; }
        public List<uint> ItemCount { get; set; }
        public List<uint> CurrencyID{ get; set; }
        public List<uint> CurrencyCount { get; set; }
    }

    public class ItemExtendedCostEntryClassMap : ClassMap<ItemExtendedCostEntry>
    {
        public ItemExtendedCostEntryClassMap()
        {
            Map(m => m.ID).Name("ID");
            Map(m => m.RequiredArenaRating).Name("RequiredArenaRating");
            Map(m => m.ArenaBracket).Name("ArenaBracket");
            Map(m => m.Flags).Name("Flags");
            Map(m => m.MinFactionID).Name("MinFactionID");
            Map(m => m.MinReputation).Name("MinReputation");
            Map(m => m.RequiredAchievement).Name("RequiredAchievement");
            Map(m => m.ItemID).Convert(item =>
            {
                var list = new List<uint>();
                for (var i = 0; i < 5; i++)
                {
                    list.Add(uint.Parse(item.Row.GetField($"ItemID[{i}]")));
                }
                return list;
            });
            Map(m => m.ItemCount).Convert(item =>
            {
                var list = new List<uint>();
                for (var i = 0; i < 5; i++)
                {
                    list.Add(uint.Parse(item.Row.GetField($"ItemCount[{i}]")));
                }
                return list;
            });
            Map(m => m.CurrencyID).Convert(item =>
            {
                var list = new List<uint>();
                for (var i = 0; i < 5; i++)
                {
                    list.Add(uint.Parse(item.Row.GetField($"CurrencyID[{i}]")));
                }
                return list;
            });
            Map(m => m.CurrencyCount).Convert(item =>
            {
                var list = new List<uint>();
                for (var i = 0; i < 5; i++)
                {
                    list.Add(uint.Parse(item.Row.GetField($"CurrencyCount[{i}]")));
                }
                return list;
            });
        }
    }

    //public struct ItemExtendedCostEntry
    //{
    //    public UInt32 ID;
    //    public List<UInt32> RequiredItem;               // required item id
    //    public List<UInt32> RequiredCurrencyCount;      // required curency count
    //    public List<UInt16> RequiredItemCount;          // required count of 1st item
    //    public UInt16 RequiredPersonalArenaRating;      // required personal arena rating
    //    public List<UInt16> RequiredCurrency;           // required curency id
    //    public Byte RequiredArenaSlot;                  // arena slot restrictions (min slot value)
    //    public Byte RequiredFactionId;
    //    public Byte RequiredFactionStanding;
    //    public Byte RequirementFlags;
    //    public Byte RequiredAchievement;
    //};

    public struct PlayerConditionEntry
    {
        public Int32 ID;                                                      // 0
        public Int32 Flags;                                                   // 1
        public Int32 MinLevel;                                                // 2
        public Int32 MaxLevel;                                                // 3
        public Int32 RaceMask;                                                // 4
        public Int32 ClassMask;                                               // 5
        public Int32 Gender;                                                  // 6
        public Int32 NativeGender;                                            // 7
        public List<Int32> SkillID;                                           // 8-11
        public List<Int32> MinSkill;                                          // 12-15
        public List<Int32> MaxSkill;                                          // 16-19
        public Int32 SkillLogic;                                              // 20
        public Int32 LanguageID;                                              // 21
        public Int32 MinLanguage;                                             // 22
        public Int32 MaxLanguage;                                             // 23
        public List<Int32> MinFactionID;                                      // 24-26
        public Int32 MaxFactionID;                                            // 27
        public List<Int32> MinReputation;                                     // 28-30
        public Int32 MaxReputation;                                           // 31
        public Int32 ReputationLogic;                                         // 32
        public Int32 MinPVPRank;                                              // 33
        public Int32 MaxPVPRank;                                              // 34
        public Int32 PvpMedal;                                                // 35
        public Int32 PrevQuestLogic;                                          // 36
        public List<Int32> PrevQuestID;                                       // 37-40
        public Int32 CurrQuestLogic;                                          // 41
        public List<Int32> CurrQuestID;                                       // 42-45
        public Int32 CurrentCompletedQuestLogic;                              // 46
        public List<Int32> CurrentCompletedQuestID;                           // 47-50
        public Int32 SpellLogic;                                              // 51
        public List<Int32> SpellID;                                           // 52-55
        public Int32 ItemLogic;                                               // 56
        public List<Int32> ItemID;                                            // 57-60
        public List<Int32> ItemCount;                                         // 61-64
        public Int32 ItemFlags;                                               // 65
        public List<Int32> Explored;                                          // 66-67
        public List<Int32> Time;                                              // 68-69
        public Int32 AuraSpellLogic;                                          // 70
        public List<Int32> AuraSpellID;                                       // 71-74
        public Int32 WorldStateExpressionID;                                  // 75
        public Int32 WeatherID;                                               // 76
        public Int32 PartyStatus;                                             // 77
        public Int32 LifetimeMaxPVPRank;                                      // 78
        public Int32 AchievementLogic;                                        // 79
        public List<Int32> Achievement;                                       // 80-83
        public Int32 LfgLogic;                                                // 84
        public List<Int32> LfgStatus;                                         // 85-88
        public List<Int32> LfgCompare;                                        // 89-92
        public List<Int32> LfgValue;                                          // 93-96
        public Int32 AreaLogic;                                               // 97
        public List<Int32> AreaID;                                            // 98-101
        public Int32 CurrencyLogic;                                           // 102
        public List<Int32> CurrencyID;                                        // 103-106
        public List<Int32> CurrencyCount;                                     // 107-110
        public Int32 QuestKillID;                                             // 111
        public Int32 QuestKillLogic;                                          // 112
        public List<Int32> QuestKillMonster;                                  // 113-116
        public Int32 MinExpansionLevel;                                       // 117
        public Int32 MaxExpansionLevel;                                       // 118
        public Int32 MinExpansionTier;                                        // 119
        public Int32 MaxExpansionTier;                                        // 120
        public Int32 MinGuildLevel;                                           // 121
        public Int32 MaxGuildLevel;                                           // 122
        public Int32 PhaseUseFlags;                                           // 123
        public Int32 PhaseID;                                                 // 124
        public Int32 PhaseGroupID;                                            // 125
        public Int32 MinAvgItemLevel;                                         // 126
        public Int32 MaxAvgItemLevel;                                         // 127
        public Int32 MinAvgEquippedItemLevel;                                 // 128
        public Int32 MaxAvgEquippedItemLevel;                                 // 129
        public Int32 ChrSpecializationIndex;                                  // 130
        public Int32 ChrSpecializationRole;                                   // 131
        public String FailureDescriptionLang;                                 // 132
        public Int32 PowerType;                                               // 133
        public Int32 PowerTypeComp;                                           // 134
        public Int32 PowerTypeValue;                                          // 135
    };

    enum CurrencyFlags
    {
        CURRENCY_FLAG_TRADEABLE             = 0x01,
        CURRENCY_FLAG_HIGH_PRECISION        = 0x08,
        CURRENCY_FLAG_ARCHAEOLOGY_FRAGMENT  = 0x20,
        CURRENCY_FLAG_HAS_SEASON_COUNT      = 0x80
    };

    public enum UnitClass
    {
        UNIT_CLASS_WARRIOR  = 1,
        UNIT_CLASS_PALADIN  = 2,
        UNIT_CLASS_ROGUE    = 4,
        UNIT_CLASS_MAGE     = 8
    };

    class Tools
    {
        private static readonly ConcurrentDictionary<string, string> CapturedResponses = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static bool CaptureResponses { get; set; }

        // Wowhead rate-limits/bans aggressive crawlers (AWS WAF). Space out real
        // HTTP requests globally so zone/range runs do not get the IP blocked.
        private static readonly object RequestThrottleLock = new object();
        private static DateTime _lastRequestStart = DateTime.MinValue;
        private const int MinRequestIntervalMs = 350;

        public static bool TryTakeCapturedResponse(string url, out string response)
        {
            return CapturedResponses.TryRemove(url, out response);
        }

        public static void DiscardCapturedResponse(string url)
        {
            string ignored;
            if (url != null)
                CapturedResponses.TryRemove(url, out ignored);
        }

        private static void CaptureResponse(string url, string response)
        {
            if (CaptureResponses && response != null)
                CapturedResponses[url] = response;
        }

        public static HttpClient InitHttpClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            HttpClient httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WowheadParserDF/1.0 (+https://github.com/Thordekk/WowheadParserDF)");
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/json;q=0.9,*/*;q=0.8");
            return httpClient;
        }

        public static String GetHtmlFromWowhead(String url, HttpClient webClient, ICacheManager cacheManager)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("A Wowhead URL is required.", "url");

            if (webClient == null)
                webClient = InitHttpClient();
            if (cacheManager == null)
                cacheManager = new FileCacheManager();

            try
            {
                var cached = cacheManager.LookupDataAsync(url, typeof(string)).GetAwaiter().GetResult();
                if (cached != null)
                {
                    string cachedText = cached.ToString();
                    // Never trust a cached bot-challenge or empty page: refetch instead.
                    if (!string.IsNullOrEmpty(cachedText) && !IsBotChallengePage(cachedText))
                    {
                        CaptureResponse(url, cachedText);
                        return cachedText;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Wowhead cache read failed; fetching from site: " + ex.Message);
            }

            for (int attempt = 1; attempt <= 3; ++attempt)
            {
                try
                {
                    ThrottleWowheadRequest();
                    using (HttpResponseMessage response = webClient.GetAsync(url).GetAwaiter().GetResult())
                    {
                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            // Do not persist negative responses: records can appear after a site update.
                            return string.Empty;
                        }

                        bool transient = response.StatusCode == HttpStatusCode.RequestTimeout ||
                            (int)response.StatusCode == 429 || (int)response.StatusCode >= 500;
                        if (transient)
                        {
                            if (attempt < 3)
                            {
                                int delay = 500 * attempt;
                                if (response.Headers.RetryAfter != null && response.Headers.RetryAfter.Delta.HasValue)
                                    delay = (int)Math.Min(10000, Math.Max(delay, response.Headers.RetryAfter.Delta.Value.TotalMilliseconds));
                                System.Threading.Thread.Sleep(delay);
                                continue;
                            }
                            Console.Error.WriteLine("Wowhead returned HTTP " + (int)response.StatusCode + " for " + url);
                            return string.Empty;
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            Console.Error.WriteLine("Wowhead returned HTTP " + (int)response.StatusCode + " for " + url);
                            return string.Empty;
                        }

                        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                        // Wowhead answers automated clients with an AWS WAF JS
                        // challenge (HTTP 202) or a CloudFront block page. These
                        // are not page content: never cache them, otherwise the
                        // poisoned entry is served forever and every parse comes
                        // back empty.
                        if (string.IsNullOrEmpty(body) || IsBotChallengePage(body))
                        {
                            // Bot checks are often transient (rate limiting), so retry a few times.
                            if (attempt < 3)
                            {
                                System.Threading.Thread.Sleep(1000 * attempt);
                                continue;
                            }
                            Console.Error.WriteLine("Wowhead served a bot challenge or empty response for " + url);
                            return string.Empty;
                        }

                        try { cacheManager.AddDataAsync(url, body).GetAwaiter().GetResult(); }
                        catch (Exception cacheError) { Console.Error.WriteLine("Wowhead cache write failed: " + cacheError.Message); }
                        CaptureResponse(url, body);
                        return body;
                    }
                }
                catch (TaskCanceledException ex)
                {
                    if (attempt == 3)
                    {
                        Console.Error.WriteLine("Wowhead request timed out for " + url + ": " + ex.Message);
                        return string.Empty;
                    }
                    System.Threading.Thread.Sleep(500 * attempt);
                }
                catch (HttpRequestException ex)
                {
                    if (attempt == 3)
                    {
                        Console.Error.WriteLine("Wowhead request failed for " + url + ": " + ex.Message);
                        return string.Empty;
                    }
                    System.Threading.Thread.Sleep(500 * attempt);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Wowhead request failed for " + url + ": " + ex.Message);
                    return string.Empty;
                }
            }
            return string.Empty;
        }

        private static void ThrottleWowheadRequest()
        {
            lock (RequestThrottleLock)
            {
                double elapsed = (DateTime.UtcNow - _lastRequestStart).TotalMilliseconds;
                int wait = (int)(MinRequestIntervalMs - elapsed);
                if (wait > 0)
                    System.Threading.Thread.Sleep(wait);
                _lastRequestStart = DateTime.UtcNow;
            }
        }

        public static bool IsBotChallengePage(string html)
        {
            if (string.IsNullOrEmpty(html))
                return true;

            // Real Wowhead pages embed the AWS WAF snippet too, so "gokuProps"
            // (only present on the JS challenge) or an empty <title> are the
            // reliable markers of a challenge/block page.
            if (html.Contains("gokuProps") || html.Contains("challenge-platform"))
                return true;
            if (html.Contains("awsWafCookieDomainList") && html.Contains("<title></title>"))
                return true;
            return html.Contains("Request blocked.");
        }

        public static String GetWowheadUrl(String type, String id)
        {
            string baseUrl = Entities.Entity.GetWowheadBaseUrl();
            if (!string.IsNullOrEmpty(type))
                return baseUrl + "/" + type + "=" + id;
            return baseUrl + "/" + id;
        }

        public static String GetFileNameForCurrentTime(String optionName)
        {
            return "SQL\\" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + optionName + ".sql";
        }

        public static List<String> ExtractListJsonFromWithPattern(String input, String pattern)
        {
            Regex parseJSonRegex = new Regex(pattern);
            MatchCollection jSonMatch = parseJSonRegex.Matches(input);

            List<String> returnList = new List<String>();
            foreach (Match match in jSonMatch)
            {
                if (match.Success != true)
                    continue;

                short i = 0;
                foreach (Group group in match.Groups)
                {
                    if (i++ != 0)
                        returnList.Add(group.Value);
                }
            }

            return returnList;
        }

        public static String ExtractJsonFromWithPattern(String input, String pattern, int groupIndex = 0)
        {
            List<String> extractedValues = ExtractListJsonFromWithPattern(input, pattern);

            if (extractedValues.Count <= groupIndex)
                return null;

            String jsonString = extractedValues[groupIndex];

            jsonString = jsonString.Replace("undefined",    "\"undefined\"");
            jsonString = jsonString.Replace("[,1]",         "[0,1]");
            jsonString = jsonString.Replace("[1,]",         "[1,0]");
            jsonString = jsonString.Replace("[,0]",         "[0,0]");
            jsonString = jsonString.Replace("[0,]",         "[0,0]");
            jsonString = jsonString.Replace("[,-1]",        "[0,-1]");
            jsonString = jsonString.Replace("[-1,]",        "[-1,0]");

            jsonString = jsonString.Replace("'{",           "{");
            jsonString = jsonString.Replace("}'",           "}");

            // Npc loot specific
            jsonString = jsonString.Replace("modes:",       "\"modes\":");
            jsonString = jsonString.Replace("count:",       "\"count\":");
            jsonString = jsonString.Replace("pctstack:",    "\"pctstack\":");

            // Npc vendor specific
            jsonString = jsonString.Replace("standing:",    "\"standing\":");
            jsonString = jsonString.Replace("react:",       "\"react\":");
            jsonString = jsonString.Replace("stack:",       "\"stack\":");
            jsonString = jsonString.Replace("avail:",       "\"avail\":");
            jsonString = jsonString.Replace("cost:",        "\"cost\":");

            return jsonString;
        }

        public static String NormalizeFloat(float value, int totalNumItems)
        {
            float returnFloat = value;

            if (Math.Floor(value) > (value - 0.10f))
                returnFloat = (float)Math.Round(value);

            if (Math.Ceiling(value) < (value + 0.10f))
                returnFloat = (float)Math.Round(value);

            if (Math.Round(value) == 0 && value != 0)
                returnFloat = value;

            if (returnFloat < 0)
                returnFloat = returnFloat * -1;

            if (returnFloat == 0)
                returnFloat = 1 / totalNumItems;

            var retVal = returnFloat.ToString("F99").TrimEnd("0".ToCharArray()).Replace(",", ".").TrimEnd(".".ToCharArray());

            if (retVal == "∞" || retVal == "")
            {
                returnFloat = 1 / totalNumItems;
                returnFloat.ToString("F99").TrimEnd("0".ToCharArray()).Replace(",", ".").TrimEnd(".".ToCharArray());

                if (retVal == "∞" || retVal == "")
                    retVal = "100";
            }

            return retVal;
        }

        public static Int32 GetUnixTimestamp()
        {
            return (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }

        public static void LoadCurrencyTemplatesCSV()
        {
            lock (m_currencyTemplate)
            {
                if (m_currencyTemplate.Count > 0)
                    return;

                using (var reader = new StreamReader("Ressources/CurrencyTypes.db2.csv"))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csv.Context.RegisterClassMap<CurrencyTypesClassMap>();
                    var records = csv.GetRecords<CurrencyTypes>();

                    foreach (CurrencyTypes line in records)
                    {
                        m_currencyTemplate.Add(line.ID, line);
                    }
                }
            }
        }

        public static void LoadItemExtendedCostDb2CSV()
        {
            lock (m_itemExtendedCost)
            {
                if (m_itemExtendedCost.Count > 0)
                    return;

                using (var reader = new StreamReader("Ressources/ItemExtendedCost.db2.csv"))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csv.Context.RegisterClassMap<ItemExtendedCostEntryClassMap>();
                    var records = csv.GetRecords<ItemExtendedCostEntry>();
                    foreach (var extendedCost in records)
                    {
                        m_itemExtendedCost.Add(extendedCost);
                    }
                }
            }
        }

        public static UInt32 GetExtendedCostId(List<Int32> itemId, List<Int32> itemCount, List<Int32> currencyId, List<Int32> currencyCount)
        {
            if (itemId.Count != itemCount.Count)
                return 0;

            if (currencyId.Count != currencyCount.Count)
                return 0;

            if (itemId.Count == 0 && currencyId.Count == 0)
                return 0;

            LoadItemExtendedCostDb2CSV();
            LoadCurrencyTemplatesCSV();

            foreach (ItemExtendedCostEntry extendedCostEntry in m_itemExtendedCost)
            {
                bool notMatch = false;

                for (int i = 0; i < 5; ++i)
                {
                    if (itemId.Count < (i + 1))
                        break;

                    if (extendedCostEntry.ItemID[i] != itemId[i])
                    {
                        notMatch = true;
                        break;
                    }

                    if (extendedCostEntry.ItemCount[i] != itemCount[i])
                    {
                        notMatch = true;
                        break;
                    }
                }

                if (notMatch)
                    continue;

                for (int i = 0; i < 5; ++i)
                {
                    if (currencyId.Count < (i + 1))
                        break;

                    if (extendedCostEntry.CurrencyID[i] != currencyId[i])
                    {
                        notMatch = true;
                        break;
                    }
                    var currId = extendedCostEntry.CurrencyCount[i];

                    if (m_currencyTemplate.TryGetValue(currId, out var currencyTypes))
                    {
                        int precision = (int)currencyTypes.GetPrecision();
                        if (extendedCostEntry.CurrencyCount[i] != (currencyCount[i] * precision))
                        {
                            notMatch = true;
                            break;
                        }
                    }
                }

                if (!notMatch)
                    return extendedCostEntry.ID;
            }

            return 0;
        }

        public static void LoadPlayerConditionDb2CSV()
        {
            lock (m_playerConditions)
            {
                if (m_playerConditions.Count > 0)
                    return;

                List<String> allLines = new List<String>(File.ReadAllLines("Ressources/PlayerCondition.db2.csv"));

                allLines.RemoveAt(0);

                foreach (String line in allLines)
                {
                    PlayerConditionEntry playerCondition = new PlayerConditionEntry();
                    String[] values = line.Split(',');

                    playerCondition.PrevQuestID = new List<Int32>();

                    playerCondition.ID = Convert.ToInt32(values[0]);
                    playerCondition.PrevQuestLogic = Convert.ToInt32(values[13]);

                    for (int i = 0; i < 4; ++i)
                        playerCondition.PrevQuestID.Add(Convert.ToInt32(values[75 + i]));

                    m_playerConditions.Add(playerCondition);
                }
            }
        }

        enum PrevQuestLogicFlags
        {
            Unk1                = 0x00001,
            TrackingQuestId1    = 0x10000,
            TrackingQuestId2    = 0x20000
        };

        public static Int32 GetPlayerConditionForTreasure(UInt32 questId)
        {
            LoadPlayerConditionDb2CSV();

            foreach (PlayerConditionEntry playerCondition in m_playerConditions)
            {
                if ((playerCondition.PrevQuestLogic & (int)PrevQuestLogicFlags.TrackingQuestId1) != 0)
                    if (playerCondition.PrevQuestID[0] == questId)
                        return playerCondition.ID;

                if ((playerCondition.PrevQuestLogic & (int)PrevQuestLogicFlags.TrackingQuestId2) != 0)
                    if (playerCondition.PrevQuestID[1] == questId)
                        return playerCondition.ID;
            }

            return 0;
        }

        public static UInt32 GetClassMaskFromClassId(String strClassId)
        {
            UInt32 classId = UInt32.Parse(strClassId);
            return Convert.ToUInt32(Math.Pow(2, classId - 1));
        }

        public static void LoadBaseHps()
        {
            if (m_baseHpForLevelAndClass != null)
                return;

            List<String> BaseHpGts = new List<String>()
            {
                "NpcTotalHp.txt",
                "NpcTotalHpExp1.txt",
                "NpcTotalHpExp2.txt",
                "NpcTotalHpExp3.txt",
                "NpcTotalHpExp4.txt",
                "NpcTotalHpExp5.txt",
                "NpcTotalHpExp6.txt",
            };

            m_baseHpForLevelAndClass = new Dictionary<int, Dictionary<int, Dictionary<int, float>>>();

            for (int i = 0; i < BaseHpGts.Count; ++i)
            {
                List<String> allLines = new List<String>(File.ReadAllLines("Ressources/" + BaseHpGts[i]));

                allLines.RemoveAt(0);
                m_baseHpForLevelAndClass.Add(i, new Dictionary<int, Dictionary<int, float>>());

                for (int rowClassIndex = 0; rowClassIndex < allLines.Count; ++rowClassIndex)
                {
                    String[] values = allLines[rowClassIndex].Split('\t');
                    int level = int.Parse(values[0]);

                    m_baseHpForLevelAndClass[i].Add(level, new Dictionary<int, float>());
                    m_baseHpForLevelAndClass[i][level].Add((int)UnitClass.UNIT_CLASS_ROGUE,  float.Parse(values[1].Replace(".", ",")));
                    m_baseHpForLevelAndClass[i][level].Add((int)UnitClass.UNIT_CLASS_MAGE,   float.Parse(values[4].Replace(".", ",")));
                    m_baseHpForLevelAndClass[i][level].Add((int)UnitClass.UNIT_CLASS_PALADIN,float.Parse(values[5].Replace(".", ",")));
                    m_baseHpForLevelAndClass[i][level].Add((int)UnitClass.UNIT_CLASS_WARRIOR,float.Parse(values[9].Replace(".", ",")));
                }
            }
        }

        public static String GetHealthModifier(float currentHealth, int exp, int level, int classIndex)
        {
            LoadBaseHps();

            float baseHp = m_baseHpForLevelAndClass[exp][level][classIndex];

            return NormalizeFloat(currentHealth / baseHp, 1);
        }

        private static List<ItemExtendedCostEntry> m_itemExtendedCost = new List<ItemExtendedCostEntry>();
        private static List<PlayerConditionEntry> m_playerConditions = new List<PlayerConditionEntry>();
        private static Dictionary<UInt32, CurrencyTypes> m_currencyTemplate = new Dictionary<uint, CurrencyTypes>();
        //                        Exp             Level           Class
        private static Dictionary<int, Dictionary<int, Dictionary<int, float>>> m_baseHpForLevelAndClass;
    }
}

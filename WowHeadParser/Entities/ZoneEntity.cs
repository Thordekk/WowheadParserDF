/*
 * * Created by Traesh for AshamaneProject (https://github.com/AshamaneProject)
 */
using Newtonsoft.Json;
using Sql;
using System;
using System.Diagnostics;
using System.Linq;
using WowHeadParser.Models;

namespace WowHeadParser.Entities
{
    class ZoneEntity : Entity
    {
        public struct ZoneParsing
        {
            public int id;
            public string name;
        }

        public struct FishingParsing
        {
            public int id;
            public int count;
            public string name;
        }

        public ZoneEntity()
        {
            m_data.id = 0;
        }

        public ZoneEntity(int id)
        {
            m_data.id = id;
        }

        public override String GetWowheadUrl()
        {
            return GetWowheadBaseUrl() + "/zone=" + m_data.id;
        }

        public override bool ParseSingleJson(int id = 0)
        {
            if (m_data.id == 0 && id == 0)
                return false;
            else if (m_data.id == 0 && id != 0)
                m_data.id = id;

            String zoneHTML = Tools.GetHtmlFromWowhead(GetWowheadUrl(), webClient, CacheManager);

            if (string.IsNullOrEmpty(zoneHTML) || zoneHTML.Contains("It may have been removed from the game."))
                return false;

            String fishingPattern = @"new Listview\(\{\s*template:\s*'item',\s*id:\s*'fishing',\s*name:\s*LANG\.tab_fishing,.*?_totalCount:\s*([0-9]+),.*?computeDataFunc:\s*Listview\.funcBox\.initLootTable,.*?data:\s*(\[[\s\S]+?\])\s*\}\);";

            String fishingJSon = Tools.ExtractJsonFromWithPattern(zoneHTML, fishingPattern);
            Debug.WriteLine(fishingJSon);


            if (fishingJSon != null)
            {
                m_fishingDatas = JsonConvert.DeserializeObject<ZoneFishingLoot[]>(fishingJSon);
            }

            return true;
        }

        public override String GetSQLRequest()
        {
            String returnSql = "";

            if (m_data.id == 0 || isError)
                return returnSql;

            if (IsCheckboxChecked("Fishing") && m_fishingDatas != null)
            {
                m_FishingLootTemplateBuilder = new SqlBuilder("fishing_loot_template", "entry", SqlQueryType.InsertIgnore);
                m_FishingLootTemplateBuilder.SetFieldsNames("item", "Chance", "lootmode", "groupid", "MinCount", "MaxCount", "Comment");
                var totalCount = m_fishingDatas.Sum(f => f.count);

                foreach (ZoneFishingLoot fishingLootdata in m_fishingDatas)
                {
                    String percent = Tools.NormalizeFloat(((float)fishingLootdata.count / (float)totalCount * 100), m_fishingDatas.Length);

                    if (fishingLootdata.stack != null && fishingLootdata.stack.Length > 1)
                        m_FishingLootTemplateBuilder.AppendFieldsValue(m_data.id, fishingLootdata.id, percent, 1, 0, fishingLootdata.stack[0], fishingLootdata.stack[1], fishingLootdata.name.Replace("'", "\\'"));
                    else
                        m_FishingLootTemplateBuilder.AppendFieldsValue(m_data.id, fishingLootdata.id, percent, 1, 0, 1, 1, fishingLootdata.name.Replace("'", "\\'"));
                }

                returnSql += m_FishingLootTemplateBuilder.ToString() + "\n";
            }

            return returnSql;
        }

        public ZoneParsing m_data;

        protected ZoneFishingLoot[] m_fishingDatas;

        protected SqlBuilder m_spellLootTemplateBuilder;
        protected SqlBuilder m_FishingLootTemplateBuilder;
    }
}

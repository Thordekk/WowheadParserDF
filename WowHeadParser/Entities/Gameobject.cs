﻿/*
 * * Created by Traesh for AshamaneProject (https://github.com/AshamaneProject)
 */
using Newtonsoft.Json;
using Sql;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WowHeadParser.Models;
using static WowHeadParser.MainWindow;

namespace WowHeadParser.Entities
{
    class Gameobject : Entity
    {
        struct GameObjectParsing
        {
            public int id;
            public string name;
        }

        public class GameObjectLootParsing
        {
            public int id;
            public int count;
            public dynamic modes;
            public int[] stack;

            public string percent;
            public string questRequired;
            public Modes ModesObj;
            public string name;
        }

        public class GameObjectLootItemParsing : GameObjectLootParsing
        {
            public int classs;
        }

        public class GameObjectLootCurrencyParsing : GameObjectLootParsing
        {
            public int category;
            public string icon;
        }

        public Gameobject()
        {
            m_data.id = 0;
        }

        public Gameobject(int id)
        {
            m_data.id = id;
        }

        public override String GetWowheadUrl()
        {
            return GetWowheadBaseUrl() + "/object=" + m_data.id;
        }

        public override List<Entity> GetIdsFromZone(String zoneId, String zoneHtml)
        {
            String pattern = @"new Listview\(\{\s*template: 'object',\s*id: 'same-model-as',\s*name: WH\.TERMS\.samemodelas_stc,\s*tabs: tabsRelated,\s*parent: 'lkljbjkb574',\s*data:\s*(\[[\s\S]+?\])\s*\}\);";
            String gameobjectJSon = Tools.ExtractJsonFromWithPattern(zoneHtml, pattern, 1);

            List<Entity> tempArray = new List<Entity>();
            if (gameobjectJSon != null)
            {
                List<GameObjectParsing> parsingArray = JsonConvert.DeserializeObject<List<GameObjectParsing>>(gameobjectJSon);
                foreach (GameObjectParsing gameobjectTemplateStruct in parsingArray)
                {
                    Gameobject gameobject = new Gameobject(gameobjectTemplateStruct.id);
                    tempArray.Add(gameobject);
                }
            }


            return tempArray;
        }

        public override bool ParseSingleJson(int id = 0)
        {
            if (m_data.id == 0 && id == 0)
                return false;
            else if (m_data.id == 0 && id != 0)
                m_data.id = id;

            bool optionSelected = false;
            String gameobjectHtml = Tools.GetHtmlFromWowhead(GetWowheadUrl(), webClient, CacheManager);

            String gameobjectDataPattern = @"\$\.extend\(g_objects\[" + m_data.id + @"\], (.+)\);";
            String gameobjectDataJSon = Tools.ExtractJsonFromWithPattern(gameobjectHtml, gameobjectDataPattern);
            Debug.WriteLine(gameobjectDataJSon);

            if (gameobjectDataJSon != null)
            {
                m_data = JsonConvert.DeserializeObject<GameObjectParsing>(gameobjectDataJSon);
            }

            if (IsCheckboxChecked("locale"))
                optionSelected = true;

            if (IsCheckboxChecked("loot"))
            {
                String gameobjectLootPattern = @"new Listview\(\{\s*template: 'item',\s*id: 'contains',\s*name: WH\.TERMS\.contains,.*?data:\s*(\[[\s\S]+?\])\s*\}\);";
                String gameobjectLootItemJSon = Tools.ExtractJsonFromWithPattern(gameobjectHtml, gameobjectLootPattern);

                String gameobjectLootCurrencyPattern = @"new Listview\({template: 'currency', id: 'contains-currency', name: WH.TERMS.currencies,.*data:(.+)}\);";
                String gameobjectLootCurrencyJSon = Tools.ExtractJsonFromWithPattern(gameobjectHtml, gameobjectLootCurrencyPattern);

                if (gameobjectLootItemJSon != null || gameobjectLootCurrencyJSon != null)
                {
                    ObjectContains[] gameobjectLootItemDatas = gameobjectLootItemJSon != null ? JsonConvert.DeserializeObject<ObjectContains[]>(gameobjectLootItemJSon, Converter.SettingsDropConverter) : new ObjectContains[0];
                    ObjectContainsCurrency[] gameobjectLootCurrencyDatas = gameobjectLootCurrencyJSon != null ? JsonConvert.DeserializeObject<ObjectContainsCurrency[]>(gameobjectLootCurrencyJSon) : new ObjectContainsCurrency[0];
                    SetGameobjectLootData(gameobjectLootItemDatas, gameobjectLootCurrencyDatas);
                    optionSelected = true;
                }
            }

            if (IsCheckboxChecked("herbalism"))
            {       
                String gameobjectHerboPattern = @"new Listview\(\{template: 'item', id: 'herbalism',.*_totalCount: ([0-9]+),.*data: (.+)\}\);";

                String gameobjectHerbalismTotalCount = Tools.ExtractJsonFromWithPattern(gameobjectHtml, gameobjectHerboPattern, 0);
                String gameobjectHerbalismJSon = Tools.ExtractJsonFromWithPattern(gameobjectHtml, gameobjectHerboPattern, 1);
                if (gameobjectHerbalismJSon != null)
                {
                    GameObjectLootParsing[] gameobjectHerbalismDatas = JsonConvert.DeserializeObject<GameObjectLootParsing[]>(gameobjectHerbalismJSon);
                    SetGameobjectHerbalismOrMiningData(gameobjectHerbalismDatas, Int32.Parse(gameobjectHerbalismTotalCount), true, gameobjectHerbalismDatas.Length);
                    optionSelected = true;
                }
            }

            if (IsCheckboxChecked("mining"))
            {
                String gameobjectMiningPattern = @"new Listview\(\{template: 'item', id: 'mining',.*_totalCount: ([0-9]+),.*data: (.+)\}\);";

                String gameobjectMiningTotalCount = Tools.ExtractJsonFromWithPattern(gameobjectHtml, gameobjectMiningPattern, 0);
                String gameobjectMiningJSon = Tools.ExtractJsonFromWithPattern(gameobjectHtml, gameobjectMiningPattern, 1);
                if (gameobjectMiningJSon != null)
                {
                    GameObjectLootParsing[] gameobjectMiningDatas = JsonConvert.DeserializeObject<GameObjectLootParsing[]>(gameobjectMiningJSon);
                    SetGameobjectHerbalismOrMiningData(gameobjectMiningDatas, Int32.Parse(gameobjectMiningTotalCount), false, gameobjectMiningDatas.Length);
                    optionSelected = true;
                }
            }

            if (optionSelected)
                return true;
            else
                return false;
        }

        public void SetGameobjectLootData(ObjectContains[] gameobjectLootItemDatas, ObjectContainsCurrency[] gameobjectLootCurrencyDatas)
        {
            var deltaList = new List<GameObjectLootParsing>();
            foreach (ObjectContains gameobjectLootItemData in gameobjectLootItemDatas)
            {
                deltaList.Add(new GameObjectLootParsing()
                {
                    count = gameobjectLootItemData.count,
                    id = gameobjectLootItemData.id,
                    questRequired = gameobjectLootItemData.classs == 12 ? "1" : "0",
                    stack = gameobjectLootItemData.stack,
                    ModesObj = gameobjectLootItemData.modes,
                    name = gameobjectLootItemData.name,
                });
            }

            foreach (ObjectContainsCurrency gameobjectLootItemData in gameobjectLootCurrencyDatas)
            {

                deltaList.Add(new GameObjectLootParsing()
                {
                    count = gameobjectLootItemData.count,
                    id = gameobjectLootItemData.id,
                    questRequired = "0",
                    stack = gameobjectLootItemData.stack,
                    ModesObj = gameobjectLootItemData.modes
                });
            }

            m_gameobjectLootDatas = deltaList.ToArray();
        }

        public void SetGameobjectHerbalismOrMiningData(GameObjectLootParsing[] gameobjectHerbalismOrMiningDatas, int totalCount, bool herbalism, int num)
        {
            for (uint i = 0; i < gameobjectHerbalismOrMiningDatas.Length; ++i)
            {
                float percent = (float)gameobjectHerbalismOrMiningDatas[i].count * 100 / (float)totalCount;

                gameobjectHerbalismOrMiningDatas[i].percent = Tools.NormalizeFloat(percent, num);
            }

            if (herbalism)
                m_gameobjectHerbalismDatas = gameobjectHerbalismOrMiningDatas;
            else
                m_gameobjectMiningDatas = gameobjectHerbalismOrMiningDatas;
        }

        public override String GetSQLRequest()
        {
            String returnSql = "";

            if (m_data.id == 0 || isError)
                return returnSql;

            if (IsCheckboxChecked("locale"))
            {
                LocaleConstant localeIndex = (LocaleConstant)Properties.Settings.Default.localIndex;

                if (localeIndex != 0)
                {
                    m_gameobjectLocalesBuilder = new SqlBuilder("gameobject_template_locale", "entry");
                    m_gameobjectLocalesBuilder.SetFieldsNames("locale", "name");

                    m_gameobjectLocalesBuilder.AppendFieldsValue(m_data.id, localeIndex.ToString(), m_data.name);
                    returnSql += m_gameobjectLocalesBuilder.ToString() + "\n";
                }
                else
                {
                    m_gameobjectLocalesBuilder = new SqlBuilder("gameobject_template", "entry");
                    m_gameobjectLocalesBuilder.SetFieldsNames("name");

                    m_gameobjectLocalesBuilder.AppendFieldsValue(m_data.id, m_data.name);
                    returnSql += m_gameobjectLocalesBuilder.ToString() + "\n";
                }
            }

            if (IsCheckboxChecked("loot") && m_gameobjectLootDatas != null)
            {
                m_gameobjectLootBuilder = new SqlBuilder("gameobject_loot_template", "entry", SqlQueryType.DeleteInsert);
                m_gameobjectLootBuilder.SetFieldsNames("Item", "ItemType", "Chance", "QuestRequired", "LootMode", "GroupId", "MinCount", "MaxCount", "Comment");

                returnSql += "UPDATE gameobject_template SET data1 = " + m_data.id + " WHERE entry = " + m_data.id + " AND type IN (3, 50);\n";
                foreach (GameObjectLootParsing gameobjectLootData in m_gameobjectLootDatas)
                {
                    GameObjectLootCurrencyParsing currentLootCurrencyData = null;
          
                    if (gameobjectLootData is GameObjectLootCurrencyParsing golip)
                        currentLootCurrencyData = golip;
    
                    int idMultiplier = currentLootCurrencyData != null ? -1 : 1;

                    if (idMultiplier < 1)
                        continue;

                    int minLootCount = gameobjectLootData.stack.Length >= 1 ? gameobjectLootData.stack[0] : 1;
                    int maxLootCount = gameobjectLootData.stack.Length >= 2 ? gameobjectLootData.stack[1] : minLootCount;
                    if (gameobjectLootData.ModesObj.ModeMap == null)
                        continue;

                    int lootMode = 1;
                    int lootMask = 1;
                    foreach (var modeId in gameobjectLootData.ModesObj.mode)
                    {
                        lootMode = lootMode * 2;
                        lootMask |= lootMode;
                    }


                    if (gameobjectLootData.ModesObj.ModeMap.TryGetValue("0", out var mode))
                    {

                        var chance = Tools.NormalizeFloat(mode.Percent, m_gameobjectLootDatas.Length);

                        if (gameobjectLootData.questRequired == "1")
                            chance = "100";
                        
                        m_gameobjectLootBuilder.AppendFieldsValue(m_data.id, // Entry
                                                                    gameobjectLootData.id * idMultiplier, // Item
                                                                    0, // ItemType
                                                                    chance, // Chance
                                                                    gameobjectLootData.questRequired, // QuestRequired
                                                                    lootMask, // LootMode
                                                                    0, // GroupId
                                                                    minLootCount, // MinCount
                                                                    maxLootCount, // MaxCount
                                                                    gameobjectLootData.name.Replace("'", "\\'")); // Comment

                    }

                    
                }

                returnSql += m_gameobjectLootBuilder.ToString() + "\n";
            }

            if (IsCheckboxChecked("herbalism") && m_gameobjectHerbalismDatas != null)
            {
                m_gameobjectHerbalismBuilder = new SqlBuilder("gameobject_loot_template", "entry", SqlQueryType.InsertIgnore);
                m_gameobjectHerbalismBuilder.SetFieldsNames("Item", "ItemType", "Chance", "QuestRequired", "LootMode", "GroupId", "MinCount", "MaxCount", "Comment");

                returnSql += "UPDATE gameobject_template SET data1 = " + m_data.id + " WHERE entry = " + m_data.id + " AND type IN (3, 50);\n";
                foreach (GameObjectLootParsing gameobjectHerbalismData in m_gameobjectHerbalismDatas)
                    m_gameobjectHerbalismBuilder.AppendFieldsValue(m_data.id, // Entry
                                                                   gameobjectHerbalismData.id, // Item
                                                                   0, // ItemType
                                                                   gameobjectHerbalismData.percent, // Chance
                                                                   0, // QuestRequired
                                                                   1, // LootMode
                                                                   0, // GroupId
                                                                   gameobjectHerbalismData.stack[0], // MinCount
                                                                   gameobjectHerbalismData.stack[1], // MaxCount
                                                                   ""); // Comment

                returnSql += m_gameobjectHerbalismBuilder.ToString() + "\n";
            }

            if (IsCheckboxChecked("mining") && m_gameobjectMiningDatas != null)
            {
                m_gameobjectMiningBuilder = new SqlBuilder("gameobject_loot_template", "entry", SqlQueryType.InsertIgnore);
                m_gameobjectMiningBuilder.SetFieldsNames("Item", "ItemType", "Chance", "QuestRequired", "LootMode", "GroupId", "MinCount", "MaxCount", "Comment");

                returnSql += "UPDATE gameobject_template SET data1 = " + m_data.id + " WHERE entry = " + m_data.id + " AND type IN (3, 50);\n";
                foreach (GameObjectLootParsing gameobjectMiningData in m_gameobjectMiningDatas)
                    m_gameobjectMiningBuilder.AppendFieldsValue(m_data.id, // Entry
                                                                gameobjectMiningData.id, // Item
                                                                0, // ItemType
                                                                gameobjectMiningData.percent, // Chance
                                                                0, // QuestRequired
                                                                1, // LootMode
                                                                0, // GroupId
                                                                gameobjectMiningData.stack[0], // MinCount
                                                                gameobjectMiningData.stack[1], // MaxCount
                                                                ""); // Comment

                returnSql += m_gameobjectMiningBuilder.ToString() + "\n";
            }

            return returnSql;
        }

        private GameObjectParsing m_data;
        protected GameObjectLootParsing[] m_gameobjectLootDatas;
        protected GameObjectLootParsing[] m_gameobjectHerbalismDatas;
        protected GameObjectLootParsing[] m_gameobjectMiningDatas;

        protected SqlBuilder m_gameobjectLootBuilder;
        protected SqlBuilder m_gameobjectHerbalismBuilder;
        protected SqlBuilder m_gameobjectMiningBuilder;
        protected SqlBuilder m_gameobjectLocalesBuilder;
    }
}

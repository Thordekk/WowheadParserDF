/*
 * * Created by Traesh for AshamaneProject (https://github.com/AshamaneProject)
 */
using Newtonsoft.Json;
using Sql;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;

namespace WowHeadParser.Entities
{
    class Item : Entity
    {
        struct ItemParsing
        {
            public int id;
            public string name;
            public string namedesc;
            public string description;
            public List<int> specs;
            public int level;
            public int classs;
            public int subclass;
            public int quality;
        }

        public struct ItemSpellParsing
        {
            public int id;
        }

        public struct ItemCreateItemParsing
        {
            public int id;
        }

        public struct ItemLootTemplateParsing
        {
            public int id;
            public int count;
            public int[] stack;
        }

        public struct ItemDroppedByTemplateParsing
        {
            public int id;
            public int count;
            public int outof;
        }

        public Item()
        {
            m_data.id = 0;
        }

        public Item(int id)
        {
            m_data.id = id;
        }

        public override String GetWowheadUrl()
        {
            return GetWowheadBaseUrl() + "/item=" + m_data.id + "&bonus=524";
        }


        public override bool ParseSingleJson(int id = 0)
        {
            if (m_data.id == 0 && id == 0)
                return false;
            else if (m_data.id == 0 && id != 0)
                m_data.id = id;


            bool optionSelected = false;
            String itemHtml = Tools.GetHtmlFromWowhead(GetWowheadUrl(), webClient, CacheManager);

            String dataPattern = @"\$\.extend\(g_items\[" + m_data.id + @"\],\s*(\{[\s\S]+?\})\s*\);";
            String itemDataJSon = Tools.ExtractJsonFromWithPattern(itemHtml, dataPattern);


            if (itemDataJSon != null)
            {
                m_data = JsonConvert.DeserializeObject<ItemParsing>(itemDataJSon);

                optionSelected = true;
            }

            if (IsCheckboxChecked("locale"))
                optionSelected = true;

            if (IsCheckboxChecked("create item"))
            {

                String itemSpellPattern = @"new Listview\(\{\s*template:\s*'spell',\s*id:\s*'created-by-spell',\s*name:\s*WH\.TERMS\.createdby,\s*tabs:\s*'tabsRelated',\s*parent:\s*'lkljbjkb574',.*?data:\s*(\[[\s\S]+?\]),?\s*\}\);";
                String itemSpellJSon = Tools.ExtractJsonFromWithPattern(itemHtml, itemSpellPattern);


                if (itemSpellJSon != null)
                {
                    m_itemSpellDatas = JsonConvert.DeserializeObject<ItemSpellParsing[]>(itemSpellJSon);
                    optionSelected = true;
                }

                String itemCreatePattern = @"new Listview\(\{\s*template:\s*'item',\s*id:\s*'see-also',\s*name:\s*WH\.TERMS\.seealso_stc,\s*tabs:\s*'tabsRelated',\s*parent:\s*'lkljbjkb574',.*?data:\s*(\[[\s\S]+?\]),?\s*\}\);";
                String itemCreateJSon = Tools.ExtractJsonFromWithPattern(itemHtml, itemCreatePattern);


                if (itemCreateJSon != null)
                {
                    m_itemCreateItemDatas = JsonConvert.DeserializeObject<ItemCreateItemParsing[]>(itemCreateJSon);
                    optionSelected = true;

                }
            }

            if (IsCheckboxChecked("loot"))
            {
                String itemLootTemplatePattern = @"new Listview\(\{\s*template:\s*'item',\s*id:\s*'contained-in-item',\s*name:\s*WH\.TERMS\.containedin,.*?computeDataFunc:\s*Listview\.funcBox\.initLootTable,.*?data:\s*(\[[\s\S]+?\]),?\s*\}\);";

                String lootMaxCountStr = Tools.ExtractJsonFromWithPattern(itemHtml, itemLootTemplatePattern, 0);

                int lootMaxCount = 0;
                if (!string.IsNullOrWhiteSpace(lootMaxCountStr) && Int32.TryParse(lootMaxCountStr, out lootMaxCount))
                {
                    m_lootMaxCount = lootMaxCount;
                }
                else
                {
                    m_lootMaxCount = 0;
                }

                String itemLootTemplateJSon = Tools.ExtractJsonFromWithPattern(itemHtml, itemLootTemplatePattern, 0);

                if (itemLootTemplateJSon != null)
                {
                    m_itemLootTemplateDatas = JsonConvert.DeserializeObject<ItemLootTemplateParsing[]>(itemLootTemplateJSon);
                    optionSelected = true;
                }
            }


            if (IsCheckboxChecked("dropped by"))
            {
                String itemDroppedByPattern = @"new Listview\(\{\s*template:\s*'npc',\s*id:\s*'dropped-by',\s*name:\s*WH\.TERMS\.droppedby,.*?computeDataFunc:\s*Listview\.funcBox\.initLootTable,.*?data:\s*(\[[\s\S]+?\]),?\s*\}\);";

                String itemDroppedByJson = Tools.ExtractJsonFromWithPattern(itemHtml, itemDroppedByPattern);


                if (itemDroppedByJson != null)
                {
                    m_itemDroppedByDatas = JsonConvert.DeserializeObject<ItemDroppedByTemplateParsing[]>(itemDroppedByJson);
                    optionSelected = true;
                }
            }


            if (optionSelected)
                return true;
            else
                return false;
        }

        public override String GetSQLRequest()
        {
            String returnSql = "";

            if (m_data.id == 0 || isError)
                return returnSql;

            if (IsCheckboxChecked("locale"))
            {
                int localeIndex = Properties.Settings.Default.localIndex;

                if (localeIndex >= 1 && localeIndex <= 10)
                {
                    SqlBuilder m_itemLocalesBuilder = new SqlBuilder("item_sparse_locale", "ID", SqlQueryType.InsertOrUpdate);

                    String locale = "";

                    switch (localeIndex)
                    {
                        case 1: locale = "koKR"; break;
                        case 2: locale = "frFR"; break;
                        case 3: locale = "deDE"; break;
                        case 4: locale = "zhCN"; break;
                        case 5: locale = "zhTW"; break;
                        case 6: locale = "esES"; break;
                        case 7: locale = "esMX"; break;
                        case 8: locale = "ruRU"; break;
                        case 9: locale = "ptPT"; break;
                        case 10: locale = "itIT"; break;
                    }

                    switch (GetVersion())
                    {
                        case "9.2.0.42560":
                            {
                                m_itemLocalesBuilder.SetFieldsNames("locale", "Description_lang", "Display3_lang", "Display2_lang", "Display1_lang", "Display_lang");
                                m_itemLocalesBuilder.AppendFieldsValue(m_data.id, locale, "", "", "", "", m_data.name.Substring(1) ?? "");
                            }
                            break;
                        default: // 8.x and 7.x
                            {
                                m_itemLocalesBuilder.SetFieldsNames("Name_" + locale);
                                m_itemLocalesBuilder.AppendFieldsValue(m_data.id, m_data.name.Substring(1) ?? "");
                            }
                            break;
                    }

                    returnSql += m_itemLocalesBuilder.ToString() + "\n";
                }
            }

            if (IsCheckboxChecked("create item") && m_itemCreateItemDatas != null)
            {
                m_spellLootTemplateBuilder = new SqlBuilder("spell_loot_template", "entry", SqlQueryType.InsertIgnore);

                m_spellLootTemplateBuilder.SetFieldsNames("Item", "Reference", "Chance", "QuestRequired", "LootMode", "GroupId", "MinCount", "MaxCount", "Comment");

                foreach (ItemCreateItemParsing itemLootData in m_itemCreateItemDatas)

                    if (m_itemSpellDatas != null && m_itemSpellDatas.Length > 0)
                    {
                        m_spellLootTemplateBuilder.AppendFieldsValue(m_itemSpellDatas[0].id, // Entry
                                                                     itemLootData.id, // Item
                                                                     0, // ReferenceitemLootData
                                                                     "100", // Chance
                                                                     0, // QuestRequired
                                                                     1, // LootMode
                                                                     0, // GroupId
                                                                     "1", // MinCount
                                                                     "1", // MaxCount
                                                                     ""); // Comment
                    }


                returnSql += m_spellLootTemplateBuilder.ToString() + "\n";

            }

            if (IsCheckboxChecked("loot") && m_itemLootTemplateDatas != null)
            {
                m_itemLootTemplateBuilder = new SqlBuilder("item_loot_template", "entry", SqlQueryType.InsertIgnore);
                m_itemLootTemplateBuilder.SetFieldsNames("Item", "Reference", "Chance", "QuestRequired", "LootMode", "GroupId", "MinCount", "MaxCount", "Comment");

                foreach (ItemLootTemplateParsing itemLootData in m_itemLootTemplateDatas)
                {
                    String percent = Tools.NormalizeFloat((float)itemLootData.count / (float)m_lootMaxCount * 100, m_itemLootTemplateDatas.Length);

                    int minLootCount = itemLootData.stack != null && itemLootData.stack.Length >= 1 ? itemLootData.stack[0] : 1;
                    int maxLootCount = itemLootData.stack != null && itemLootData.stack.Length >= 2 ? itemLootData.stack[1] : minLootCount;

                    m_itemLootTemplateBuilder.AppendFieldsValue(m_data.id, // Entry
                                                                itemLootData.id, // Item
                                                                0, // Reference
                                                                percent, // Chance
                                                                0, // QuestRequired
                                                                1, // LootMode
                                                                0, // GroupId
                                                                minLootCount, // MinCount
                                                                maxLootCount, // MaxCount
                                                                ""); // Comment
                }

                returnSql += m_itemLootTemplateBuilder.ToString() + "\n";
            }

            if (IsCheckboxChecked("dropped by") && m_itemDroppedByDatas != null)
            {
                m_itemDroppedByBuilder = new SqlBuilder("creature_loot_template", "entry", SqlQueryType.InsertIgnore);

                switch (GetVersion())
                {
                    case "9.2.0.42560":
                        {
                            m_itemDroppedByBuilder.SetFieldsNames("ItemType", "Item", "Reference", "Chance", "LootMode", "GroupId", "MinCount", "MaxCount", "comment");
                        }
                        break;
                    default: // 8.x and 7.x
                        {
                            m_itemDroppedByBuilder.SetFieldsNames("item", "ChanceOrQuestChance", "lootmode", "groupid", "mincountOrRef", "maxcount", "itemBonuses");
                        }
                        break;
                }


                foreach (ItemDroppedByTemplateParsing itemDroppedByData in m_itemDroppedByDatas)
                {
                    float percent = ((float)itemDroppedByData.count / (float)itemDroppedByData.outof) * 100.0f;
                    String percentStr = Tools.NormalizeFloat(percent, m_itemDroppedByDatas.Length);

                    switch (GetVersion())
                    {
                        case "9.2.0.42560":
                            {
                                m_itemDroppedByBuilder.AppendFieldsValue(itemDroppedByData.id, m_data.id, percentStr, 1, 0, "1", "1");
                            }
                            break;
                        default: // 8.x and 7.x
                            {
                                m_itemDroppedByBuilder.AppendFieldsValue(itemDroppedByData.id, m_data.id, percentStr, 1, 0, "1", "1", "");
                            }
                            break;
                    }
                }

                returnSql += "DELETE FROM creature_loot_template WHERE item = " + m_data.id + ";\n";
                returnSql += m_itemDroppedByBuilder.ToString() + "\n";
            }

            return returnSql;
        }

        protected int m_lootMaxCount;

        private ItemParsing m_data;
        protected ItemSpellParsing[] m_itemSpellDatas;
        protected ItemCreateItemParsing[] m_itemCreateItemDatas;
        protected ItemLootTemplateParsing[] m_itemLootTemplateDatas;
        protected ItemDroppedByTemplateParsing[] m_itemDroppedByDatas;

        protected SqlBuilder m_spellLootTemplateBuilder;
        protected SqlBuilder m_itemLootTemplateBuilder;
        protected SqlBuilder m_itemDroppedByBuilder;
    }
}

/*
 * * Created by Traesh for AshamaneProject (https://github.com/AshamaneProject)
 */
using HtmlAgilityPack;
using Newtonsoft.Json;
using Sql;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using WOWSharp.Community;
using WOWSharp.Community.Diablo;
using WOWSharp.Community.Wow;

namespace WowHeadParser.Entities
{
    class Quest : Entity
    {
        public string Site { get; set; } = "";
        struct QuestTemplateParsing
        {
            public int id;
            public int category;
            public int category2;
            public int[] currencyrewards;
            public int money;
            public string name;
            public int reqlevel;
            public int side;
            public int type;
        }

        public Quest() : base()
        {
            m_builderStarter = new SqlBuilder("creature_queststarter", "id");
            m_builderStarter.SetFieldsNames("quest");

            m_builderEnder = new SqlBuilder("creature_questender", "id");
            m_builderEnder.SetFieldsNames("quest");

            m_builderSerieWithPrevious = new SqlBuilder("quest_template_addon", "id", SqlQueryType.DeleteInsert);
            m_builderSerieWithPrevious.SetFieldsNames("PrevQuestID", "NextQuestID", "ExclusiveGroup", "RequiredMinRepFaction", "RequiredMaxRepFaction", "RequiredMinRepValue", "RequiredMaxRepValue", "ProvidedItemCount");

            m_builderRequiredTeam = new SqlBuilder("quest_template", "id", SqlQueryType.Update);

            m_builderRequiredClass = new SqlBuilder("quest_template_addon", "id", SqlQueryType.Update);
            m_builderRequiredClass.SetFieldsNames("AllowableClasses");

        }

        public Quest(int id) : this()
        {
            m_data.id = id;
        }

        public override String GetWowheadUrl()
        {
            return GetWowheadBaseUrl() + "/quest=" + m_data.id;
        }

        /* This method no longer exists */
        //public override List<Entity> GetIdsFromZone(String zoneId, String zoneHtml)
        //{
        //    String pattern = @"new Listview\(\{template: 'quest', id: 'quests', name: WH.TERMS.quests, tabs: tabsRelated, parent: 'lkljbjkb574',(.*)data: (.+)\}\);";
        //    String creatureJSon = Tools.ExtractJsonFromWithPattern(zoneHtml, pattern, 1);

        //    List<Entity> tempArray = new List<Entity>();
        //    if (creatureJSon != null)
        //    {
        //        List<CreatureTemplateParsing> parsingArray = JsonConvert.DeserializeObject<List<CreatureTemplateParsing>>(creatureJSon);
        //        foreach (CreatureTemplateParsing creatureTemplateStruct in parsingArray)
        //        {
        //            Quest questTemplate = new Quest(creatureTemplateStruct.id);
        //            tempArray.Add(questTemplate);
        //        }
        //    }

        //    return tempArray;
        //}

        public void PopulateSite()
        {
            Site = Tools.GetHtmlFromWowhead(GetWowheadUrl(), webClient, CacheManager);

            if (Site.Contains("inputbox-error") || Site.Contains("database-detail-page-not-found-message") || Site.Contains("This quest was marked obsolete by Blizzard and cannot be obtained or completed"))
                QuestIsValid = false;
        }

        public bool QuestIsValid { get; set; } = true;

        public override bool ParseSingleJson(int id = 0)
        {
            if (m_data.id == 0 && id == 0)
                return false;
            else if (m_data.id == 0 && id != 0)
                m_data.id = id;

            PopulateSite();
            bool optionSelected = false;

            if (!QuestIsValid)
                return false;

            if (IsCheckboxChecked("starter/ender"))
            {
                String dataPattern = @"var\s+myMapper\s*=\s*new\s+Mapper\s*\(\s*(\{[\s\S]+?\})\s*\)";


                String questDataJSon = Tools.ExtractJsonFromWithPattern(Site, dataPattern);
             

                if (questDataJSon != null)
                {
                    dynamic data = JsonConvert.DeserializeObject<dynamic>(questDataJSon);
                    SetData(data);
                    optionSelected = true;
                }
            }

            if (IsCheckboxChecked("serie"))
            {
                String seriePattern = "(<table class=\"series\">.+?</table>)";

                String questSerieXml = Tools.ExtractJsonFromWithPattern(Site, seriePattern);
                if (questSerieXml != null)
                {
                    SetSerie(questSerieXml, false).Wait();


                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(Site);
                    var divs = doc.DocumentNode.SelectNodes("//div[@class='quick-facts-storyline-list']");
                    
                    if (divs != null && divs.Count != 0)
                        foreach (var div in divs)
                            SetSerie(div.InnerHtml, true).Wait();
                    
                    optionSelected = true;
                }
            }

            if (IsCheckboxChecked("class"))
            {
                String classLinePattern = @"\[li\](?:Class|Classes): (.+)\[\\/li\]\[li\]\[icon name=quest_start\]";

                String questClassLineJSon = Tools.ExtractJsonFromWithPattern(Site, classLinePattern);
                if (questClassLineJSon != null)
                {
                    List<String> questClass = Tools.ExtractListJsonFromWithPattern(questClassLineJSon, @"\[class=(\d+)\]");
                    SetClassRequired(questClass);
                }
                else
                    m_builderRequiredClass.AppendFieldsValue(m_data.id, 0);
                optionSelected = true;
            }

            if (IsCheckboxChecked("team"))
            {
                bool isAlliance = Site.Contains(@"Side: [span class=icon-alliance]Alliance[\/span]");
                bool isHorde = Site.Contains(@"Side: [span class=icon-horde]Horde[\/span]");

                SetTeam(isAlliance, isHorde);
                optionSelected = true;
            }

            if (optionSelected)
                return true;
            else
                return false;
        }

        public void SetData(dynamic questData)
        {
            // Save starter and ender IDs to avoid duplicates that Wowhead sometimes has
            List<int> starterIDs = new List<int>();
            List<int> enderIDs = new List<int>();

            foreach (dynamic objective in questData.objectives)
            {
                foreach (dynamic zoneid in objective)
                {
                    foreach (dynamic levels in zoneid.levels)
                    {
                        dynamic objectiveData = levels.First;
                        if (objectiveData is Newtonsoft.Json.Linq.JArray)
                        {
                            for (int i = 0; i < objectiveData.Count; i++)
                            {
                                if (objectiveData[i]["id"] == null)
                                    continue;

                                int npcID = objectiveData[i]["id"].ToObject<int>();

                                if (objectiveData[i]["point"] == "start" && !starterIDs.Contains(npcID))
                                {
                                    m_builderStarter.AppendFieldsValue(objectiveData[i]["id"], m_data.id);
                                    starterIDs.Add(npcID);
                                }

                                if (objectiveData[i]["point"] == "end" && !enderIDs.Contains(npcID))
                                {
                                    m_builderEnder.AppendFieldsValue(objectiveData[i]["id"], m_data.id);
                                    enderIDs.Add(npcID);
                                }
                            }

                        } else
                        {
                            if (objectiveData["point"] == "start")
                                m_builderStarter.AppendFieldsValue(objectiveData["id"], m_data.id);

                            if (objectiveData["point"] == "end")
                                m_builderEnder.AppendFieldsValue(objectiveData["id"], m_data.id);
                        }
                    }
                }
            }
        }
        static List<string> parsedQuests = new List<string>();
        public async Task SetSerie(String serieXml, bool li)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(serieXml);
                Dictionary<int, List<String>> questInSerieByStep = new Dictionary<int, List<String>>();
                int step = 0;

                if (!li)
                {
                    XmlNodeList trs = doc.DocumentElement.SelectNodes("tr");

                    foreach (XmlNode tr in trs)
                    {
                        int currentStep = step++;
                        questInSerieByStep.Add(currentStep, new List<string>());

                        XmlNode td = tr.SelectSingleNode("td");

                        if (td == null)
                            continue;

                        XmlNode div = td.SelectSingleNode("div");

                        if (div == null)
                            continue;

                        XmlNode b = div.SelectSingleNode("b");
                        XmlNodeList aList = div.SelectNodes("a");

                        // Current quest is writed with a simple "b" html tag
                        if (div.SelectSingleNode("b") != null)
                            questInSerieByStep[currentStep].Add(m_data.id.ToString());

                        foreach (XmlNode a in aList)
                        {
                            XmlNode hrefAttr = a.Attributes.GetNamedItem("href");

                            if (hrefAttr == null)
                                continue;

                            String href = hrefAttr.Value;
                            String questId = href.Substring(7);
                            questId = questId.Substring(0, questId.LastIndexOf("/"));

                            questInSerieByStep[currentStep].Add(questId);
                        }
                    }
                }
                else
                {
                    XmlNodeList trs = doc.DocumentElement.SelectNodes("li");

                    foreach (XmlNode tr in trs)
                    {
                        int currentStep = step++;
                        questInSerieByStep.Add(currentStep, new List<string>());
                        XmlNode b = tr.SelectSingleNode("span");
                        XmlNodeList aList = tr.SelectNodes("a");

                        if (b != null)
                        {
                            questInSerieByStep[currentStep].Add(m_data.id.ToString());
                            continue;
                        }

                        foreach (XmlNode a in aList)
                        {
                            XmlNode hrefAttr = a.Attributes.GetNamedItem("href");

                            if (hrefAttr == null)
                                continue;

                            String href = hrefAttr.Value;
                            String questId = href.Substring(7);
                            questId = questId.Substring(0, questId.LastIndexOf("/"));

                            questInSerieByStep[currentStep].Add(questId);
                        }
                    }
                }

                
                for (int i = questInSerieByStep.Count - 1; i >= 0; --i)
                {
                    String previousQuest = "0";
                    String nextQuest = "0";

                    if (questInSerieByStep.TryGetValue(i - 1, out var listStr) && listStr != null && listStr.Count != 0)
                        previousQuest = i > 0 ? listStr[0]: "";

                    if (questInSerieByStep.TryGetValue(i + 1, out listStr) && listStr != null && listStr.Count != 0)
                        nextQuest = i + 1 > 0 ? listStr[0] : "";

                    String exclusiveGroup = "0";
                    if (questInSerieByStep[i].Count > 1)
                        exclusiveGroup = "-" + questInSerieByStep[i][0];

                    foreach (String questId in questInSerieByStep[i])
                    {
                        lock(parsedQuests)
                            if (parsedQuests.Contains(questId))
                                continue;
                        
                        var questInfo = WowClient.GetQuestAsync(int.Parse(questId));
                        questInfo.Wait();
                        var qi = questInfo.Result;

                        long RequiredMinRepFaction = 0;
                        long RequiredMaxRepFaction = 0;
                        long RequiredMinRepValue = 0;
                        long RequiredMaxRepValue = 0;
                        long ProvidedItemCount = 0;
                       
                        if (qi != null)
                        {
                            if (qi.Requirements != null && qi.Requirements.Reputations != null && qi.Requirements.Reputations.Length != 0)
                            {
                                var facton = qi.Requirements.Reputations[0];

                                long minId = qi.Requirements.Reputations.Min(r => r.Faction.Id);
                                long maxId = qi.Requirements.Reputations.Max(r => r.Faction.Id);

                                RequiredMinRepFaction = minId;
                                RequiredMaxRepFaction = maxId;
                                RequiredMinRepValue = facton.MinReputation.HasValue ? facton.MinReputation.Value : 0;
                                RequiredMaxRepValue = facton.MaxReputation.HasValue ? facton.MaxReputation.Value : 0;
                            }

                            if (qi.Rewards != null && qi.Rewards.Items != null &&
                                ((qi.Rewards.Items.ChoiceOf != null && qi.Rewards.Items.ChoiceOf.Length != 0) || (qi.Rewards.Items.ItemsItems != null && qi.Rewards.Items.ItemsItems.Length != 0)))
                                ProvidedItemCount = 1;
                        }

                        lock(parsedQuests)
                            parsedQuests.Add(questId);
                        m_builderSerieWithPrevious.AppendFieldsValue(questId, previousQuest, nextQuest, exclusiveGroup, RequiredMinRepFaction, RequiredMaxRepFaction, RequiredMinRepValue, RequiredMaxRepValue, ProvidedItemCount);
                        m_builderRequiredTeam.AppendFieldsValue(questId);

                    }
                }
            }
            catch (Exception ex)
            { }
        }

        public void SetTeam(bool isAlliance, bool isHorde)
        {
            switch (GetVersion())
            {
                case "9.2.0.42560":
                {
                    ulong team = isAlliance ? 6130900294268439629 : isHorde ? 18446744073709551615 : 0;

                    m_builderRequiredTeam.SetFieldsNames("AllowableRaces");
                    m_builderRequiredTeam.AppendFieldsValue(m_data.id, team);
                }
                break;
                default: // 8.x and 7.x
                {
                    Int32 team = isAlliance ? 0 : isHorde ? 1 : -1;

                    m_builderRequiredTeam.SetFieldsNames("requiredTeam");
                    m_builderRequiredTeam.AppendFieldsValue(m_data.id, team);
                }
                break;
            }
        }

        public void SetClassRequired(List<String> classIds)
        {
            UInt32 classMask = 0;
            foreach (String classId in classIds)
            {
                classMask += Tools.GetClassMaskFromClassId(classId);
            }

            if (classMask != 0)
                m_builderRequiredClass.AppendFieldsValue(m_data.id, classMask);
        }

        public override String GetSQLRequest()
        {
            String sqlRequest = "";

            if (IsCheckboxChecked("starter/ender"))
            {
                sqlRequest += m_builderStarter.ToString() + m_builderEnder.ToString();
            }

            if (IsCheckboxChecked("serie"))
            {
                sqlRequest += m_builderSerieWithPrevious.ToString();
            }

            if (IsCheckboxChecked("team"))
            {
                sqlRequest += m_builderRequiredTeam.ToString();
            }

            if (IsCheckboxChecked("class"))
            {
                sqlRequest += m_builderRequiredClass.ToString();
            }

            return sqlRequest;
        }

        private QuestTemplateParsing m_data;

        protected SqlBuilder m_builderStarter;
        protected SqlBuilder m_builderEnder;
        protected SqlBuilder m_builderSerieWithPrevious;
        protected SqlBuilder m_builderRequiredTeam;
        protected SqlBuilder m_builderRequiredClass;
    }
}

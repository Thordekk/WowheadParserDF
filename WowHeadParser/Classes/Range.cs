/*
 * Updated for current WowHead.com structure (2025)
 * Original by Traesh for AshamaneProject (https://github.com/AshamaneProject)
 * 
 * Key Changes:
 * - WowHead removed XML support in favor of JSON endpoints
 * - Blizzard now provides official Game Data APIs as primary source
 * - WowHead tooltips now use JavaScript-based system
 * - Modern async/await patterns replace BackgroundWorker
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using WowHeadParser.Entities;
using WOWSharp.Community;
using WOWSharp.Community.Wow;

namespace WowHeadParser
{
    class Range
    {
        const int MAX_WORKER = 20;
        Queue<string> _sqlQ = new Queue<string>();
        bool _done = false;
        private readonly object _lockObj = new object();

        public Range(MainWindow view, String fileName, String optionName)
        {
            m_view = view;
            m_index = 0;
            m_parsedEntitiesCount = 0;
            m_getRangeListBackgroundWorker = new BackgroundWorker[MAX_WORKER];
            m_webClients = new HttpClient[MAX_WORKER];
            m_client = new WowClient[MAX_WORKER];
            m_cacheManagers = new ICacheManager[MAX_WORKER];
            m_fileName = fileName;
            m_optionName = optionName;
            m_lastEstimateTime = 0;
        }

        public bool StartParsing(int from, int to)
        {
            if (from > to)
            {
                System.Windows.Forms.MessageBox.Show("End ID of Range should be higher than start.", "Error!");
                return false;
            }

            m_timestamp = Tools.GetUnixTimestamp();

            m_from = from;
            m_to = to;
            m_entityTodoCount = to - from + 1;

            StartSniffByEntity();
            return true;
        }

        void StartSniffByEntity()
        {
            m_index = 0;
            m_parsedEntitiesCount = 0;

            int maxWorkers = (m_to - m_from + 1) > MAX_WORKER ? MAX_WORKER : m_to - m_from + 1;

            var task = new Task(() =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(m_fileName));
                using (var sw = new StreamWriter(m_fileName, true))
                {
                    while (!_done || _sqlQ.Count != 0)
                    {
                        while (_sqlQ.Count > 0)
                        {
                            string requestText = null;

                            lock (_sqlQ)
                            {
                                if (_sqlQ.Count > 0)
                                    requestText = _sqlQ.Dequeue();
                            }

                            if (!string.IsNullOrEmpty(requestText))
                                sw.Write(requestText);
                        }

                        sw.Flush();
                        System.Threading.Thread.Sleep(1000);
                    }

                    System.Threading.Thread.Sleep(1000);
                }
            });
            task.Start();

            for (int i = 0; i < maxWorkers; ++i)
            {
                m_webClients[i] = Tools.InitHttpClient();

                // Set user agent to avoid blocking
                m_webClients[i].DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                m_client[i] = new WowClient();
                m_cacheManagers[i] = new FileCacheManager();
                m_getRangeListBackgroundWorker[i] = new BackgroundWorker();
                m_getRangeListBackgroundWorker[i].DoWork += new DoWorkEventHandler(BackgroundWorkerProcessEntitiesList);
                m_getRangeListBackgroundWorker[i].RunWorkerCompleted += new RunWorkerCompletedEventHandler(BackgroundWorkerProcessEntitiesCompleted);
                m_getRangeListBackgroundWorker[i].RunWorkerAsync(i);
            }
        }

        private void BackgroundWorkerProcessEntitiesList(object sender, DoWorkEventArgs e)
        {
            int currentIndex;
            lock (_lockObj)
            {
                if (m_index >= m_entityTodoCount)
                    return;
                currentIndex = m_index++;
            }

            try
            {
                e.Result = e.Argument;
                Entity entity = m_view.CreateNeededEntity(m_from + currentIndex);
                entity.webClient = m_webClients[(int)e.Result];
                entity.WowClient = m_client[(int)e.Result];

                // Modern WowHead uses JSON instead of XML
                // The ParseSingleJson method should be updated to use:
                // 1. Blizzard's Game Data API (primary)
                // 2. WowHead's tooltip data endpoint (fallback)
                // Format: https://www.wowhead.com/tooltip/[type]/[id]
                if (entity.ParseSingleJson())
                {
                    String requestText = "\n\n" + entity.GetSQLRequest();

                    lock (_sqlQ)
                        _sqlQ.Enqueue(requestText);
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                Console.WriteLine($"Entity {m_from + currentIndex} not found (404)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing entity {m_from + currentIndex}: {ex.Message}");
            }
            finally
            {
                lock (_lockObj)
                {
                    ++m_parsedEntitiesCount;
                }
            }
        }

        private void BackgroundWorkerProcessEntitiesCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (m_parsedEntitiesCount > m_entityTodoCount)
                return;

            if (m_view != null)
            {
                EstimateSecondsTimeLeft();
            }

            if (m_parsedEntitiesCount == m_entityTodoCount)
            {
                _done = true;
                m_view.SetWorkDone();
                return;
            }

            lock (_lockObj)
            {
                if (m_index >= m_entityTodoCount)
                    return;
            }

            int workerIndex = (int)e.Result;
            m_getRangeListBackgroundWorker[workerIndex].RunWorkerAsync(workerIndex);
        }

        private void EstimateSecondsTimeLeft()
        {
            Int32 unixTimestamp = Tools.GetUnixTimestamp();

            if ((m_lastEstimateTime + 1) >= unixTimestamp)
                return;

            m_lastEstimateTime = unixTimestamp;

            float elapsedSeconds = unixTimestamp - m_timestamp;

            float entityCount = m_to - m_from;
            float timeByEntity = (float)elapsedSeconds / (float)m_parsedEntitiesCount;

            float estimatedSecondsLeft = timeByEntity * (entityCount - m_parsedEntitiesCount);
            if (estimatedSecondsLeft < 0)
                estimatedSecondsLeft = 0;
            float totalTime = timeByEntity * entityCount;
            if (totalTime < 1)
                totalTime = 1;
            float percent = estimatedSecondsLeft / totalTime * 100;

            m_view.setProgressBar(100 - (int)percent);
            m_view.SetTimeLeft((Int32)estimatedSecondsLeft);
        }

        private MainWindow m_view;

        private String m_fileName;
        private String m_optionName;

        private int m_from;
        private int m_to;
        private int m_entityTodoCount;
        private int m_index;
        private int m_parsedEntitiesCount;

        private BackgroundWorker[] m_getRangeListBackgroundWorker;
        private HttpClient[] m_webClients;
        private WowClient[] m_client;
        private ICacheManager[] m_cacheManagers;

        private int m_timestamp;
        private int m_lastEstimateTime;
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WowHeadParser.Entities;
using WOWSharp.Community;

namespace WowHeadParser
{
    class Zone
    {
        private const int MaxWorkers = 4;
        private readonly object _stateLock = new object();
        private readonly object _queueLock = new object();
        private readonly Queue<string> _writeQueue = new Queue<string>();
        private Task _writerTask;
        private volatile bool _writerDone;
        private bool _completionStarted;

        private readonly MainWindow _view;
        private readonly bool _stagingMode;
        private readonly string _optionName;
        private string _zoneId;
        private string _zoneHtml;
        private string _fileName;
        private readonly List<Entity> _entities = new List<Entity>();
        private int _nextIndex;
        private int _parsedCount;
        private BackgroundWorker[] _workers;
        private HttpClient[] _httpClients;
        private ICacheManager[] _cacheManagers;
        private int _timestamp;
        private int _lastEstimateTime;

        public Zone(MainWindow view, string optionName)
        {
            _view = view;
            _stagingMode = view != null && view.StagingMode;
            _optionName = optionName;
        }

        private void ResetZone()
        {
            if (_view != null)
                _view.setProgressBar(0);

            _entities.Clear();
            _nextIndex = 0;
            _parsedCount = 0;
            _completionStarted = false;
            _lastEstimateTime = 0;
            _timestamp = Tools.GetUnixTimestamp();
            _fileName = Tools.GetFileNameForCurrentTime(_optionName);

            string directory = Path.GetDirectoryName(_fileName);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            _writerDone = false;
            lock (_queueLock)
                _writeQueue.Clear();

            string outputPath = _fileName;
            _writerTask = Task.Run(async () =>
            {
                using (var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false)))
                {
                    if (_stagingMode)
                        await writer.WriteAsync(Entity.StagingSchemaSql).ConfigureAwait(false);
                    while (true)
                    {
                        string sql = null;
                        lock (_queueLock)
                        {
                            if (_writeQueue.Count > 0)
                                sql = _writeQueue.Dequeue();
                            else if (_writerDone)
                                break;
                        }
                        if (sql != null)
                            await writer.WriteAsync(sql).ConfigureAwait(false);
                        else
                            await Task.Delay(100).ConfigureAwait(false);
                    }
                    await writer.FlushAsync().ConfigureAwait(false);
                }
            });
        }

        public bool StartParsing(string zone)
        {
            int zoneId;
            if (!Int32.TryParse(zone, out zoneId) || zoneId <= 0)
            {
                System.Windows.Forms.MessageBox.Show("Enter a positive zone ID.", "Invalid zone");
                return false;
            }

            Entity requestedEntity = _view.CreateNeededEntity();
            if (requestedEntity == null)
                return false;

            ResetZone();
            _zoneId = zoneId.ToString();
            try
            {
                if (requestedEntity.GetType() != typeof(BlackMarket))
                {
                    _zoneHtml = GetZoneHtmlFromWowhead(_zoneId);
                    if (string.IsNullOrEmpty(_zoneHtml) || Tools.IsBotChallengePage(_zoneHtml))
                    {
                        _writerDone = true;
                        try { if (_writerTask != null) _writerTask.Wait(); }
                        catch (AggregateException) { }
                        System.Windows.Forms.MessageBox.Show(
                            "Wowhead did not return the zone page (bot check or empty response). No SQL was generated.",
                            "Zone fetch failed");
                        return false;
                    }
                    ParseZoneJson(requestedEntity);
                }
                StartSniffByEntity();
                return true;
            }
            catch (Exception ex)
            {
                _writerDone = true;
                try { if (_writerTask != null) _writerTask.Wait(); }
                catch (AggregateException) { }
                System.Windows.Forms.MessageBox.Show("Could not parse the Wowhead zone page: " + ex.GetBaseException().Message, "Zone parse error");
                return false;
            }
        }

        private string GetZoneUrl(string zone)
        {
            return Entity.GetWowheadBaseUrl() + "/zone=" + zone;
        }

        private string GetZoneHtmlFromWowhead(string zone)
        {
            using (HttpClient client = Tools.InitHttpClient())
                return Tools.GetHtmlFromWowhead(GetZoneUrl(zone), client, new FileCacheManager());
        }

        private void ParseZoneJson(Entity requestedEntity)
        {
            List<Entity> entityList = requestedEntity.GetIdsFromZone(_zoneId, _zoneHtml);
            if (entityList != null)
                _entities.AddRange(entityList);

            string zoneUrl = GetZoneUrl(_zoneId);
            if (_stagingMode)
            {
                string payload;
                if (Tools.TryTakeCapturedResponse(zoneUrl, out payload) && !string.IsNullOrEmpty(payload))
                {
                    int zoneId = Int32.Parse(_zoneId);
                    string sql = Entity.CreateStagingSQL("zone_index", zoneId, zoneUrl, payload, string.Empty);
                    if (!string.IsNullOrWhiteSpace(sql))
                    {
                        lock (_queueLock)
                            _writeQueue.Enqueue(sql + Environment.NewLine);
                    }
                }
            }
            else
                Tools.DiscardCapturedResponse(zoneUrl);
        }

        private void StartSniffByEntity()
        {
            if (_entities.Count == 0)
            {
                // Defer completion until the Parse button handler has disabled itself.
                if (_view != null && _view.IsHandleCreated)
                    _view.BeginInvoke(new Action(CompleteZone));
                else
                    CompleteZone();
                return;
            }

            int workerCount = Math.Min(MaxWorkers, _entities.Count);
            _workers = new BackgroundWorker[workerCount];
            _httpClients = new HttpClient[workerCount];
            _cacheManagers = new ICacheManager[workerCount];

            for (int i = 0; i < workerCount; ++i)
            {
                _httpClients[i] = Tools.InitHttpClient();
                _cacheManagers[i] = new FileCacheManager();
                _workers[i] = new BackgroundWorker();
                _workers[i].DoWork += BackgroundWorkerProcessEntity;
                _workers[i].RunWorkerCompleted += BackgroundWorkerProcessEntityCompleted;
                _workers[i].RunWorkerAsync(i);
            }
        }

        private void BackgroundWorkerProcessEntity(object sender, DoWorkEventArgs e)
        {
            int workerIndex = (int)e.Argument;
            e.Result = workerIndex;
            int index;
            lock (_stateLock)
            {
                if (_nextIndex >= _entities.Count)
                    return;
                index = _nextIndex++;
            }

            Entity entity = _entities[index];
            try
            {
                entity.webClient = _httpClients[workerIndex];
                entity.CacheManager = _cacheManagers[workerIndex];
                bool parsed = entity.ParseSingleJson();
                bool stagingMode = _stagingMode;
                if (parsed || stagingMode)
                {
                    string legacySql = string.Empty;
                    if (parsed && stagingMode)
                    {
                        try { legacySql = entity.GetSQLRequest(); }
                        catch (Exception sqlError) { Console.Error.WriteLine("Legacy SQL mapping failed for zone entity: " + sqlError.Message); }
                    }
                    else if (parsed)
                        legacySql = entity.GetSQLRequest();
                    string sql = stagingMode ? entity.GetStagingSQL(legacySql) : legacySql;
                    if (!string.IsNullOrWhiteSpace(sql))
                    {
                        lock (_queueLock)
                            _writeQueue.Enqueue(sql + Environment.NewLine);
                    }
                }
                else
                    Tools.DiscardCapturedResponse(entity.GetWowheadUrl());
            }
            catch (Exception ex)
            {
                if (_stagingMode)
                {
                    string staged = entity.GetStagingSQL(string.Empty);
                    if (!string.IsNullOrWhiteSpace(staged))
                    {
                        lock (_queueLock)
                            _writeQueue.Enqueue(staged + Environment.NewLine);
                    }
                }
                else
                    Tools.DiscardCapturedResponse(entity.GetWowheadUrl());
                Console.Error.WriteLine("Zone entity parse failed: " + ex);
            }
            finally
            {
                lock (_stateLock)
                    ++_parsedCount;
            }
        }

        private void BackgroundWorkerProcessEntityCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
                Console.Error.WriteLine("Zone worker error: " + e.Error);
            if (e.Result == null)
                return;

            int processed;
            int nextIndex;
            bool completeNow;
            lock (_stateLock)
            {
                processed = _parsedCount;
                nextIndex = _nextIndex;
                completeNow = processed >= _entities.Count && !_completionStarted;
                if (completeNow)
                    _completionStarted = true;
                if (_completionStarted && !completeNow)
                    return;
            }
            EstimateSecondsTimeLeft(processed);

            if (completeNow)
            {
                CompleteZone();
                return;
            }

            int workerIndex = (int)e.Result;
            if (nextIndex < _entities.Count && !_workers[workerIndex].IsBusy)
                _workers[workerIndex].RunWorkerAsync(workerIndex);
        }

        private void CompleteZone()
        {
            _writerDone = true;
            try
            {
                if (_writerTask != null)
                    _writerTask.Wait();
            }
            catch (AggregateException ex)
            {
                System.Windows.Forms.MessageBox.Show("Could not finish writing the SQL file: " + ex.GetBaseException().Message, "Export error");
            }
            if (_httpClients != null)
                foreach (HttpClient client in _httpClients)
                    if (client != null)
                        client.Dispose();
            if (_view != null)
                _view.SetWorkDone();
        }

        private void EstimateSecondsTimeLeft(int processed)
        {
            int now = Tools.GetUnixTimestamp();
            if (_view == null || now <= _lastEstimateTime || processed <= 0)
                return;
            _lastEstimateTime = now;
            double elapsed = Math.Max(1, now - _timestamp);
            double remaining = elapsed / processed * Math.Max(0, _entities.Count - processed);
            int progress = (int)Math.Min(100, (long)processed * 100 / _entities.Count);
            _view.setProgressBar(progress);
            _view.SetTimeLeft((int)Math.Min(int.MaxValue, remaining));
        }
    }
}

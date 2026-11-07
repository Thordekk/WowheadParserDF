using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WOWSharp.Community;
using WOWSharp.Community.Wow;
using WowHeadParser.Entities;

namespace WowHeadParser
{
    class Range
    {
        // Keep concurrent requests modest: Wowhead is a public site, not a bulk API.
        private const int MaxWorkers = 4;
        private readonly object _stateLock = new object();
        private readonly object _queueLock = new object();
        private readonly System.Collections.Generic.Queue<string> _sqlQueue = new System.Collections.Generic.Queue<string>();
        private volatile bool _done;
        private bool _completionStarted;
        private Task _writerTask;

        private readonly MainWindow _view;
        private readonly string _fileName;
        private readonly string _optionName;
        private readonly bool _stagingMode;
        private int _from;
        private int _to;
        private int _todoCount;
        private int _nextIndex;
        private int _processedCount;
        private BackgroundWorker[] _workers;
        private HttpClient[] _httpClients;
        private WowClient[] _wowClients;
        private ICacheManager[] _cacheManagers;
        private int _timestamp;
        private int _lastEstimateTime;

        public Range(MainWindow view, string fileName, string optionName)
        {
            _view = view;
            _fileName = fileName;
            _optionName = optionName;
            _stagingMode = view != null && view.StagingMode;
        }

        public bool StartParsing(int from, int to)
        {
            if (from < 1 || to < from)
            {
                System.Windows.Forms.MessageBox.Show("Enter a positive ID range with End ID >= Start ID.", "Invalid range");
                return false;
            }

            long count = (long)to - from + 1;
            if (count > 1000000)
            {
                System.Windows.Forms.MessageBox.Show("The requested range exceeds the one-million-ID safety limit. Split it into smaller ranges.", "Range too large");
                return false;
            }

            _from = from;
            _to = to;
            _todoCount = (int)count;
            _nextIndex = 0;
            _processedCount = 0;
            _done = false;
            _completionStarted = false;
            _timestamp = Tools.GetUnixTimestamp();
            _lastEstimateTime = 0;
            StartWorkers();
            return true;
        }

        private void StartWorkers()
        {
            int count = Math.Min(MaxWorkers, _todoCount);
            _workers = new BackgroundWorker[count];
            _httpClients = new HttpClient[count];
            _wowClients = new WowClient[count];
            _cacheManagers = new ICacheManager[count];

            string directory = Path.GetDirectoryName(_fileName);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            bool writeStagingSchema = _stagingMode &&
                (!File.Exists(_fileName) || new FileInfo(_fileName).Length == 0);

            _writerTask = Task.Run(() =>
            {
                using (var writer = new StreamWriter(_fileName, true, new UTF8Encoding(false)))
                {
                    if (writeStagingSchema)
                        writer.Write(Entity.StagingSchemaSql);
                    while (true)
                    {
                        string sql = null;
                        lock (_queueLock)
                        {
                            if (_sqlQueue.Count > 0)
                                sql = _sqlQueue.Dequeue();
                            else if (_done)
                                break;
                        }

                        if (sql != null)
                            writer.Write(sql);
                        else
                            Thread.Sleep(100);
                    }
                    writer.Flush();
                }
            });

            for (int i = 0; i < count; ++i)
            {
                _httpClients[i] = Tools.InitHttpClient();
                _wowClients[i] = new WowClient();
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
                if (_nextIndex >= _todoCount)
                    return;
                index = _nextIndex++;
            }

            int entityId = _from + index;
            Entity entity = null;
            try
            {
                entity = _view.CreateNeededEntity(entityId);
                entity.webClient = _httpClients[workerIndex];
                entity.WowClient = _wowClients[workerIndex];
                entity.CacheManager = _cacheManagers[workerIndex];

                bool parsed = entity.ParseSingleJson();
                bool stagingMode = _stagingMode;
                if (parsed || stagingMode)
                {
                    string legacySql = string.Empty;
                    if (parsed && stagingMode)
                    {
                        try { legacySql = entity.GetSQLRequest(); }
                        catch (Exception sqlError) { Console.Error.WriteLine("Legacy SQL mapping failed for " + entityId + ": " + sqlError.Message); }
                    }
                    else if (parsed)
                        legacySql = entity.GetSQLRequest();
                    string sql = stagingMode
                        ? entity.GetStagingSQL(legacySql)
                        : legacySql;
                    if (!string.IsNullOrWhiteSpace(sql))
                    {
                        lock (_queueLock)
                            _sqlQueue.Enqueue("\n" + sql + "\n");
                    }
                    else if (_stagingMode)
                    {
                        Console.Error.WriteLine("No captured Wowhead page was available for entity " + entityId + "; skipped staging row.");
                    }
                }
                else
                {
                    Tools.DiscardCapturedResponse(entity.GetWowheadUrl());
                }
            }
            catch (Exception ex)
            {
                if (entity != null && _stagingMode)
                {
                    string staged = entity.GetStagingSQL(string.Empty);
                    if (!string.IsNullOrWhiteSpace(staged))
                    {
                        lock (_queueLock)
                            _sqlQueue.Enqueue("\n" + staged + "\n");
                    }
                }
                else if (entity != null)
                    Tools.DiscardCapturedResponse(entity.GetWowheadUrl());
                Console.Error.WriteLine("Error processing entity " + entityId + ": " + ex);
            }
            finally
            {
                lock (_stateLock)
                    ++_processedCount;
            }
        }

        private void BackgroundWorkerProcessEntityCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
                Console.Error.WriteLine("Worker error: " + e.Error);

            int workerIndex = e.Result == null ? -1 : (int)e.Result;
            if (workerIndex < 0)
                return;

            int processed;
            int nextIndex;
            bool completeNow;
            lock (_stateLock)
            {
                processed = _processedCount;
                nextIndex = _nextIndex;
                completeNow = processed >= _todoCount && !_completionStarted;
                if (completeNow)
                    _completionStarted = true;
                if (_completionStarted && !completeNow)
                    return;
            }

            EstimateSecondsTimeLeft(processed);

            if (completeNow)
            {
                _done = true;
                try
                {
                    if (_writerTask != null)
                        _writerTask.Wait();
                }
                catch (AggregateException ex)
                {
                    System.Windows.Forms.MessageBox.Show("Could not finish writing the SQL file: " + ex.GetBaseException().Message, "Export error");
                }
                DisposeHttpClients();
                if (_view != null)
                    _view.SetWorkDone();
                return;
            }

            if (nextIndex < _todoCount && !_workers[workerIndex].IsBusy)
                _workers[workerIndex].RunWorkerAsync(workerIndex);
        }

        private void EstimateSecondsTimeLeft(int processed)
        {
            int now = Tools.GetUnixTimestamp();
            if (now <= _lastEstimateTime || processed <= 0)
                return;
            _lastEstimateTime = now;

            double elapsed = Math.Max(1, now - _timestamp);
            double remaining = elapsed / processed * Math.Max(0, _todoCount - processed);
            int progress = (int)Math.Min(100, (long)processed * 100 / _todoCount);
            if (_view != null)
            {
                _view.setProgressBar(progress);
                _view.SetTimeLeft((int)Math.Min(int.MaxValue, remaining));
            }
        }

        private void DisposeHttpClients()
        {
            if (_httpClients == null)
                return;
            foreach (HttpClient client in _httpClients)
                if (client != null)
                    client.Dispose();
        }
    }
}

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WOWSharp.Community
{
    /// <summary>
    /// Small disk cache for Wowhead/API responses. Cache names are hashes of the
    /// complete URL (not just the final path component), preventing locale/query
    /// collisions and Windows-invalid filenames.
    /// </summary>
    public class FileCacheManager : ICacheManager
    {
        public static string CacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WowheadParser", "cache");
        private static readonly object FileLock = new object();

        public async Task AddDataAsync(string key, object value)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            string path = GetCacheFilePath(key);
            string contents = value is string ? (string)value : JsonConvert.SerializeObject(value);
            await Task.Run(() =>
            {
                string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporaryPath, contents, Encoding.UTF8);
                    lock (FileLock)
                    {
                        if (File.Exists(path))
                            File.Delete(path);
                        File.Move(temporaryPath, path);
                    }
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
            }).ConfigureAwait(false);
        }

        public async Task<object> LookupDataAsync(string key, Type objectType)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            if (objectType == null)
                throw new ArgumentNullException("objectType");

            string path = GetCacheFilePath(key);
            string contents = null;
            await Task.Run(() =>
            {
                lock (FileLock)
                {
                    if (File.Exists(path))
                        contents = File.ReadAllText(path, Encoding.UTF8);
                }
            }).ConfigureAwait(false);

            if (contents == null)
                return null;
            if (objectType == typeof(string))
                return contents;
            return JsonConvert.DeserializeObject(contents, objectType);
        }

        private string GetCacheFilePath(string key)
        {
            if (string.IsNullOrWhiteSpace(CacheDirectory))
                throw new InvalidOperationException("CacheDirectory must be configured.");
            Directory.CreateDirectory(CacheDirectory);

            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] hash;
            using (SHA256 sha = SHA256.Create())
                hash = sha.ComputeHash(keyBytes);

            var fileName = new StringBuilder(hash.Length * 2);
            foreach (byte item in hash)
                fileName.Append(item.ToString("x2"));
            return Path.Combine(CacheDirectory, fileName + ".cache");
        }
    }
}

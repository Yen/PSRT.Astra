using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.Phases
{
    public class VerifyFilesPhase
    {
        private InstallConfiguration _InstallConfiguration;
        private PatchCache _PatchCache;

        private class ProcessState
        {
            public int AtomicIndex;
            public int AtomicProcessCount;
            public ConcurrentQueue<List<PatchCacheEntry>> UpdateBuckets;
        }

        public VerifyFilesPhase(InstallConfiguration installConfiguration, PatchCache patchCache)
        {
            _InstallConfiguration = installConfiguration;
            _PatchCache = patchCache;
        }

        public async Task RunAsync(List<(string Name, (string Hash, Uri DownloadPath))> toUpdate)
        {
            var state = new ProcessState
            {
                AtomicIndex = 0,
                AtomicProcessCount = Environment.ProcessorCount,
                UpdateBuckets = new ConcurrentQueue<List<PatchCacheEntry>>()
            };

            var processTasks = Enumerable.Range(0, state.AtomicProcessCount)
                .Select(i => Task.Factory.StartNew(() => _Process(state, toUpdate), TaskCreationOptions.LongRunning));

            while (state.AtomicProcessCount > 0 || state.UpdateBuckets.Count != 0)
            {
                await Task.Delay(100);

                if (state.UpdateBuckets.Count == 0)
                    continue;

                while (state.UpdateBuckets.TryDequeue(out var list))
                {
                    if (list.Count > 0)
                        await _PatchCache.InsertUnderTransactionAsync(list);
                }
            }

            await Task.WhenAll(processTasks);
        }

        private async Task _Process(ProcessState state, List<(string Name, (string Hash, Uri DownloadPath))> toUpdate)
        {
            using (var md5 = MD5.Create())
            using (var client = new AquaHttpClient())
            {
                const int streamingFileSize = 10 * 1024 * 1024; // 10MB
                byte[] bufferBytes = new byte[streamingFileSize];

                var entries = new List<PatchCacheEntry>();
                while (true)
                {
                    var index = Interlocked.Increment(ref state.AtomicIndex) - 1;
                    if (index >= toUpdate.Count)
                    {
                        state.UpdateBuckets.Enqueue(entries);
                        break;
                    }

                    if (entries.Count > 50)
                    {
                        state.UpdateBuckets.Enqueue(entries);
                        entries = new List<PatchCacheEntry>();
                    }

                    var (name, info) = toUpdate[index];
                    var path = Path.Combine(_InstallConfiguration.PSO2BinDirectory, name.Substring(0, name.Length - 4));

                    if (File.Exists(path))
                    {
                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            // streaming the file into the hash is considerably slower than 
                            // reading into a byte array first. It does however avoid possible
                            // out of memory errors with giant files.
                            // As such we only stream the file if its bigger than the 
                            // specified file size
                            byte[] hashBytes;
                            if (fs.Length > streamingFileSize)
                            {
                                hashBytes = md5.ComputeHash(fs);
                            }
                            else
                            {
                                fs.Read(bufferBytes, 0, (int)fs.Length);
                                hashBytes = md5.ComputeHash(bufferBytes, 0, (int)fs.Length);
                            }

                            var hashString = string.Concat(hashBytes.Select(b => b.ToString("X2")));

                            if (hashString == info.Hash)
                            {
                                entries.Add(new PatchCacheEntry()
                                {
                                    Name = name,
                                    Hash = info.Hash,
                                    LastWriteTime = new FileInfo(path).LastWriteTimeUtc.ToFileTimeUtc()
                                });
                                continue;
                            }
                        }
                    }

                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        using (var responseStream = await client.GetStreamAsync(info.DownloadPath))
                        using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, true))
                            await responseStream.CopyToAsync(fs);
                    }
                    catch (Exception ex)
                    {
                        App.Current.Logger.Error(nameof(VerifyFilesPhase), "Error downloading file", ex);
                        throw;
                    }

                    entries.Add(new PatchCacheEntry()
                    {
                        Name = name,
                        Hash = info.Hash,
                        LastWriteTime = new FileInfo(path).LastWriteTimeUtc.ToFileTimeUtc()
                    });
                }
            }

            Interlocked.Decrement(ref state.AtomicProcessCount);
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.Phases
{
    public class VerifyFilesPhase
    {
        public VerifyFilesProgress Progress { get; } = new VerifyFilesProgress();

        private InstallConfiguration _InstallConfiguration;

        private class ProcessState
        {
            public int AtomicIndex;
            public int AtomicCompletedCount;
            public int AtomicProcessCount;
            public ConcurrentQueue<List<PatchCacheEntry>> UpdateBuckets;
        }

        public VerifyFilesPhase(InstallConfiguration installConfiguration)
        {
            _InstallConfiguration = installConfiguration;
        }

        public async Task RunAsync(PatchInfo[] toUpdate, PatchCache patchCache, CancellationToken ct = default)
        {
            Progress.Progress = 0;
            Progress.IsIndeterminate = true;
            Progress.CompletedCount = 0;
            Progress.TotalCount = 0;

            if (toUpdate.Length == 0)
                return;

            var state = new ProcessState
            {
                AtomicIndex = 0,
                AtomicCompletedCount = 0,
                AtomicProcessCount = 0,
                UpdateBuckets = new ConcurrentQueue<List<PatchCacheEntry>>()
            };

            var processCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);

            Progress.TotalCount = toUpdate.Length;

            App.Logger.Info(nameof(VerifyFilesPhase), "Starting processing threads");
            Progress.IsIndeterminate = false;
            // TODO: processor affinity?
            var threadTasks = Enumerable.Range(0, Environment.ProcessorCount)
                .Select(i => ConcurrencyUtils.RunOnDedicatedThreadAsync(() =>
                {
                    Interlocked.Increment(ref state.AtomicProcessCount);
                    try
                    {
                        App.Logger.Info(nameof(VerifyFilesPhase), $"Processing thread {i} started");

                        _Process(state, toUpdate, processCancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        App.Logger.Error(nameof(VerifyFilesPhase), $"Processing thread {i} canceled");
                        processCancellationTokenSource.Cancel();
                        throw;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error(nameof(VerifyFilesPhase), $"Exception in processing thread {i}", ex);
                        processCancellationTokenSource.Cancel();
                        throw;
                    }
                    finally
                    {
                        Interlocked.Decrement(ref state.AtomicProcessCount);
                        App.Logger.Info(nameof(VerifyFilesPhase), $"Processing thread {i} ended");
                    }
                }, $"{nameof(VerifyFilesPhase)}({i})")).ToArray();

            await Task.Run(async () =>
            {
                while (state.AtomicProcessCount > 0 || state.UpdateBuckets.Count != 0)
                {
                    await Task.Delay(250, ct);

                    Progress.Progress = state.AtomicCompletedCount / (double)toUpdate.Length;
                    Progress.CompletedCount = state.AtomicCompletedCount;

                    if (state.UpdateBuckets.Count == 0)
                        continue;

                    while (state.UpdateBuckets.TryDequeue(out var list))
                    {
                        if (list.Count > 0)
                            await patchCache.InsertUnderTransactionAsync(list);
                    }
                }
            });

            Progress.IsIndeterminate = true;

            App.Logger.Info(nameof(VerifyFilesPhase), "Joining processing threads");
            try
            {
                await Task.WhenAll(threadTasks);
            }
            catch (Exception ex)
            {
                App.Logger.Error(nameof(VerifyFilesPhase), "Error verifying files", ex);
                throw;
            }
        }

        private void _Process(ProcessState state, PatchInfo[] toUpdate, CancellationToken ct = default)
        {
            using (var md5 = MD5.Create())
            using (var client = new AquaHttpClient())
            {
                const int streamingFileSize = 10 * 1024 * 1024; // 10MB
                byte[] bufferBytes = new byte[streamingFileSize];

                var entries = new List<PatchCacheEntry>();

                bool ProcessSingleFile()
                {
                    ct.ThrowIfCancellationRequested();

                    var index = Interlocked.Increment(ref state.AtomicIndex) - 1;
                    if (index >= toUpdate.Length)
                    {
                        state.UpdateBuckets.Enqueue(entries);
                        return true;
                    }

                    if (entries.Count > 50)
                    {
                        state.UpdateBuckets.Enqueue(entries);
                        entries = new List<PatchCacheEntry>();
                    }

                    var patch = toUpdate[index];
                    var path = Path.Combine(_InstallConfiguration.PSO2BinDirectory, patch.Name.Substring(0, patch.Name.Length - 4));

                    if (File.Exists(path))
                    {
                        using (var fs = File.OpenRead(path))
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

                            if (hashString == patch.Hash)
                            {
                                entries.Add(new PatchCacheEntry()
                                {
                                    Name = patch.Name,
                                    Hash = patch.Hash,
                                    LastWriteTime = new FileInfo(path).LastWriteTimeUtc.ToFileTimeUtc()
                                });
                                return false;
                            }
                        }
                    }

                    try
                    {
                        // perhaps we should look for a synchronous version of the network requests, or we
                        // should restructure this whole phase to be async only using thread pool work 
                        // for the CPU intensive tasks
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        var task = Task.Run(async () =>
                        {
                            using (var response = await client.GetAsync(patch.DownloadPath, HttpCompletionOption.ResponseHeadersRead, ct))
                            {
                                response.EnsureSuccessStatusCode();
                                using (var responseStream = await response.Content.ReadAsStreamAsync())
                                using (var fs = File.Create(path, 4096, FileOptions.Asynchronous))
                                    await responseStream.CopyToAsync(fs, 4096, ct);
                            }
                        });
                        task.GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error(nameof(VerifyFilesPhase), $"Error downloading file: \"{patch.DownloadPath}\"", ex);
                        throw;
                    }

                    entries.Add(new PatchCacheEntry()
                    {
                        Name = patch.Name,
                        Hash = patch.Hash,
                        LastWriteTime = new FileInfo(path).LastWriteTimeUtc.ToFileTimeUtc()
                    });

                    return false;
                }

                while (true)
                {
                    if (ProcessSingleFile())
                        break;
                    Interlocked.Increment(ref state.AtomicCompletedCount);
                }
            }
        }
    }
}

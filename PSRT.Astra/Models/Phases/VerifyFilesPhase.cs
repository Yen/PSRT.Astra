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
            if (toUpdate.Length == 0)
                return;

            var state = new ProcessState
            {
                AtomicIndex = 0,
                AtomicCompletedCount = 0,
                AtomicProcessCount = 0,
                UpdateBuckets = new ConcurrentQueue<List<PatchCacheEntry>>()
            };

            var exceptions = new ConcurrentBag<Exception>();
            var errorCancellationTokenSource = new CancellationTokenSource();
            var processCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(errorCancellationTokenSource.Token, ct);

            Progress.TotalCount = toUpdate.Length;

            App.Current.Logger.Info(nameof(VerifyFilesPhase), "Starting processing threads");
            Progress.IsIndeterminate = false;
            // TODO: processor affinity?
            var threads = Enumerable.Range(0, Environment.ProcessorCount).Select(i =>
            {
                var thread = new Thread(() =>
                {
                    Interlocked.Increment(ref state.AtomicProcessCount);
                    try
                    {
                        App.Current.Logger.Info(nameof(VerifyFilesPhase), $"Processing thread {i} started");

                        _ProcessAsync(state, toUpdate, processCancellationTokenSource.Token).GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException ex)
                    {
                        App.Current.Logger.Error(nameof(VerifyFilesPhase), $"Processing thread {i} canceled", ex);
                    }
                    catch (Exception ex)
                    {
                        App.Current.Logger.Error(nameof(VerifyFilesPhase), $"Exception in processing thread {i}", ex);
                        exceptions.Add(ex);

                        errorCancellationTokenSource.Cancel();
                    }
                    finally
                    {
                        Interlocked.Decrement(ref state.AtomicProcessCount);

                        App.Current.Logger.Info(nameof(VerifyFilesPhase), $"Processing thread {i} ended");
                    }
                });
                thread.Name = $"{nameof(VerifyFilesPhase)}({i})";
                thread.Start();

                return thread;
            }).ToArray();

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

            App.Current.Logger.Info(nameof(VerifyFilesPhase), "Joining processing threads");
            foreach (var t in threads)
                await Task.Factory.StartNew(() => t.Join(), TaskCreationOptions.LongRunning);

            if (exceptions.Count > 0)
            {
                var aggregate = new AggregateException("Error verifying files", exceptions);
                App.Current.Logger.Error(nameof(VerifyFilesPhase), "Error verifying files", aggregate);
                throw aggregate;
            }
        }

        private async Task _ProcessAsync(ProcessState state, PatchInfo[] toUpdate, CancellationToken ct = default)
        {
            using (var md5 = MD5.Create())
            using (var client = new AquaHttpClient())
            {
                const int streamingFileSize = 10 * 1024 * 1024; // 10MB
                byte[] bufferBytes = new byte[streamingFileSize];

                var entries = new List<PatchCacheEntry>();

                async Task<bool> ProcessSingleFileAsync()
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
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        using (var responseStream = await client.GetStreamAsync(patch.DownloadPath))
                        using (var fs = File.Create(path, 4096, FileOptions.Asynchronous))
                            await responseStream.CopyToAsync(fs, 4096, ct);
                    }
                    catch (Exception ex)
                    {
                        App.Current.Logger.Error(nameof(VerifyFilesPhase), "Error downloading file", ex);
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
                    if (await ProcessSingleFileAsync())
                        break;
                    Interlocked.Increment(ref state.AtomicCompletedCount);
                }
            }
        }
    }
}

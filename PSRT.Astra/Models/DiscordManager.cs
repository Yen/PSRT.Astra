using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.Models
{
    public class DiscordManager : IDisposable
    {
        private readonly CancellationTokenSource _DisposedTokenSource;

        public bool StatusVisible { get; set; }
        private bool? LastStatusVisible { get; set; }

        private Discord.Discord _Discord;

        public DiscordManager()
        {
            _DisposedTokenSource = new CancellationTokenSource();
            App.Logger.Info(nameof(DiscordManager), "Discord manager created");
            App.Current.Dispatcher.Invoke(async () => await ConcurrencyUtils.RunOnDedicatedThreadAsync(_BackgroundLoop));
        }

        private Discord.Discord _CreateDiscordClient()
        {
            var discord = new Discord.Discord(630023869389996044, (long)Discord.CreateFlags.NoRequireDiscord);
            App.Logger.Info(nameof(DiscordManager), "Discord client created");
            discord.SetLogHook(Discord.LogLevel.Debug, (level, message) =>
            {
                const string LogDomain = nameof(DiscordManager) + "/LogHook";

                switch (level)
                {
                    case Discord.LogLevel.Debug:
                    case Discord.LogLevel.Info:
                        App.Logger.Info(LogDomain, message);
                        break;
                    case Discord.LogLevel.Warn:
                        App.Logger.Warning(LogDomain, message);
                        break;
                    case Discord.LogLevel.Error:
                        App.Logger.Error(LogDomain, message);
                        break;
                }
            });

            return discord;
        }

        private void _PollDiscord(Discord.Discord discord)
        {
            _DisposedTokenSource.Token.ThrowIfCancellationRequested();
            discord.RunCallbacks();
            Thread.Sleep(250);
        }

        private void _BackgroundLoop()
        {
            App.Logger.Info(nameof(DiscordManager), "Background loop started");
            try
            {
                while (true)
                {
                    _DisposedTokenSource.Token.ThrowIfCancellationRequested();

                    var workingVisible = StatusVisible;
                    if (workingVisible != LastStatusVisible)
                    {
                        if (workingVisible)
                        {
                            _Discord?.Dispose();
                            _Discord = _CreateDiscordClient();

                            var activity = new Discord.Activity
                            {
                                State = "Optimised PSO2 Patcher",
                                Details = "https://astra.yen.gg",
                                Assets = new Discord.ActivityAssets
                                {
                                    LargeImage = "icon-borderless",
                                    LargeText = "PSRT Astra"
                                }
                            };

                            Discord.Result? result = null;
                            _Discord.GetActivityManager().UpdateActivity(activity, res => result = res);
                            while (result == null)
                                _PollDiscord(_Discord);

                            if (result == Discord.Result.Ok)
                                App.Logger.Info(nameof(DiscordManager), "Activity updated");
                            else
                                App.Logger.Info(nameof(DiscordManager), $"Activity update failed: {result}");
                        }
                        else if (_Discord != null)
                        {
                            Discord.Result? result = null;
                            _Discord.GetActivityManager().ClearActivity(res => result = res);
                            while (result == null)
                                _PollDiscord(_Discord);

                            _Discord.Dispose();
                            _Discord = null;

                            if (result == Discord.Result.Ok)
                                App.Logger.Info(nameof(DiscordManager), "Activity cleared");
                            else
                                App.Logger.Info(nameof(DiscordManager), $"Activity clearing failed: {result}");
                        }
                        LastStatusVisible = workingVisible;
                    }

                    Thread.Sleep(250);
                }
            }
            catch (OperationCanceledException)
            {
                App.Logger.Info(nameof(DiscordManager), "Background loop canceled");
            }
            catch (Exception ex)
            {
                App.Logger.Error(nameof(DiscordManager), "Fatal error in background loop", ex);
            }
            finally
            {
                _Discord?.Dispose();
            }
        }

        public void Dispose()
        {
            _DisposedTokenSource.Cancel();
        }
    }
}

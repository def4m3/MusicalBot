using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Lavalink;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicalBot.CommandModules
{
    public class SlashMusicCommands : ApplicationCommandModule
    {
        private static Dictionary<ulong, List<LavalinkTrack>> _Queues { get; set; } = new Dictionary<ulong, List<LavalinkTrack>>();

        private static bool _IsSubscribed = false;

        private DiscordEmbedBuilder _connectionErrorEmbed = new DiscordEmbedBuilder()
        {
            Color = DiscordColor.Red,
            Title = "Error",
            Description = "Connection error",
            Timestamp = DateTime.Now

        };

        private DiscordEmbedBuilder _noMatchesEmbed = new DiscordEmbedBuilder()
        {
            Color = DiscordColor.Red,
            Title = "Error",
            Description = "No matches",
            Timestamp = DateTime.Now
        };

        private DiscordEmbedBuilder _noCurrentTrackEmbed = new DiscordEmbedBuilder()
        {
            Title = "Error",
            Description = $"No track is currently playing",
            Color = DiscordColor.DarkRed,
            Timestamp = DateTime.Now


        };

        private DiscordEmbedBuilder _playingInAnotherChannelEmbed = new DiscordEmbedBuilder()
        {
            Title = "Error",
            Description = $"You cannot control the bot while it is playing music in another channel",
            Color = DiscordColor.DarkRed,
            Timestamp = DateTime.Now
        };

        [SlashCommand("play", "Find the song on Youtube via text request and play")]
        public async Task PlaySong(InteractionContext ctx, [Option("query", "Song name")] string query)
        {
            await ctx.DeferAsync();

            var lavalinkInstance = ctx.Client.GetLavalink();

            if (ctx.Member.VoiceState == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Color = DiscordColor.Red,
                    Title = "Error",
                    Description = "You are not currently in the voice channel",
                    Timestamp = DateTime.Now

                }));
                return;
            }

            DiscordChannel userVC = ctx.Member.VoiceState.Channel;

            if (!lavalinkInstance.ConnectedNodes.Any())
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_connectionErrorEmbed));
                return;
            }

            var node = lavalinkInstance.ConnectedNodes.Values.First();

            await node.ConnectAsync(userVC);

            var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (connection == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_connectionErrorEmbed));
                return;
            }

            if (connection.Channel != userVC)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_playingInAnotherChannelEmbed));
                return;
            }

            LavalinkLoadResult? loadResult;
            try
            {
                loadResult = await node.Rest.GetTracksAsync(query);
            }
            catch
            {
                loadResult = await node.Rest.GetTracksAsync(query);
            }

            if (loadResult == null ||
                loadResult.LoadResultType == LavalinkLoadResultType.NoMatches ||
                loadResult.LoadResultType == LavalinkLoadResultType.LoadFailed)
            {

                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_noMatchesEmbed));
                return;
            }

            var musicTrack = loadResult.Tracks.First();

            var nowPlayingEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Green,
                Title = $"Playing music in the channel {userVC.Name}",
                Description = $"Now playing: `{musicTrack.Title}` \n " +
                              $"Author: `{musicTrack.Author}` \n " +
                              $"Duration: {musicTrack.Length} \n" +
                              $"URL: [Link]({musicTrack.Uri}) \n",
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail()
                {
                    Url = "https://media.tenor.com/3cAzAWunJ3gAAAAd/dj-eban-dj.gif"
                },
                Timestamp = DateTime.Now
            };

            if (connection.CurrentState.CurrentTrack != null)
            {
                if (!_Queues.TryGetValue(ctx.Guild.Id, out var queue))
                {
                    queue = new List<LavalinkTrack>();

                    _Queues.Add(ctx.Guild.Id, queue);
                }

                queue.Add(musicTrack);

                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = $"The track `{musicTrack.Author} - {musicTrack.Title}` was successfully added to the queue",
                    Description = $"Position in the queue: {queue.IndexOf(musicTrack) + 1}",
                    Color = DiscordColor.Green,
                    Timestamp = DateTime.Now

                }));

            }

            if (connection.CurrentState.CurrentTrack == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(nowPlayingEmbed));

                await connection.PlayAsync(musicTrack);

                if (_IsSubscribed == false)
                {
                    connection.PlaybackFinished += async (s, e) =>
                    {
                        if (!_Queues.TryGetValue(s.Guild.Id, out var queue))
                            return;

                        if (queue.Count == 0)
                        {
                            var msgBuilder = new DiscordMessageBuilder();
                            msgBuilder.AddEmbed(new DiscordEmbedBuilder()
                            {
                                Color = DiscordColor.MidnightBlue,
                                Timestamp = DateTime.Now,
                                Title = "There are no more tracks in the queue",
                                Description = "Playback of tracks in the channel  `" + ctx.Channel.Name + "` is finished \n" +
                                "Leave the channel ?"
                            });
                            msgBuilder.AddComponents(new DiscordComponent[]
                            {
                                new DiscordButtonComponent(ButtonStyle.Success,"leaveChannel","Yes"),
                                new DiscordButtonComponent(ButtonStyle.Success,"stayInChannel","No")
                            });

                            await ctx.Channel.SendMessageAsync(msgBuilder);
                            return;
                        }

                        var nextTrack = queue[0];
                        queue.RemoveAt(0);

                        await s.PlayAsync(nextTrack);
                        await ctx.Channel.SendMessageAsync(embed: new DiscordEmbedBuilder()
                        {
                            Color = DiscordColor.Green,
                            Title = $"Playing music in the channel {userVC.Name}",
                            Description = $"Now playing: `{nextTrack.Title}` \n " +
                              $"Author: `{nextTrack.Author}` \n " +
                              $"Duration: {musicTrack.Length} \n" +
                              $"URL: [Link]({nextTrack.Uri})",
                            Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail()
                            {
                                Url = "https://media.tenor.com/3cAzAWunJ3gAAAAd/dj-eban-dj.gif"
                            },
                            Timestamp = DateTime.Now
                        });

                    };

                    _IsSubscribed = true;
                }

            }


        }

        [SlashCommand("playURL", "Find the song on Youtube via URL and play")]
        public async Task PlaySongViaURL(InteractionContext ctx, [Option("url", "Link to the song")] string url)
        {
            await ctx.DeferAsync();

            var lavalinkInstance = ctx.Client.GetLavalink();

            if (ctx.Member.VoiceState == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Color = DiscordColor.Red,
                    Title = "Error",
                    Description = "You are not currently in the voice channel",
                    Timestamp = DateTime.Now

                }));
                return;
            }

            DiscordChannel userVC = ctx.Member.VoiceState.Channel;

            if (!lavalinkInstance.ConnectedNodes.Any())
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_connectionErrorEmbed));
                return;
            }

            var node = lavalinkInstance.ConnectedNodes.Values.First();

            await node.ConnectAsync(userVC);

            var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (connection == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_connectionErrorEmbed));
                return;
            }

            if (connection.Channel != userVC)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_playingInAnotherChannelEmbed));
                return;
            }

            var uri = new Uri(url);
            LavalinkLoadResult? loadResult;
            try
            {
                loadResult = await node.Rest.GetTracksAsync(uri);
            }
            catch
            {
                loadResult = await node.Rest.GetTracksAsync(uri);
            }

            if (loadResult == null ||
                loadResult.LoadResultType == LavalinkLoadResultType.NoMatches ||
                loadResult.LoadResultType == LavalinkLoadResultType.LoadFailed)
            {

                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_noMatchesEmbed));
                return;
            }

            var musicTrack = loadResult.Tracks.First();

            var nowPlayingEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Green,
                Title = $"Playing music in the channel {userVC.Name}",
                Description = $"Now playing: `{musicTrack.Title}` \n " +
                              $"Author: `{musicTrack.Author}` \n " +
                              $"Duration: {musicTrack.Length} \n" +
                              $"URL: [Link]({musicTrack.Uri})",
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail()
                {
                    Url = "https://media.tenor.com/3cAzAWunJ3gAAAAd/dj-eban-dj.gif"
                },
                Timestamp = DateTime.Now
            };

            if (connection.CurrentState.CurrentTrack != null)
            {
                if (!_Queues.TryGetValue(ctx.Guild.Id, out var queue))
                {
                    queue = new List<LavalinkTrack>();

                    _Queues.Add(ctx.Guild.Id, queue);
                }

                queue.Add(musicTrack);

                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = $"The track `{musicTrack.Author} - {musicTrack.Title}` was successfully added to the queue",
                    Description = $"Position in the queue: {queue.IndexOf(musicTrack) + 1}",
                    Color = DiscordColor.Green,
                    Timestamp = DateTime.Now

                }));

            }

            if (connection.CurrentState.CurrentTrack == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(nowPlayingEmbed));

                await connection.PlayAsync(musicTrack);

                if (_IsSubscribed == false)
                {
                    connection.PlaybackFinished += async (s, e) =>
                    {
                        if (!_Queues.TryGetValue(s.Guild.Id, out var queue))
                            return;

                        if (queue.Count == 0)
                        {
                            var msgBuilder = new DiscordMessageBuilder();
                            msgBuilder.AddEmbed(new DiscordEmbedBuilder()
                            {
                                Color = DiscordColor.MidnightBlue,
                                Timestamp = DateTime.Now,
                                Title = "There are no more tracks in the queue",
                                Description = "Playback of tracks in the channel `" + ctx.Channel.Name + "`is finished\n" +
                                "Leave the channel ?"
                            });
                            msgBuilder.AddComponents(new DiscordComponent[]
                            {
                                new DiscordButtonComponent(ButtonStyle.Success,"leaveChannel","Yes"),
                                new DiscordButtonComponent(ButtonStyle.Success,"stayInChannel","No")
                            });

                            await ctx.Channel.SendMessageAsync(msgBuilder);
                            return;
                        }

                        var nextTrack = queue[0];
                        queue.RemoveAt(0);

                        await s.PlayAsync(nextTrack);
                        await ctx.Channel.SendMessageAsync(embed: new DiscordEmbedBuilder()
                        {
                            Color = DiscordColor.Green,
                            Title = $"Playing music in the channel {userVC.Name}",
                            Description = $"Now playing: `{nextTrack.Title}` \n " +
                                          $"Author: `{nextTrack.Author}` \n " +
                                          $"Duration: {musicTrack.Length} \n" +
                                          $"URL: [Link]({nextTrack.Uri})",
                            Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail()
                            {
                                Url = "https://media.tenor.com/3cAzAWunJ3gAAAAd/dj-eban-dj.gif"
                            },
                            Timestamp = DateTime.Now
                        });

                    };

                    _IsSubscribed = true;
                }
            }




        }

        [SlashCommand("skip", "Skips the current track")]
        public async Task SkipSong(InteractionContext ctx)
        {
            await ctx.DeferAsync();

            if (_Queues.TryGetValue(ctx.Guild.Id, out var queue) && queue.Count != 0)
            {
                var lavalinkInstance = ctx.Client.GetLavalink();

                var node = lavalinkInstance.ConnectedNodes.Values.First();

                LavalinkGuildConnection? connection = node.GetGuildConnection(ctx.Guild);

                if (connection != null)
                {
                    if (connection.Channel != ctx.Member.VoiceState.Channel)
                    {
                        await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_playingInAnotherChannelEmbed));
                        return;
                    }

                    var currentTrack = connection.CurrentState.CurrentTrack;

                    await connection.StopAsync();

                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                    {
                        Color = DiscordColor.Green,
                        Title = $"Track `{currentTrack.Author} - {currentTrack.Title}` skipped",
                        Timestamp = DateTime.Now
                    }));
                }
                else return;
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = "Error",
                    Description = "There is no track queue on the server" + $"\n`{ctx.Guild.Name}`",
                    Color = DiscordColor.Red,
                    Timestamp = DateTime.Now

                }));
            }


        }

        [SlashCommand("showQueue", "Shows full queue")]
        public async Task ShowQueue(InteractionContext ctx)
        {
            await ctx.DeferAsync();

            if (_Queues.TryGetValue(ctx.Guild.Id, out var queue) && queue.Count != 0)
            {
                var builder = new StringBuilder();

                for (int i = 0; i < queue.Count; i++)
                {
                    builder.AppendLine($"{i + 1}. {queue[i].Author} - {queue[i].Title}");
                }


                var result = builder.ToString();

                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = "Queue of tracks on the server " + ctx.Guild.Name,
                    Description = "```" + result + "```",
                    Color = DiscordColor.PhthaloBlue,
                    Timestamp = DateTime.Now

                }));
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = "Error",
                    Description = "There is no track queue on the server" + $"\n`{ctx.Guild.Name}`",
                    Color = DiscordColor.Red,
                    Timestamp = DateTime.Now
                }));
            }
        }

        [SlashCommand("clear", "Clears the queue")]
        public async Task Clear(InteractionContext ctx)
        {
            await ctx.DeferAsync();

            if (_Queues.TryGetValue(ctx.Guild.Id, out var queue) && queue.Count != 0)
            {
                var builder = new StringBuilder();

                for (int i = 0; i < queue.Count; i++)
                {
                    builder.AppendLine($"{i + 1}. {queue[i].Author} - {queue[i].Title}");
                }


                var result = builder.ToString();

                queue.Clear();

                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = "Queue of tracks on the server " + ctx.Guild.Name + "successfully cleared",
                    Description = "The queue that was cleared: \n```" + result + "```",
                    Color = DiscordColor.PhthaloBlue,
                    Timestamp = DateTime.Now

                }));
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = "Error",
                    Description = "There is no track queue on the server" + $"\n`{ctx.Guild.Name}`",
                    Color = DiscordColor.Red,
                    Timestamp = DateTime.Now
                }));
            }
        }

        [SlashCommand("pause", "Pauses playback")]
        public async Task Pause(InteractionContext ctx)
        {
            await ctx.DeferAsync();

            var lavalinkInstance = ctx.Client.GetLavalink();

            var node = lavalinkInstance.ConnectedNodes.Values.First();

            LavalinkGuildConnection? connection = node.GetGuildConnection(ctx.Guild);


            if (connection != null && connection.CurrentState.CurrentTrack != null)
            {
                if (connection.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_playingInAnotherChannelEmbed));
                    return;
                }

                await connection.PauseAsync();
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = "Success",
                    Description = $"Track playback `{connection.CurrentState.CurrentTrack.Title}` stopped",
                    Color = DiscordColor.SpringGreen,
                    Timestamp = DateTime.Now

                }));
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_noCurrentTrackEmbed));
            }

        }

        [SlashCommand("resume", "resumes playback")]
        public async Task Resume(InteractionContext ctx)
        {
            await ctx.DeferAsync();

            var lavalinkInstance = ctx.Client.GetLavalink();

            var node = lavalinkInstance.ConnectedNodes.Values.First();

            LavalinkGuildConnection? connection = node.GetGuildConnection(ctx.Guild);

            if (connection != null && connection.CurrentState.CurrentTrack != null)
            {

                if (connection.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_playingInAnotherChannelEmbed));
                    return;
                }

                await connection.ResumeAsync();
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = "Success",
                    Description = $"Track playback `{connection.CurrentState.CurrentTrack.Title}` resumed",
                    Color = DiscordColor.SpringGreen,
                    Timestamp = DateTime.Now

                }));
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_noCurrentTrackEmbed));
            }

        }

        [SlashCommand("stop", "stop playback and leave the channel")]
        public async Task Stop(InteractionContext ctx)
        {
            await ctx.DeferAsync();

            var lavalinkInstance = ctx.Client.GetLavalink();

            var node = lavalinkInstance.ConnectedNodes.Values.First();

            LavalinkGuildConnection? connection = node.GetGuildConnection(ctx.Guild);

            if (connection != null && connection.CurrentState.CurrentTrack != null)
            {
                if (connection.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_playingInAnotherChannelEmbed));
                    return;
                }

                if (_Queues.TryGetValue(ctx.Guild.Id, out var queue))
                {
                    queue.Clear();
                }

                await connection.StopAsync();
                await connection.DisconnectAsync();

                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = "Success",
                    Description = $"Playing tracks on the server `{ctx.Guild.Name}` stopped",
                    Color = DiscordColor.SpringGreen,
                    Timestamp = DateTime.Now
                }));
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_noCurrentTrackEmbed));
            }

        }

        [SlashCommand("volume", "set volume for playback")]
        public async Task SetVolume(InteractionContext ctx, [Choice("10%", 10)]
                                                            [Choice("50%", 50)]
                                                            [Choice("100%", 100)]
                                                            [Option("percentage", "volume percentage")] long volume)
        {
            await ctx.DeferAsync();

            var lavalinkInstance = ctx.Client.GetLavalink();

            var node = lavalinkInstance.ConnectedNodes.Values.First();

            LavalinkGuildConnection? connection = node.GetGuildConnection(ctx.Guild);

            if (connection != null && connection.CurrentState.CurrentTrack != null)
            {

                if (connection.Channel != ctx.Member.VoiceState.Channel)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_playingInAnotherChannelEmbed));
                    return;
                }

                await connection.SetVolumeAsync((int)volume);
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = "Success",
                    Description = $"The volume is successfully set to {volume}%",
                    Color = DiscordColor.SpringGreen,
                    Timestamp = DateTime.Now

                }));
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = "Error",
                    Description = $"No track is currently playing",
                    Color = DiscordColor.DarkRed,
                    Timestamp = DateTime.Now

                }));
            }

        }

        [SlashCommand("trackRN", "gets the track which is playing right now")]
        public async Task TrackRN(InteractionContext ctx)
        {
            await ctx.DeferAsync();

            var lavalinkInstance = ctx.Client.GetLavalink();

            var node = lavalinkInstance.ConnectedNodes.Values.First();

            LavalinkGuildConnection? connection = node.GetGuildConnection(ctx.Guild);

            if (connection != null && connection.CurrentState.CurrentTrack != null)
            {
                var trackRightNow = connection.CurrentState.CurrentTrack;
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Color = DiscordColor.Chartreuse,
                    Title = "Now playing",
                    Description = $"Title: `{trackRightNow.Title}` \n" +
                    $"Author: `{trackRightNow.Author}` \n" +
                    $"Duration: {trackRightNow.Length} \n" +
                    $"Playback position now: {connection.CurrentState.PlaybackPosition.ToString("c").Substring(3, 5)}",
                    Timestamp = DateTime.Now
                }));
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(_noCurrentTrackEmbed));
            }
        }
        public static async Task LeaveChannelButton(DiscordClient sender, ComponentInteractionCreateEventArgs e)
        {
            await e.Message.DeleteAsync();

            var lavalinkInstance = sender.GetLavalink();

            var node = lavalinkInstance.ConnectedNodes.Values.First();

            LavalinkGuildConnection? connection = node.GetGuildConnection(e.Guild);

            if (connection != null && connection.CurrentState.CurrentTrack == null)
            {
                if (_Queues.TryGetValue(e.Guild.Id, out var queue))
                {
                    queue.Clear();
                }
                var msgBuilder = new DiscordInteractionResponseBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = "Bye-bye",
                    Color = DiscordColor.Blurple,
                    Description = "Left your wonderful voice"
                });
                msgBuilder.IsEphemeral = true;
                await connection.DisconnectAsync();
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, msgBuilder);
            }
            else
            {
                return;
            }

        }
    }
}

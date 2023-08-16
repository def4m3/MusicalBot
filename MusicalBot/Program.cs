using DSharpPlus;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.Interactivity;
using DSharpPlus.Net;
using DSharpPlus.Lavalink;
using DSharpPlus.SlashCommands;
using MusicalBot.CommandModules;

class Program
{
    static void Main(string[] args)
    {
        MainAsync().GetAwaiter().GetResult();
    }

    static async Task MainAsync()
    {
        var discord = new DiscordClient(new DiscordConfiguration()
        {
            Token = "", // PUT YOUR BOT TOKEN HERE
            TokenType = TokenType.Bot,
            Intents = DiscordIntents.All,
            MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Debug,
            AutoReconnect = true,
        });

        discord.UseInteractivity();

        var endpoint = new ConnectionEndpoint
        {
            Hostname = "",
            Port = 0,
            Secured = false,
        };

        var lavalinkConfig = new LavalinkConfiguration()
        {
            Password = "password",
            RestEndpoint = endpoint,
            SocketEndpoint = endpoint,
        };

        var lavalink = discord.UseLavalink();

        var slashcommands = discord.UseSlashCommands();

        slashcommands.RegisterCommands<SlashMusicCommands>(); // PUT YOUR GUILD ID HERE

        discord.ComponentInteractionCreated += Discord_ComponentInteractionCreated;

        await discord.ConnectAsync();
        await lavalink.ConnectAsync(lavalinkConfig);
        await Task.Delay(-1);
    }

    private async static Task Discord_ComponentInteractionCreated(DiscordClient sender, DSharpPlus.EventArgs.ComponentInteractionCreateEventArgs e)
    {
        switch (e.Interaction.Data.CustomId)
        {
            case "leaveChannel":
                {
                    await SlashMusicCommands.LeaveChannelButton(sender, e);
                    break;
                }

            case "stayInChannel":
                {
                    await e.Message.DeleteAsync();
                    break;
                }
        }
    }
}
using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using SkepyUniverseIndustry_DiscordBot.Events;

namespace SkepyUniverseIndustry_DiscordBot
{
    class Program
    {
        private DiscordSocketClient _client;
        private ulong _channelId;
        private readonly MessageReceived _obj = new();
        private string? _token;

        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            _client = new DiscordSocketClient();
            _client.Log += Log;
            _channelId = Convert.ToUInt64(Environment.GetEnvironmentVariable("DISCORD_CHANNEL_ID"));
            _client.MessageReceived += MessageReceived;

            _token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");

            await _client.LoginAsync(TokenType.Bot, _token);
            await _client.StartAsync();
            await Task.Delay(-1);
        }
        private async Task MessageReceived(SocketMessage message)
        {
            if (_client.GetChannel(_channelId) == message.Channel)
            {
                _obj.MessageReceivedHandler(message);
            }
            if (_client.GetChannel(_channelId) == message.Channel && message.Content.Equals("!exit"))
            {
                ExitApplication();
            }
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
        
        private void ExitApplication()
        {
            Environment.Exit(0);
        }
    }
}
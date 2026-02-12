namespace LavaLinkLouieBot.Helpers;

public sealed class MusicMessageService
{
    private readonly Dictionary<ulong, (ulong ChannelId, ulong MessageId)> _guildMessageMap = new();

    public async Task<IUserMessage> SendOrUpdateAsync(SocketInteractionContext context, string content, MessageComponent components)
    {
        if (_guildMessageMap.TryGetValue(context.Guild.Id, out var storedMessage))
        {
            // Try editing existing message
            var channel = await context.Client.GetChannelAsync(storedMessage.ChannelId) as IMessageChannel;
            if (channel != null)
            {
                var message = await channel.GetMessageAsync(storedMessage.MessageId) as IUserMessage;
                if (message != null)
                {
                    await message.ModifyAsync(m =>
                    {
                        m.Content = content;
                        m.Components = components;
                    });
                    return message;
                }
            }
        }

        // Otherwise send new one
        var sent = await context.Channel.SendMessageAsync(content, components: components);
        _guildMessageMap[context.Guild.Id] = (context.Channel.Id, sent.Id);
        return sent;
    }

    public async Task<bool> UpdateByGuildAsync(
        DiscordSocketClient client,
        ulong guildId,
        string content,
        MessageComponent components)
    {
        if (!_guildMessageMap.TryGetValue(guildId, out var storedMessage))
        {
            return false;
        }

        var channel = await client.GetChannelAsync(storedMessage.ChannelId) as IMessageChannel;
        if (channel is null)
        {
            return false;
        }

        var message = await channel.GetMessageAsync(storedMessage.MessageId) as IUserMessage;
        if (message is null)
        {
            _guildMessageMap.Remove(guildId);
            return false;
        }

        await message.ModifyAsync(m =>
        {
            m.Content = content;
            m.Components = components;
        });

        return true;
    }

    public void Clear(ulong guildId)
    {
        _guildMessageMap.Remove(guildId);
    }
}

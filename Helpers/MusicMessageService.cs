namespace LavaLinkLouieBot.Helpers;

public sealed class MusicMessageService
{
    private readonly Dictionary<ulong, ulong> _guildMessageMap = new();

    public async Task<IUserMessage> SendOrUpdateAsync(SocketInteractionContext context, string content, MessageComponent components)
    {
        if (_guildMessageMap.TryGetValue(context.Guild.Id, out var messageId))
        {
            // Try editing existing message
            var channel = await context.Client.GetChannelAsync(context.Channel.Id) as IMessageChannel;
            if (channel != null)
            {
                var message = await channel.GetMessageAsync(messageId) as IUserMessage;
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
        _guildMessageMap[context.Guild.Id] = sent.Id;
        return sent;
    }

    public void Clear(ulong guildId)
    {
        _guildMessageMap.Remove(guildId);
    }
}

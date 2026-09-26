// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.Chat;
using osu.Game.Overlays.Chat;
using osu.Game.Tournament.IPC;
using osu.Game.Tournament.Models;

namespace osu.Game.Tournament.Components
{
    public partial class TournamentMatchChatDisplay : StandAloneChatDisplay
    {
        private readonly Bindable<string> channelName = new Bindable<string>();

        private IAPIProvider? api;
        private ScheduledDelegate? poll;
        private Channel? currentChannel;

        [Resolved]
        private LadderInfo ladderInfo { get; set; } = null!;

        public TournamentMatchChatDisplay()
        {
            RelativeSizeAxes = Axes.X;
            Height = 144;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;

            CornerRadius = 0;
        }

        [BackgroundDependencyLoader]
        private void load(MatchIPCInfo ipc, IAPIProvider api)
        {
            this.api = api;

            channelName.BindTo(ipc.ChatChannel);
            channelName.BindValueChanged(c =>
            {
                Logger.Log($"Tournament IPC chat channel changed from '{c.OldValue}' to '{c.NewValue}'", LoggingTarget.Network);
                if (long.TryParse(c.OldValue, out long oldChannelId) && oldChannelId > 0)
                {
                    poll?.Cancel();
                }

                if (long.TryParse(c.NewValue, out long newChannelId) && newChannelId > 0)
                {
                    var channel = new Channel
                    {
                        Id = newChannelId,
                        Type = ChannelType.Public
                    };

                    Logger.Log($"Joining tournament chat channel {newChannelId} as user {api.LocalUser.Value.Id}", LoggingTarget.Network);
                    currentChannel = channel;
                    Channel.Value = channel;
                    poll = Scheduler.AddDelayed(pollMessages, 5000, true);
                }
            }, true);
        }

        private void pollMessages()
        {
            if (api == null || currentChannel == null || !api.IsLoggedIn)
                return;

            var request = new GetMessagesRequest(currentChannel);
            request.Success += messages => currentChannel.AddNewMessages(messages.ToArray());
            request.Failure += error => Logger.Error(error, $"Failed to poll tournament chat channel {currentChannel.Id}");
            api.Queue(request);
        }

        public void Expand() => this.FadeIn(300);

        public void Contract() => this.FadeOut(200);

        protected override ChatLine? CreateMessage(Message message)
        {
            if (message.Content.StartsWith("!mp", StringComparison.Ordinal))
                return null;

            return new MatchMessage(message, ladderInfo);
        }

        protected override StandAloneDrawableChannel CreateDrawableChannel(Channel channel) => new MatchChannel(channel);

        public partial class MatchChannel : StandAloneDrawableChannel
        {
            public MatchChannel(Channel channel)
                : base(channel)
            {
                ScrollbarVisible = false;
            }
        }

        protected partial class MatchMessage : StandAloneMessage
        {
            public MatchMessage(Message message, LadderInfo info)
                : base(message)
            {
                if (info.CurrentMatch.Value is TournamentMatch match)
                {
                    if (match.Team1.Value?.Players.Any(u => u.OnlineID == Message.Sender.OnlineID) == true)
                        UsernameColour = TournamentGame.COLOUR_RED;
                    else if (match.Team2.Value?.Players.Any(u => u.OnlineID == Message.Sender.OnlineID) == true)
                        UsernameColour = TournamentGame.COLOUR_BLUE;
                }
            }
        }
    }
}

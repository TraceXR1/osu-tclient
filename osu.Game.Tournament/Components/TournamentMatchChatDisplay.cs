// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.Chat;
using osu.Game.Online.Notifications.WebSocket;
using osu.Game.Online.Notifications.WebSocket.Events;
using osu.Game.Online.Notifications.WebSocket.Requests;
using osu.Game.Overlays.Chat;
using osu.Game.Tournament.IPC;
using osu.Game.Tournament.Models;

namespace osu.Game.Tournament.Components
{
    public partial class TournamentMatchChatDisplay : StandAloneChatDisplay
    {
        private readonly Bindable<string> channelName = new Bindable<string>();
        private readonly IBindable<APIState> apiState = new Bindable<APIState>();

        private IAPIProvider? api;
        private INotificationsClient? notificationsClient;
        private ScheduledDelegate? scheduledStart;
        private Channel? currentChannel;
        private bool disposed;

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
            notificationsClient = api.NotificationsClient;
            notificationsClient.MessageReceived += onSocketMessage;
            notificationsClient.IsConnected.ValueChanged += onConnectionChanged;
            if (notificationsClient.IsConnected.Value)
                Schedule(() => _ = startChat());

            channelName.BindTo(ipc.ChatChannel);
            channelName.BindValueChanged(c =>
            {
                Logger.Log($"Tournament IPC chat channel changed from '{c.OldValue}' to '{c.NewValue}'", LoggingTarget.Network);
                currentChannel = null;
                Channel.Value = null;

                if (long.TryParse(c.NewValue, out long newChannelId) && newChannelId > 0)
                {
                    var channel = new Channel
                    {
                        Id = newChannelId,
                        Type = ChannelType.Multiplayer
                    };

                    Logger.Log($"Displaying tournament multiplayer chat channel {newChannelId}", LoggingTarget.Network);
                    currentChannel = channel;
                    Channel.Value = channel;
                    if (apiState.Value == APIState.Online)
                        fetchInitialMessages(channel);
                }
            }, true);

            apiState.BindTo(api.State);
            apiState.BindValueChanged(state =>
            {
                if (state.NewValue == APIState.Online)
                    Schedule(() =>
                    {
                        if (currentChannel is Channel channel)
                            fetchInitialMessages(channel);
                    });
            }, true);
        }

        private void onConnectionChanged(ValueChangedEvent<bool> connection)
        {
            Schedule(() =>
            {
                scheduledStart?.Cancel();

                if (connection.NewValue && !disposed)
                    _ = startChat();
            });
        }

        private async Task startChat()
        {
            if (notificationsClient?.IsConnected.Value != true || disposed)
                return;

            try
            {
                await notificationsClient.SendAsync(new StartChatRequest()).ConfigureAwait(false);
                Logger.Log("Listening to tournament chat websocket messages", LoggingTarget.Network);
            }
            catch (Exception error)
            {
                Logger.Error(error, "Failed to start tournament chat websocket subscription");
                Schedule(() =>
                {
                    if (!disposed && notificationsClient?.IsConnected.Value == true)
                        scheduledStart = Scheduler.AddDelayed(() => _ = startChat(), 5000);
                });
            }
        }

        private void onSocketMessage(SocketMessage socketMessage)
        {
            if (socketMessage.Event != "chat.message.new" || socketMessage.Data == null)
                return;

            NewChatMessageData? data;

            try
            {
                data = socketMessage.Data.ToObject<NewChatMessageData>();
            }
            catch (Exception error)
            {
                Logger.Error(error, "Failed to read tournament chat websocket message");
                return;
            }

            if (data?.Messages == null)
                return;

            Schedule(() =>
            {
                if (currentChannel is Channel channel)
                    channel.AddNewMessages(data.Messages.Where(message => message.ChannelId == channel.Id).ToArray());
            });
        }

        private void fetchInitialMessages(Channel channel)
        {
            if (api == null || !api.IsLoggedIn)
                return;

            var request = new GetMessagesRequest(channel);
            request.Success += messages => Schedule(() =>
            {
                if (currentChannel == channel)
                    channel.AddNewMessages(messages.ToArray());
            });
            request.Failure += error => Logger.Error(error, $"Failed to load tournament chat channel {channel.Id}");
            api.Queue(request);
        }

        protected override void Dispose(bool isDisposing)
        {
            scheduledStart?.Cancel();
            disposed = true;
            apiState.UnbindAll();
            if (notificationsClient != null)
            {
                notificationsClient.MessageReceived -= onSocketMessage;
                notificationsClient.IsConnected.ValueChanged -= onConnectionChanged;
            }
            base.Dispose(isDisposing);
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
                    {
                        UsernameColour = info.RedSide.Colour.Value;
                        return;
                    }
                    else if (match.Team2.Value?.Players.Any(u => u.OnlineID == Message.Sender.OnlineID) == true)
                    {
                        UsernameColour = info.BlueSide.Colour.Value;
                        return;
                    }
                }

                UsernameColour = info.RefereeColour.Value;
                UsernameInverted = true;
            }
        }
    }
}

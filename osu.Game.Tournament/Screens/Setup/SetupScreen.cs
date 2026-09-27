// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Drawing;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets;
using osu.Game.Tournament.IPC;
using osu.Game.Tournament.Models;
using osuTK;

namespace osu.Game.Tournament.Screens.Setup
{
    public partial class SetupScreen : TournamentScreen
    {
        private FillFlowContainer fillFlow = null!;

        private LoginOverlay? loginOverlay;
        private ResolutionSelector resolution = null!;

        [Resolved]
        private MatchIPCInfo ipc { get; set; } = null!;

        [Resolved]
        private StableInfo stableInfo { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private TournamentSceneManager? sceneManager { get; set; }

        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();
        private Bindable<Size> windowSize = null!;
        private ApiCredentialsControl credentials = null!;
        private readonly IBindable<APIState> apiState = new Bindable<APIState>();
        [Resolved]
        private TournamentGameBase tournamentGame { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager frameworkConfig)
        {
            windowSize = frameworkConfig.GetBindable<Size>(FrameworkSetting.WindowedSize);

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourProvider.Background5,
                },
                new PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new OsuScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = fillFlow = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding(10),
                            Spacing = new Vector2(10),
                        },
                    },
                },
            };

            localUser.BindTo(api.LocalUser);
            localUser.BindValueChanged(_ => Schedule(reload));
            stableInfo.OnStableInfoSaved += () => Schedule(reload);
            reload();
            apiState.BindTo(api.State);
            apiState.BindValueChanged(_ => Schedule(updateAuthenticationDescription), true);
            tournamentGame.SavingChanges += saveCredentials;
        }

        private void updateAuthenticationDescription()
        {
            credentials.Description = api.State.Value == APIState.Online
                ? "Authenticated using client ID and secret!"
                : "In order to access API please input your valid client ID and secret into the fields";
        }

        private void saveCredentials() => api.SetOAuthCredentials(credentials.ClientId, credentials.ClientSecret);

        protected override void Dispose(bool isDisposing)
        {
            tournamentGame.SavingChanges -= saveCredentials;
            base.Dispose(isDisposing);
        }

        private void reload()
        {
            var fileBasedIpc = ipc as FileBasedIPC;
            fillFlow.Children = new Drawable[]
            {
                new ActionableInfo
                {
                    Label = "Current IPC source",
                    ButtonText = "Change source",
                    Action = () => sceneManager?.SetScreen(new StablePathSelectScreen()),
                    Value = fileBasedIpc?.IPCStorage?.GetFullPath(string.Empty) ?? "Not found",
                    Failing = fileBasedIpc?.IPCStorage == null,
                    Description =
                        "The osu!stable installation which is currently being used as a data source. If a source is not found, make sure you have created an empty ipc.txt in your stable cutting-edge installation."
                },
                credentials = new ApiCredentialsControl(api.Endpoints.APIClientID, api.Endpoints.APIClientSecret)
                {
                    Label = "API Credentials",
                    ButtonText = api.Endpoints.UseAuthorizationCode ? "Login" : "Change sign-in",
                    Action = () =>
                    {
                        api.SetOAuthCredentials(credentials.ClientId, credentials.ClientSecret);
                        if (api.Endpoints.UseAuthorizationCode)
                        {
                            api.RequestOAuthAuthorization();
                            return;
                        }
                        api.Logout();

                        if (loginOverlay == null)
                        {
                            AddInternal(loginOverlay = new LoginOverlay
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                            });
                        }

                        loginOverlay.State.Value = Visibility.Visible;
                    },
                    Value = string.Empty,
                    Failing = api.IsLoggedIn != true,
                    Description = api.State.Value == APIState.Online
                        ? "Authenticated using client ID and secret!"
                        : "In order to access API please input your valid client ID and secret into the fields"
                },
                new LabelledDropdown<RulesetInfo?>(padded: true)
                {
                    Label = "Ruleset",
                    Description = "Decides what stats are displayed and which ranks are retrieved for players. This requires a restart to reload data for an existing bracket.",
                    Items = rulesets.AvailableRulesets,
                    Current = LadderInfo.Ruleset,
                    DropdownWidth = 0.5f,
                },
                new TournamentSwitcher
                {
                    Label = "Current tournament",
                    Description = "Changes the background videos and bracket to match the selected tournament. This requires a restart to apply changes.",
                },
                resolution = new ResolutionSelector
                {
                    Label = "Stream area resolution",
                    ButtonText = "Set height",
                    Action = height =>
                    {
                        windowSize.Value = new Size((int)(height * aspect_ratio / TournamentSceneManager.STREAM_AREA_WIDTH * TournamentSceneManager.REQUIRED_WIDTH), height);
                    }
                },
                new LabelledSwitchButton
                {
                    Label = "Auto advance screens",
                    Description = "Screens will progress automatically from gameplay -> results -> map pool",
                    Current = LadderInfo.AutoProgressScreens,
                },
                new LabelledSwitchButton
                {
                    Label = "Display team seeds",
                    Description = "Team seeds will display alongside each team at the top in gameplay/map pool screens.",
                    Current = LadderInfo.DisplayTeamSeeds,
                },
                new LabelledTextBox
                {
                    Label = "Left side name",
                    Current = LadderInfo.RedSide.Name,
                    SelectAllOnFocus = true,
                },
                new SideColourControl
                {
                    Label = "Left side colour",
                    Current = LadderInfo.RedSide.Colour,
                },
                new LabelledTextBox
                {
                    Label = "Right side name",
                    Current = LadderInfo.BlueSide.Name,
                    SelectAllOnFocus = true,
                },
                new SideColourControl
                {
                    Label = "Right side colour",
                    Current = LadderInfo.BlueSide.Colour,
                },
                new SideColourControl
                {
                    Label = "Referee colour",
                    Current = LadderInfo.RefereeColour,
                },
                new LabelledSwitchButton
                {
                    Label = "Mute UI sounds",
                    Current = LadderInfo.MuteUISounds,
                },
            };

            credentials.Changed += () => api.SetOAuthCredentials(credentials.ClientId, credentials.ClientSecret);
        }

        private partial class SideColourControl : LabelledComponent<SettingsColour.ColourControl, Colour4>
        {
            public SideColourControl()
                : base(true)
            {
            }

            protected override SettingsColour.ColourControl CreateComponent() => new SettingsColour.ColourControl();
        }

        private partial class ApiCredentialsControl : ActionableInfo
        {
            public string ClientId => clientId!.Current.Value;
            public string ClientSecret => clientSecret!.Current.Value;
            public event Action? Changed;

            private FormPasswordTextBox clientId = null!;
            private FormPasswordTextBox clientSecret = null!;

            public ApiCredentialsControl(string id, string secret)
            {
                clientId = new FormPasswordTextBox { Caption = "Client ID", Current = new Bindable<string>(id) };
                clientSecret = new FormPasswordTextBox { Caption = "Client Secret", Current = new Bindable<string>(secret) };
                FlowContainer.Direction = FillDirection.Horizontal;
                FlowContainer.Remove(Button, false);
                FlowContainer.Children = new Drawable[]
                {
                    new Container { Width = 180, AutoSizeAxes = Axes.Y, Child = clientId },
                    new Container { Width = 280, AutoSizeAxes = Axes.Y, Child = clientSecret },
                    Button,
                };
                Button.Anchor = Anchor.CentreLeft;
                Button.Origin = Anchor.CentreLeft;
                clientId.Current.BindValueChanged(_ => updateCredentials());
                clientSecret.Current.BindValueChanged(_ => updateCredentials());
                updateCredentials();
            }

            private void updateCredentials()
            {
                Button.Enabled.Value = !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
                Changed?.Invoke();
            }

        }

        private const float aspect_ratio = 16f / 9f;

        protected override void Update()
        {
            base.Update();

            resolution.Value = $"{ScreenSpaceDrawQuad.Width:N0}x{ScreenSpaceDrawQuad.Height:N0}";
        }
    }
}

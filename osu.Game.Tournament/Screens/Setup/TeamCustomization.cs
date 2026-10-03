// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Bindables;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.Tournament.Screens.Setup
{
    internal partial class SideColourControl : LabelledComponent<SettingsColour.ColourControl, Colour4>
    {
        public float? ControlWidth { get; init; }
        public SideColourControl(bool padded = true)
            : base(padded)
        {
        }
        protected override SettingsColour.ColourControl CreateComponent() => new SettingsColour.ColourControl();
        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (ControlWidth.HasValue)
            {
                Component.RelativeSizeAxes = Axes.None;
                Component.Width = ControlWidth.Value;
            }
        }
    }

    internal partial class TeamCustomization : LabelledDrawable<Drawable>
    {
        protected FillFlowContainer FlowContainer = null!;
        private LabelledTextBox teamName = null!;
        private SideColourControl teamColour = null!;

        private Bindable<Colour4> currentColour = new Bindable<Colour4>();
        public Bindable<Colour4> CurrentColour
        {
            get => currentColour;
            set
            {
                currentColour = value;
                if (teamColour != null)
                    teamColour.Current = value;
            }
        }

        private Bindable<string> currentText = new Bindable<string>();
        public Bindable<string> CurrentText
        {
            get => currentText;
            set
            {
                currentText = value;
                if (teamName != null)
                    teamName.Current = value;
            }
        }

        public TeamCustomization()
            : base(true)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            teamName.Current = CurrentText;
            teamColour.Current = CurrentColour;
        }

        protected override Drawable CreateComponent() => new Container
        {
            AutoSizeAxes = Axes.Y,
            RelativeSizeAxes = Axes.X,
            Children = new Drawable[]
            {
                FlowContainer = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Width = 275,
                            AutoSizeAxes = Axes.Y,
                            Child = teamName = new LabelledTextBox
                            {
                                Label = "Team name",
                                SelectAllOnFocus = true,
                            },
                        },
                        new Container
                        {
                            Width = 220,
                            AutoSizeAxes = Axes.Y,
                            Child = teamColour = new SideColourControl(false)
                            {
                                Label = "Team colour",
                            },
                        }
                    }
                }
            }
        };
    };
}

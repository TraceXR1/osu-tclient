// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Tournament.Models;
using osuTK;

namespace osu.Game.Tournament.Components
{
    public partial class DrawableTeamHeader : TournamentSpriteTextWithBackground, IHasTooltip
    {
        public LocalisableString TooltipText => Text.Text;

        private readonly TeamColour teamColour;
        private readonly Bindable<string> sideName = new Bindable<string>();
        private readonly Bindable<Colour4> sideColour = new Bindable<Colour4>();

        public DrawableTeamHeader(TeamColour colour)
        {
            teamColour = colour;
            Background.Colour = TournamentGame.GetTeamColour(colour);

            Text.Colour = TournamentGame.TEXT_COLOUR;
            Text.Text = $"Team {colour}".ToUpperInvariant();
            Text.Scale = new Vector2(0.6f);
            Text.MaxWidth = 350;
            ((SpriteText)Text).Truncate = true;
        }

        [BackgroundDependencyLoader]
        private void load(LadderInfo ladder)
        {
            var side = ladder.GetSide(teamColour);
            sideName.BindTo(side.Name);
            sideColour.BindTo(side.Colour);
            sideName.BindValueChanged(name => Text.Text = $"Team {name.NewValue}".ToUpperInvariant(), true);
            sideColour.BindValueChanged(colour =>
            {
                Background.Colour = colour.NewValue;
                Text.Colour = OsuColour.ForegroundTextColourFor(colour.NewValue);
            }, true);
        }
    }
}

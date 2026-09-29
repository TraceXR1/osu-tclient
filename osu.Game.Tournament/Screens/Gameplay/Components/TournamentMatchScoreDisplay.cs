// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Screens.Play.HUD;
using osu.Game.Tournament.IPC;
using osu.Game.Tournament.Models;

namespace osu.Game.Tournament.Screens.Gameplay.Components
{
    public partial class TournamentMatchScoreDisplay : MatchScoreDisplay
    {
        [BackgroundDependencyLoader]
        private void load(MatchIPCInfo ipc, LadderInfo ladder)
        {
            Team1Score.BindTo(ipc.Score1);
            Team2Score.BindTo(ipc.Score2);
            Team1Colour.BindTo(ladder.RedSide.Colour);
            Team2Colour.BindTo(ladder.BlueSide.Colour);
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace osu.Game.Tournament.Models
{
    public class TournamentSide
    {
        public readonly Bindable<string> Name;

        [JsonIgnore]
        public readonly Bindable<Colour4> Colour;

        public string ColourHex
        {
            get => Colour.Value.ToHex();
            set => Colour.Value = Colour4.FromHex(value);
        }

        public TournamentSide(string name, Colour4 colour)
        {
            Name = new Bindable<string>(name);
            Colour = new Bindable<Colour4>(colour);
        }
    }
}

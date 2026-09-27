// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Tournament.Models;

namespace osu.Game.Tournament.Tests.NonVisual
{
    [TestFixture]
    public class LadderInfoSerialisationTest
    {
        [Test]
        public void TestDeserialise()
        {
            var ladder = createSampleLadder();
            string serialised = JsonConvert.SerializeObject(ladder);

            JsonConvert.DeserializeObject<LadderInfo>(serialised, new JsonPointConverter());
        }

        [Test]
        public void TestSerialise()
        {
            var ladder = createSampleLadder();
            JsonConvert.SerializeObject(ladder);
        }

        [Test]
        public void TestSideSettingsRoundTrip()
        {
            var ladder = new LadderInfo();
            ladder.RedSide.Name.Value = "Alpha";
            ladder.BlueSide.Name.Value = "Beta";
            ladder.RedSide.Colour.Value = Colour4.FromHex("22CC88");
            ladder.BlueSide.Colour.Value = Colour4.FromHex("FFD100");
            ladder.RefereeColour.Value = Colour4.FromHex("FF77AA");

            string serialised = JsonConvert.SerializeObject(ladder, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                Converters = new JsonConverter[] { new JsonPointConverter() }
            });
            var restored = JsonConvert.DeserializeObject<LadderInfo>(serialised, new JsonPointConverter())!;

            Assert.Multiple(() =>
            {
                Assert.That(restored.RedSide.Name.Value, Is.EqualTo("Alpha"));
                Assert.That(restored.BlueSide.Name.Value, Is.EqualTo("Beta"));
                Assert.That(restored.RedSide.Colour.Value, Is.EqualTo(ladder.RedSide.Colour.Value));
                Assert.That(restored.BlueSide.Colour.Value, Is.EqualTo(ladder.BlueSide.Colour.Value));
                Assert.That(restored.RefereeColour.Value, Is.EqualTo(ladder.RefereeColour.Value));
            });
        }

        [Test]
        public void TestOldBracketSideDefaults()
        {
            var ladder = JsonConvert.DeserializeObject<LadderInfo>("{}")!;

            Assert.Multiple(() =>
            {
                Assert.That(ladder.RedSide.Name.Value, Is.EqualTo("Red"));
                Assert.That(ladder.BlueSide.Name.Value, Is.EqualTo("Blue"));
                Assert.That(ladder.RedSide.Colour.Value, Is.EqualTo((Colour4)TournamentGame.COLOUR_RED));
                Assert.That(ladder.BlueSide.Colour.Value, Is.EqualTo((Colour4)TournamentGame.COLOUR_BLUE));
                Assert.That(ladder.RefereeColour.Value, Is.EqualTo(Colour4.FromHex("FFD966")));
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestMuteUISoundsRoundTrip(bool muted)
        {
            var ladder = new LadderInfo { MuteUISounds = { Value = muted } };
            string serialised = JsonConvert.SerializeObject(ladder, new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore
            });

            var restored = JsonConvert.DeserializeObject<LadderInfo>(serialised)!;
            Assert.That(restored.MuteUISounds.Value, Is.EqualTo(muted));
        }

        [Test]
        public void TestOldBracketMutesUISoundsByDefault()
        {
            var ladder = JsonConvert.DeserializeObject<LadderInfo>("{}")!;
            Assert.That(ladder.MuteUISounds.Value, Is.True);
        }

        private static LadderInfo createSampleLadder()
        {
            var match = TournamentTestScene.CreateSampleMatch();

            return new LadderInfo
            {
                PlayersPerTeam = { Value = 4 },
                Teams =
                {
                    match.Team1.Value!,
                    match.Team2.Value!,
                },
                Rounds =
                {
                    new TournamentRound
                    {
                        Beatmaps =
                        {
                            new RoundBeatmap { Beatmap = TournamentTestScene.CreateSampleBeatmap() },
                            new RoundBeatmap { Beatmap = TournamentTestScene.CreateSampleBeatmap() },
                        }
                    }
                },

                Matches =
                {
                    match,
                },
                Progressions =
                {
                    new TournamentProgression(1, 2),
                    new TournamentProgression(1, 3, true),
                }
            };
        }
    }
}

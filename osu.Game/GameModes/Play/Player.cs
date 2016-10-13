//Copyright (c) 2007-2016 ppy Pty Ltd <contact@ppy.sh>.
//Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osu.Framework.Graphics;
using osu.Framework.MathUtils;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Objects;
using osu.Game.Beatmaps.Objects.Osu;
using osu.Game.GameModes.Backgrounds;
using osu.Game.GameModes.Play.Catch;
using osu.Game.GameModes.Play.Mania;
using osu.Game.GameModes.Play.Osu;
using osu.Game.GameModes.Play.Taiko;
using osu.Game.Graphics.UserInterface;
using OpenTK;
using OpenTK.Input;
using osu.Framework;
using osu.Framework.Audio.Track;
using osu.Game.Beatmaps.Formats;
using osu.Game.Beatmaps.IO;

namespace osu.Game.GameModes.Play
{
    class Player : OsuGameMode
    {
        protected override BackgroundMode CreateBackground() => new BackgroundModeCustom(@"Backgrounds/bg4");

        protected override IFrameBasedClock Clock => clock;

        private FramedClock clock = new FramedClock();

        public override void Load(BaseGame game)
        {
            base.Load(game);

            //i start with a beatmap
            Beatmap beatmap = ((OsuGame)game).Beatmaps.FirstBeatmap;

            //beatmap is missing storage access method (to get osz directly) and ability to access file streams

            //...so i get it myself
            var reader = ArchiveReader.GetReader(game.Host.Storage, @"150945 Knife Party - Centipede.osz");

            //i have to read the filenames manually because they aren't in the databse either
            string[] maps = reader.ReadBeatmaps();


            AudioTrackBass track = new AudioTrackBass(reader.ReadFile(@"150945 Knife Party - Centipede/02-knife_party-centipede.mp3"));
            game.Audio.Track.ActiveItems.ForEach(t => t.Stop());
            game.Audio.Track.ActiveItems.Clear();
            game.Audio.Track.AddItem(track);

            track.Start();

            using (Stream s = reader.ReadFile(maps[0]))
            using (StreamReader sr = new StreamReader(s))
            {
                var decoder = BeatmapDecoder.GetDecoder(sr);

                //i get a new beatmap even though i already had one earlier... decoding should populate hitobjects etc and set a flag on the beatmap object saying it's fully loaded (and allow unloading too)
                //rather than return a new beatmap

                //because the decoder consumed some of the streamreader, i need to reset the stream (hopefully the underlying stream allows this) and then start reading again, ew
                //s.Seek(0, SeekOrigin.Begin);

                //ok seeking doesn't work.

                using (Stream s2 = reader.ReadFile(maps[0]))
                using (StreamReader sr2 = new StreamReader(s2))
                    beatmap = decoder.Decode(sr2);
            }

            OsuGame osu = game as OsuGame;

            switch (osu.PlayMode.Value)
            {
                case PlayMode.Osu:
                    Add(new OsuHitRenderer
                    {
                        Objects = beatmap.HitObjects,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre
                    });
                    break;
                case PlayMode.Taiko:
                    Add(new TaikoHitRenderer
                    {
                        Objects = beatmap.HitObjects,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre
                    });
                    break;
                case PlayMode.Catch:
                    Add(new CatchHitRenderer
                    {
                        Objects = beatmap.HitObjects,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre
                    });
                    break;
                case PlayMode.Mania:
                    Add(new ManiaHitRenderer
                    {
                        Objects = beatmap.HitObjects,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre
                    });
                    break;
            }

            Add(new KeyCounterCollection
            {
                IsCounting = true,
                FadeTime = 50,
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Position = new Vector2(10, 50),
                Counters = new KeyCounter[]
                {
                    new KeyCounterKeyboard(@"Z", Key.Z),
                    new KeyCounterKeyboard(@"X", Key.X),
                    new KeyCounterMouse(@"M1", MouseButton.Left),
                    new KeyCounterMouse(@"M2", MouseButton.Right),
                }
            });
        }

        protected override void Update()
        {
            base.Update();
            clock.ProcessFrame();
        }
    }
}
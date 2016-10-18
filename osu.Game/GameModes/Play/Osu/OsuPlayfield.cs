//Copyright (c) 2007-2016 ppy Pty Ltd <contact@ppy.sh>.
//Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using OpenTK;
using osu.Framework;
using osu.Framework.Graphics.Sprites;
using OpenTK.Graphics;

namespace osu.Game.GameModes.Play.Osu
{
    public class OsuPlayfield : Playfield
    {
        public OsuPlayfield()
        {
            RelativeSizeAxes = Axes.None;
            Size = new Vector2(512, 384);
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        public override void Load(BaseGame game)
        {
            base.Load(game);

            Add(new Box()
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Black,
                Depth = float.MinValue,
                Alpha = 0.5f
            });
        }
    }
}
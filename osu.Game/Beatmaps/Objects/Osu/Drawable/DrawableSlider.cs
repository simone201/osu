//Copyright (c) 2007-2016 ppy Pty Ltd <contact@ppy.sh>.
//Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Transformations;
using OpenTK;
using OpenTK.Graphics;

namespace osu.Game.Beatmaps.Objects.Osu.Drawable
{
    class DrawableSlider : Container
    {
        private Slider s;

        public DrawableSlider(Slider s)
        {
            this.s = s;

            Alpha = 0;
            Colour = Color4.Green;
            RelativeSizeAxes = Axes.Both;
        }

        public override void Load(BaseGame game)
        {
            base.Load(game);

            Add(new Sprite
            {
                Origin = Anchor.Centre,
                Texture = game.Textures.Get(@"Menu/logo"),
                Scale = new Vector2(0.1f),
                Position = s.Position
            });

            foreach (var pos in s.Path)
            {
                Add(new Sprite
                {
                    Origin = Anchor.Centre,
                    Texture = game.Textures.Get(@"Menu/logo"),
                    Scale = new Vector2(0.1f),
                    Position = pos
                });
            }

            Transforms.Add(new TransformAlpha(Clock) { StartTime = s.StartTime - 200, EndTime = s.StartTime, StartValue = 0, EndValue = 1 });
            Transforms.Add(new TransformAlpha(Clock) { StartTime = s.StartTime + s.Duration + 200, EndTime = s.StartTime + s.Duration + 400, StartValue = 1, EndValue = 0 });
            Expire(true);
        }
    }
}

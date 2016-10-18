﻿//Copyright (c) 2007-2016 ppy Pty Ltd <contact@ppy.sh>.
//Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using osu.Framework.Graphics;
using osu.Game.Beatmaps.Objects;
using osu.Game.Beatmaps.Objects.Osu;
using osu.Game.Beatmaps.Objects.Osu.Drawable;

namespace osu.Game.GameModes.Play.Osu
{
    public class OsuHitRenderer : HitRenderer<OsuBaseHit>
    {
        protected override HitObjectConverter<OsuBaseHit> Converter => new OsuConverter();

        protected override Playfield CreatePlayfield() => new OsuPlayfield();

        protected override Drawable GetVisualRepresentation(OsuBaseHit h)
        {
            Circle c = h as Circle;
            if (c != null) return new DrawableCircle(c);
            Slider s = h as Slider;
            if (s != null) return new DrawableSlider(s);

            return null;
        }
    }
}

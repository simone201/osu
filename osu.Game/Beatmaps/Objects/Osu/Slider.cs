﻿//Copyright (c) 2007-2016 ppy Pty Ltd <contact@ppy.sh>.
//Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System.Collections.Generic;
using OpenTK;

namespace osu.Game.Beatmaps.Objects.Osu
{
    public class Slider : OsuBaseHit
    {
        public List<Vector2> Path = new List<Vector2>();

        public int RepeatCount;
    }
}

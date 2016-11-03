﻿using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;

namespace osu.Game.Overlays.Options
{
    public class VolumeOptions : OptionsSubsection
    {
        public VolumeOptions()
        {
            Header = "Volume";
            Children = new Drawable[]
            {
                new SpriteText { Text = "Master: TODO slider" },
                new SpriteText { Text = "Music: TODO slider" },
                new SpriteText { Text = "Effect: TODO slider" },
                new BasicCheckBox { LabelText = "Ignore beatmap hitsounds" }
            };
        }
    }
}
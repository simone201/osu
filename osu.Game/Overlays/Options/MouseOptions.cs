﻿using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;

namespace osu.Game.Overlays.Options
{
    public class MouseOptions : OptionsSubsection
    {
        public MouseOptions()
        {
            Header = "Mouse";
            Children = new Drawable[]
            {
                new SpriteText { Text = "Sensitivity: TODO slider" },
                new BasicCheckBox { LabelText = "Raw input" },
                new BasicCheckBox { LabelText = "Map absolute raw input to the osu! window" },
                new SpriteText { Text = "Confine mouse cursor: TODO dropdown" },
                new BasicCheckBox { LabelText = "Disable mouse wheel in play mode" },
                new BasicCheckBox { LabelText = "Disable mouse buttons in play mode" },
                new BasicCheckBox { LabelText = "Cursor ripples" },
            };
        }
    }
}
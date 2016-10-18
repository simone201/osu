//Copyright (c) 2007-2016 ppy Pty Ltd <contact@ppy.sh>.
//Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Game.Graphics;
using OpenTK;
using OpenTK.Graphics;
using osu.Framework;
using osu.Framework.Graphics.Primitives;

namespace osu.Game.Overlays
{
    public class ToolbarButton : AutoSizeContainer
    {
        public FontAwesome Icon
        {
            get { return DrawableIcon.Icon; }
            set { DrawableIcon.Icon = value; }
        }

        public string Text
        {
            get { return DrawableText.Text; }
            set
            {
                DrawableText.Text = value;
            }
        }

        public string TooltipMain
        {
            get { return tooltip1.Text; }
            set
            {
                tooltip1.Text = value;
            }
        }

        public string TooltipSub
        {
            get { return tooltip2.Text; }
            set
            {
                tooltip2.Text = value;
            }
        }

        public Action Action;
        protected TextAwesome DrawableIcon;
        protected SpriteText DrawableText;
        protected Box HoverBackground;
        private FlowContainer tooltipContainer;
        private SpriteText tooltip1;
        private SpriteText tooltip2;

        public ToolbarButton()
        {
            RelativeSizeAxes = Axes.Y;

            DrawableText = new SpriteText
            {
                //Margin = new MarginPadding { Left = 5 },
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            };

            Children = new Drawable[]
            {
                HoverBackground = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Additive = true,
                    Colour = new Color4(60, 60, 60, 255),
                    Alpha = 0,
                },
                new FlowContainer
                {
                    Direction = FlowDirection.HorizontalOnly,
                    Padding = new MarginPadding { Left = 10, Right = 10 },
                    RelativeSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        DrawableIcon = new TextAwesome
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        },

                    },
                },
                tooltipContainer = new FlowContainer
                {
                    Direction = FlowDirection.VerticalOnly,
                    Anchor = Anchor.BottomLeft,
                    Position = new Vector2(5, -5),
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    Children = new[]
                    {
                        tooltip1 = new SpriteText()
                        {
                            TextSize = 22,
                        },
                        tooltip2 = new SpriteText
                        {
                            TextSize = 15
                        }
                    }
                }
            };
        }

        protected override bool OnClick(InputState state)
        {
            Action?.Invoke();
            HoverBackground.FlashColour(Color4.White, 400);
            return true;
        }

        protected override bool OnHover(InputState state)
        {
            HoverBackground.FadeTo(0.4f, 200);
            tooltipContainer.FadeIn(100);
            return true;
        }

        protected override void OnHoverLost(InputState state)
        {
            HoverBackground.FadeTo(0, 200);
            tooltipContainer.FadeOut(100);
        }
    }
}
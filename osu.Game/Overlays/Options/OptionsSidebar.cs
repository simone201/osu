﻿using System;
using OpenTK;
using OpenTK.Graphics;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Game.Graphics;

namespace osu.Game.Overlays.Options
{
    public class OptionsSidebar : Container
    {
        private FlowContainer content;
        protected override Container Content => content;

        public OptionsSidebar()
        {
            RelativeSizeAxes = Axes.Y;
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    Colour = Color4.Black,
                    RelativeSizeAxes = Axes.Both,
                },
                new SidebarScrollContainer
                {
                    Children = new []
                    {
                        content = new FlowContainer
                        {
                            Origin = Anchor.CentreLeft,
                            Anchor = Anchor.CentreLeft,
                            AutoSizeAxes = Axes.Y,
                            RelativeSizeAxes = Axes.X,
                            Direction = FlowDirection.VerticalOnly
                        }
                    }
                },
            };
        }

        private class SidebarScrollContainer : ScrollContainer
        {
            public SidebarScrollContainer()
            {
                Content.Anchor = Anchor.CentreLeft;
                Content.Origin = Anchor.CentreLeft;
            }
        }

        public class SidebarButton : Container
        {
            private TextAwesome drawableIcon;
            private Box backgroundBox;
            public Action Action;

            public FontAwesome Icon
            {
                get { return drawableIcon.Icon; }
                set { drawableIcon.Icon = value; }
            }

            public SidebarButton()
            {
                Size = new Vector2(60);
                Children = new Drawable[]
                {
                    backgroundBox = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        BlendingMode = BlendingMode.Additive,
                        Colour = new Color4(60, 60, 60, 255),
                        Alpha = 0,
                    },
                    drawableIcon = new TextAwesome
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                };
            }

            protected override bool OnClick(InputState state)
            {
                Action?.Invoke();
                backgroundBox.FlashColour(Color4.White, 400);
                return true;
            }

            protected override bool OnHover(InputState state)
            {
                backgroundBox.FadeTo(0.4f, 200);
                return true;
            }

            protected override void OnHoverLost(InputState state)
            {
                backgroundBox.FadeTo(0, 200);
            }
        }
    }
}
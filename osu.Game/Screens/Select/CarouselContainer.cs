//Copyright (c) 2007-2016 ppy Pty Ltd <contact@ppy.sh>.
//Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using OpenTK;
using osu.Framework.Caching;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Transformations;
using osu.Game.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Lists;
using osu.Game.Beatmaps.Drawables;
using osu.Framework.Timing;

namespace osu.Game.Screens.Select
{
    class CarouselContainer : ScrollContainer
    {
        private CarouselContent content;
        private List<BeatmapGroup> groups = new List<BeatmapGroup>();

        public BeatmapGroup SelectedGroup { get; private set; }
        public BeatmapPanel SelectedPanel { get; private set; }

        private List<float> yPositions = new List<float>();

        public CarouselContainer()
        {
            DistanceDecayJump = 0.01;

            Add(content = new CarouselContent()
            {
                RelativeSizeAxes = Axes.X,
            });
        }

        class CarouselContent : Container<Panel>
        {
            public CarouselContent() : base(new CarouselLifetimeList<Panel>())
            { }

            public float MinimumY { set { ((CarouselLifetimeList<Panel>)children).MinimumY = value; } }
            public float MaximumY { set { ((CarouselLifetimeList<Panel>)children).MaximumY = value; } }

            private class CarouselLifetimeList<T> : LifetimeList<Panel>
            {
                class PositionComparer : DepthComparer
                {
                    public override int Compare(Drawable x, Drawable y)
                    {
                        int i = x.Position.Y.CompareTo(y.Position.Y);
                        if (i != 0)
                            return i;
                        return base.Compare(x, y);
                    }
                }

                public CarouselLifetimeList()
                    : base(new PositionComparer())
                {
                }

                public float MinimumY;
                public float MaximumY;

                public override bool Update(FrameTimeInfo time)
                {
                    bool anyAliveChanged = false;

                    //check existing items to make sure they haven't died.
                    foreach (var item in AliveItems.ToArray())
                    {
                        item.UpdateTime(time);
                        if (item.Position.Y < MinimumY || item.Position.Y > MaximumY)
                        {
                            //todo: make this more efficient
                            int i = IndexOf(item);
                            item.OnScreen = false;
                            anyAliveChanged |= CheckItem(item, ref i);
                        }
                    }

                    int firstIndex = BinarySearch(new Panel { Position = new Vector2(0, MinimumY) }, Comparer);
                    if (firstIndex < 0) firstIndex = ~firstIndex;
                    int lastIndex = BinarySearch(new Panel { Position = new Vector2(0, MaximumY) }, Comparer);
                    if (lastIndex < 0) lastIndex = ~lastIndex;

                    //handle custom range
                    for (int i = firstIndex; i < lastIndex; i++)
                    {
                        var item = this[i];
                        item.OnScreen = true;
                        item.UpdateTime(time);
                        anyAliveChanged |= CheckItem(item, ref i);
                    }

                    return anyAliveChanged;
                }
            }
        }

        public void AddGroup(BeatmapGroup group)
        {
            group.State = BeatmapGroupState.Collapsed;
            groups.Add(group);

            group.Header.Depth = content.Children.Count();
            content.Add(group.Header);

            foreach (BeatmapPanel panel in group.BeatmapPanels)
            {
                panel.Depth = content.Children.Count();
                content.Add(panel);
            }

            computeYPositions();
        }

        private void movePanel(Panel panel, bool advance, ref float currentY)
        {
            yPositions.Add(currentY);
            panel.MoveToY(currentY, 750, EasingTypes.OutExpo);

            if (advance)
                currentY += panel.DrawHeight + 5;
        }

        /// <summary>
        /// Computes the target Y positions for every panel in the carousel.
        /// </summary>
        /// <returns>The Y position of the currently selected panel.</returns>
        private float computeYPositions()
        {
            yPositions.Clear();

            float currentY = DrawHeight / 2;
            float selectedY = currentY;

            foreach (BeatmapGroup group in groups)
            {
                movePanel(group.Header, true, ref currentY);

                if (group.State == BeatmapGroupState.Expanded)
                {
                    group.Header.MoveToX(-100, 500, EasingTypes.OutExpo);
                    var headerY = group.Header.Position.Y;

                    foreach (BeatmapPanel panel in group.BeatmapPanels)
                    {
                        if (panel == SelectedPanel)
                            selectedY = currentY + panel.DrawHeight / 2 - DrawHeight / 2;

                        panel.MoveToX(-50, 500, EasingTypes.OutExpo);

                        //on first display we want to begin hidden under our group's header.
                        if (panel.Alpha == 0)
                            panel.MoveToY(headerY);

                        movePanel(panel, true, ref currentY);
                    }
                }
                else
                {
                    group.Header.MoveToX(0, 500, EasingTypes.OutExpo);

                    foreach (BeatmapPanel panel in group.BeatmapPanels)
                    {
                        panel.MoveToX(0, 500, EasingTypes.OutExpo);
                        movePanel(panel, false, ref currentY);
                    }
                }
            }

            currentY += DrawHeight / 2;
            content.Height = currentY;

            return selectedY;
        }

        public void SelectBeatmap(BeatmapInfo beatmap)
        {
            foreach (BeatmapGroup group in groups)
            {
                var panel = group.BeatmapPanels.FirstOrDefault(p => p.Beatmap.Equals(beatmap));
                if (panel != null)
                {
                    SelectGroup(group, panel);
                    return;
                }
            }
        }

        public void SelectGroup(BeatmapGroup group, BeatmapPanel panel)
        {
            if (SelectedGroup != null && SelectedGroup != group)
                SelectedGroup.State = BeatmapGroupState.Collapsed;

            SelectedGroup = group;
            panel.State = PanelSelectedState.Selected;
            SelectedPanel = panel;

            float selectedY = computeYPositions();
            ScrollTo(selectedY);
        }

        private static float offsetX(float dist, float halfHeight)
        {
            // The radius of the circle the carousel moves on.
            const float CIRCLE_RADIUS = 4;
            double discriminant = Math.Max(0, CIRCLE_RADIUS * CIRCLE_RADIUS - dist * dist);
            float x = (CIRCLE_RADIUS - (float)Math.Sqrt(discriminant)) * halfHeight;

            return 125 + x;
        }

        protected override void Update()
        {
            base.Update();

            float drawHeight = DrawHeight;

            content.MinimumY = Current - Panel.MAX_HEIGHT;
            content.MaximumY = Current + drawHeight;

            float halfHeight = drawHeight / 2;

            foreach (var panel in content.AliveChildren)
            {
                float panelDrawY = panel.Position.Y - Current + panel.DrawHeight / 2;
                float dist = Math.Abs(1f - panelDrawY / halfHeight);

                // Setting the origin position serves as an additive position on top of potential
                // local transformation we may want to apply (e.g. when a panel gets selected, we
                // may want to smoothly transform it leftwards.)
                panel.OriginPosition = new Vector2(-offsetX(dist, halfHeight), 0);

                // We are applying a multiplicative alpha (which is internally done by nesting an
                // additional container and setting that container's alpha) such that we can
                // layer transformations on top, with a similar reasoning to the previous comment.
                panel.SetMultiplicativeAlpha(MathHelper.Clamp(1.75f - 1.5f * dist, 0, 1));
            }
        }
    }
}

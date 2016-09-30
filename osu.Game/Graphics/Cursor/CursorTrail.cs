using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.OpenGL;
using osu.Framework.Graphics.OpenGL.Buffers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.ES20;

namespace osu.Game.Graphics.Cursor
{
    internal class CursorTrail : LargeContainer
    {
        internal class CursorTrailDrawNode : DrawNode
        {
            private static Shader shader;
            public Framework.Game Game;
            public Texture Texture;
            public Quad ScreenSpaceDrawQuad;
            public int VisibleRange;
            public int NewRange;
            public int[] NewRanges;
            public int[] VisibleRanges;
            private bool[] needsUpload;
            private VertexBuffer<TimedTexturedVertex2d> vertexBuffer;

            private Uniform<float> fadeClock;
            public float FadeClock;

            protected override void Draw()
            {
                base.Draw();

                if (shader == null)
                {
                    shader = Game.Shaders.Load(@"CursorTrail", @"Texture");
                    fadeClock = shader.GetUniform<float>(@"g_FadeClock");
                }

                fadeClock.Value = FadeClock;

                if (vertexBuffer == null)
                    vertexBuffer = new QuadVertexBuffer<TimedTexturedVertex2d>(MAX_SPRITES, BufferUsageHint.DynamicDraw);

                Texture trail = Texture;

                if (VisibleRange == 0 || trail == null || trail.IsDisposed)
                    return;

                GLWrapper.BindTexture(trail.TextureGL.TextureId);
                shader.Bind();
                fadeClock.Value = fadeClock;

                GLWrapper.SetBlend(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.One);

                for (int i = 0; i < NewRange; i += 2)
                {
                    for (int j = NewRanges[i]; j < NewRanges[i + 1]; ++j)
                        needsUpload[j] = false;

                    vertexBuffer.UpdateRange(NewRanges[i] * 4, NewRanges[i + 1] * 4);
                }

                for (int i = 0; i < VisibleRange; i += 2)
                    vertexBuffer.DrawRange(VisibleRanges[i] * 4, VisibleRanges[i + 1] * 4);

                shader.Unbind();
            }
        }

        private double timeOffset = 0;
        private float[] fadeTimes;

        private int[] newIndexRanges = new int[4];
        private int[] visibleIndexRanges = new int[4];

        private int amountNewRanges = 0;
        private int amountVisibleRanges = 0;

        const int MAX_SPRITES = 2048;

        private float fade;

        protected override DrawNode CreateDrawNode() => new CursorTrailDrawNode();

        protected override void ApplyDrawNode(DrawNode node)
        {
            CursorTrailDrawNode n = node as CursorTrailDrawNode;
            n.NewRange = amountNewRanges;
            n.VisibleRange = amountVisibleRanges;
            n.NewRanges = newIndexRanges;
            n.VisibleRanges = visibleIndexRanges;
            n.FadeClock = fade;
            base.ApplyDrawNode(node);

        }

        public override void Load()
        {
            base.Load();

            //cursorTrailShader = Game.Shaders.Load(@"CursorTrail", @"Texture");
            //fadeClock = cursorTrailShader.GetUniform<float>(@"g_FadeClock");

            fadeTimes = new float[MAX_SPRITES];
            needsUpload = new bool[MAX_SPRITES];

        }

        int currentIndex = 0;

        Queue<Vector2> addQueue = new Queue<Vector2>();

        internal void Add(Vector2 pos)
        {
            addQueue.Enqueue(pos);
        }

        private void add(Vector2 pos)
        {
            pos *= DrawInfo.Matrix;

            Texture trailTexture = Game.Textures.Get(@"");
            if (trailTexture == null)
                return;

            float drawSize = trailTexture.DisplayWidth;// * OsuGame.Window.RatioInverse * Scale;

            RectangleF texCoordsRect = new RectangleF(0, 0,
                    (float)trailTexture.Width,
                    (float)trailTexture.Height);

            int vertexIndex = currentIndex * 4;
            float fadeTime = fadeClock + 1f;

            vertexBuffer.Vertices[vertexIndex].Position = pos + new Vector2(-drawSize / 2, -drawSize / 2);
            vertexBuffer.Vertices[vertexIndex].TexturePosition = new Vector2(0, 0);
            vertexBuffer.Vertices[vertexIndex].Colour = Color4.White;
            vertexBuffer.Vertices[vertexIndex].Time = fadeTime;

            vertexBuffer.Vertices[vertexIndex + 1].Position = pos + new Vector2(-drawSize / 2, drawSize / 2);
            vertexBuffer.Vertices[vertexIndex + 1].TexturePosition = new Vector2(0, texCoordsRect.Height);
            vertexBuffer.Vertices[vertexIndex + 1].Colour = Color4.White;
            vertexBuffer.Vertices[vertexIndex + 1].Time = fadeTime;

            vertexBuffer.Vertices[vertexIndex + 2].Position = pos + new Vector2(drawSize / 2, drawSize / 2);
            vertexBuffer.Vertices[vertexIndex + 2].TexturePosition = new Vector2(texCoordsRect.Width, texCoordsRect.Height);
            vertexBuffer.Vertices[vertexIndex + 2].Colour = Color4.White;
            vertexBuffer.Vertices[vertexIndex + 2].Time = fadeTime;

            vertexBuffer.Vertices[vertexIndex + 3].Position = pos + new Vector2(drawSize / 2, -drawSize / 2);
            vertexBuffer.Vertices[vertexIndex + 3].TexturePosition = new Vector2(texCoordsRect.Width, 0);
            vertexBuffer.Vertices[vertexIndex + 3].Colour = Color4.White;
            vertexBuffer.Vertices[vertexIndex + 3].Time = fadeTime;

            fadeTimes[currentIndex] = fadeTime;
            needsUpload[currentIndex] = true;

            currentIndex = (currentIndex + 1) % MAX_SPRITES;
        }


        private int findRanges(int[] ranges, float alphaThreshold)
        {
            // Figure out in which ranges of indices the visible vertices lie. There are at most 2 such ranges due to the way we dim, e.g. [111100001111] produces (0,4) and (8,12).
            int amountRanges = 0;
            for (int i = 0; i < MAX_SPRITES; i++)
            {
                float alpha = fadeTimes[i] - fadeClock;
                if (alpha >= alphaThreshold || needsUpload[i])
                {
                    if (amountRanges % 2 == 0)
                    {
                        ranges[amountRanges] = i;
                        ++amountRanges;
                    }
                }
                else
                {
                    if (amountRanges % 2 == 1)
                    {
                        ranges[amountRanges] = i;
                        ++amountRanges;
                    }
                }
            }

            // We might have had visible vertices until the end in which case we still need to close the current range.
            if (amountRanges % 2 == 1)
            {
                ranges[amountRanges] = MAX_SPRITES;
                ++amountRanges;
            }

            return amountRanges;
        }

        protected override void Update()
        {
            base.Update();

            while (addQueue.Count > 0)
                add(addQueue.Dequeue());

            fade = (float)((Time - timeOffset) / 500.0);

            //int fadeClockResetThreshold = (OsuGame.IdleTime > 60000 || !OsuGame.Instance.IsActive) ? 10000 : 1000000;
            //if (fadeClock > fadeClockResetThreshold)
            //    ResetTime();

            amountNewRanges = findRanges(newIndexRanges, 1f);
            amountVisibleRanges = findRanges(visibleIndexRanges, 0);


        }

        private void ResetTime()
        {
            double currentTime = Time;
            timeOffset = currentTime;

            for (int i = 0; i < MAX_SPRITES; ++i)
            {
                fadeTimes[i] -= fadeClock;
                needsUpload[i] = true;

                int vertexIndex = i * 4;
                vertexBuffer.Vertices[vertexIndex + 0].Time -= fadeClock;
                vertexBuffer.Vertices[vertexIndex + 1].Time -= fadeClock;
                vertexBuffer.Vertices[vertexIndex + 2].Time -= fadeClock;
                vertexBuffer.Vertices[vertexIndex + 3].Time -= fadeClock;
            }

            fadeClock.Value = 0;
        }
    }
}

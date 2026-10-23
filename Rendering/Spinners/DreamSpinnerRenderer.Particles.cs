using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Randomness;
using Celeste.Core.Rendering;

namespace Celeste.Mods.IsaGrabBag.Rendering.Spinners
{
    public sealed partial class DreamSpinnerRenderer
    {
        internal struct DreamParticle
        {
            public Vector2 Position;
            public int Layer;
            public float TimeOffset;
            public PackedColor Color;
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            particles = CreateParticles(((ISourceRandomProcessEnvironment)engine).RandomProcess);
        }

        internal static DreamParticle[] CreateParticles(SourceRandomProcessState process)
        {
            process.PushRandom(4851);
            try
            {
                var particles = new DreamParticle[630];
                var colors = new[]
                {
                    new[] { new PackedColor(255, 239, 17), new PackedColor(255, 0, 208), new PackedColor(8, 163, 16) },
                    new[]
                    {
                        new PackedColor(95, 205, 228), new PackedColor(127, 178, 94), new PackedColor(224, 86, 76)
                    },
                    new[]
                    {
                        new PackedColor(91, 110, 225), new PackedColor(204, 59, 59), new PackedColor(125, 170, 100)
                    }
                };
                var layers = new[] { 0, 1, 1, 2, 2, 2 };
                var random = process.Random;
                for (int i = 0; i < particles.Length; i++)
                {
                    particles[i].Position =
                        new Vector2((float)random.NextDouble() * 320f, (float)random.NextDouble() * 180f);
                    particles[i].Layer = layers[random.Next(6)];
                    particles[i].TimeOffset = (float)random.NextDouble();
                    particles[i].Color = colors[particles[i].Layer][random.Next(3)];
                }

                return particles;
            }
            finally
            {
                process.PopRandom();
            }
        }

        private void DrawParticles(ISourceRenderContext context, Vector2 camera)
        {
            foreach (var particle in particles)
            {
                int layer = particle.Layer;
                var color = dreamDash ? particle.Color : PackedColor.LightGray * (.5f + layer / 2f * .5f);
                int texture = layer == 0 ? 3 - (int)((particle.TimeOffset * 4f + animation) % 4f)
                    : layer == 1 ? 1 + (int)((particle.TimeOffset * 2f + animation) % 2f) : 2;
                var position = particle.Position - camera * (.7f - .25f * layer);
                position.X = (position.X % 320f + 320f) % 320f;
                position.Y = (position.Y % 180f + 180f) % 180f;
                DrawTexture(context, particlesTextures[texture], position, new Vector2(3.5f), color);
            }
        }
    }
}

using System;
using Akka.Actor;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AkkaFractal.Core.Akka
{
    public class TileRenderActor : ReceiveActor
    {
        public TileRenderActor()
        {

            Receive<Completed>(c => Sender.Tell(c));


            Receive<RenderTile>(render =>
            {
                Console.WriteLine("{0} rendering {1},{2}", Self, render.X, render.Y);

                var res = MandelbrotSet(
                    render.X, render.Y, render.Width, render.Height,
                    render.ImageWidth, render.ImageHeight, 0.5, -2.5, 1.5, -1.5);

                // TODO LAB
                // Complete this e "R Receive<RenderTile>" handler so that it responds back (reply)
                // to the sender a "RenderedTile" message with the correct parameters
            });
        }

        private static Image<Rgba32> MandelbrotSet(
            int xp, int yp, int w, int h, int width, int height,
            double maxr, double minr, double maxi, double mini)
        {
            var img = new Image<Rgba32>(w, h);
            double zx = 0;
            double zy = 0;
            double cx = 0;
            double cy = 0;
            var xjump = (maxr - minr) / Convert.ToDouble(width);
            var yjump = (maxi - mini) / Convert.ToDouble(height);
            double tempzx = 0;
            var loopmax = 1000;
            var loopgo = 0;
            for (var x = xp; x < xp + w; x++)
            {
                cx = xjump * x - Math.Abs(minr);
                for (var y = yp; y < yp + h; y++)
                {
                    zx = 0;
                    zy = 0;
                    cy = yjump * y - Math.Abs(mini);
                    loopgo = 0;
                    while (zx * zx + zy * zy <= 4 && loopgo < loopmax)
                    {
                        loopgo++;
                        tempzx = zx;
                        zx = zx * zx - zy * zy + cx;
                        zy = 2 * tempzx * zy + cy;
                    }

                    if (loopgo != loopmax)
                        img[x - xp, y - yp] = new Rgba32((byte) (loopgo % 32 * 7), (byte) (loopgo % 128 * 2),
                            (byte) (loopgo % 16 * 14));
                    else
                        img[x - xp, y - yp] = Rgba32.ParseHex("#000000");
                }
            }

            return img;
        }
    }
}

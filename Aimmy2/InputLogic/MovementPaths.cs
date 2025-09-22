using System.Drawing;

namespace InputLogic
{
    class MovementPaths
    {
        private static readonly int[] permutation = new int[512];

        internal static Point CubicBezier(Point start, Point end, Point control1, Point control2, double t)
        {
            double u = 1 - t;
            double tt = t * t;
            double uu = u * u;

            double x = uu * u * start.X + 3 * uu * t * control1.X + 3 * u * tt * control2.X + tt * t * end.X;
            double y = uu * u * start.Y + 3 * uu * t * control1.Y + 3 * u * tt * control2.Y + tt * t * end.Y;

            return new Point((int)x, (int)y);
        }

        internal static Point Lerp(Point start, Point end, double t)
        {
            int x = (int)(start.X + (end.X - start.X) * t);
            int y = (int)(start.Y + (end.Y - start.Y) * t);
            return new Point(x, y);
        }

        internal static Point Exponential(Point start, Point end, double t, double exponent = 2.0)
        {
            double x = start.X + (end.X - start.X) * Math.Pow(t, exponent);
            double y = start.Y + (end.Y - start.Y) * Math.Pow(t, exponent);
            return new Point((int)x, (int)y);
        }

        internal static Point Adaptive(Point start, Point end, double t, double threshold = 100.0)
        {
            double distance = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
            if (distance < threshold)
            {
                return Lerp(start, end, t);
            }
            else
            {
                Point control1 = new Point(start.X + (end.X - start.X) / 3, start.Y + (end.Y - start.Y) / 3);
                Point control2 = new Point(start.X + 2 * (end.X - start.X) / 3, start.Y + 2 * (end.Y - start.Y) / 3);
                return CubicBezier(start, end, control1, control2, t);
            }
        }

        internal static Point PerlinNoise(Point start, Point end, double t, double amplitude = 10.0, double frequency = 0.1)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            const double deadzoneRadius = 15.0;
            const double minDamping = 0.1;

            // Easing (Smoothstep) for natural interpolation
            double easeT = t * t * (3 - 2 * t);

            double baseX = start.X + dx * easeT;
            double baseY = start.Y + dy * easeT;

            double currentToEnd = Math.Sqrt((end.X - baseX) * (end.X - baseX) + (end.Y - baseY) * (end.Y - baseY));

            // Scale amplitude by distance and proximity
            double dynamicAmplitude = amplitude * Math.Min(1.0, distance / 100.0) * 0.4;

            // Fade in at start and fade out at end
            double startDamp = Math.Clamp(t * 2.0, 0.0, 1.0);
            double endDamp = Math.Clamp((1.0 - t) * 2.0, 0.0, 1.0);
            double movementDamp = startDamp * endDamp;

            dynamicAmplitude *= movementDamp;

            // Deadzone fade: scale noise down when close to target
            double deadzoneDamp = 1.0;
            if (currentToEnd < deadzoneRadius)
            {
                deadzoneDamp = Math.Clamp(currentToEnd / deadzoneRadius, minDamping, 1.0);
            }

            dynamicAmplitude *= deadzoneDamp;

            // Perlin noise components
            double noiseX = Noise(t * frequency, 0) * dynamicAmplitude;
            double noiseY = Noise(t * frequency, 100) * dynamicAmplitude;

            // Perpendicular vector (normalized)
            double perpX = -dy;
            double perpY = dx;
            double perpLength = Math.Sqrt(perpX * perpX + perpY * perpY);

            if (perpLength > 0)
            {
                perpX /= perpLength;
                perpY /= perpLength;
            }

            // Reduce sideways drift near target
            double lateralDamp = Math.Clamp(currentToEnd / 150.0, 0.3, 1.0);
            noiseX *= lateralDamp;
            noiseY *= 0.1 * lateralDamp;

            // Final point with noise
            double finalX = baseX + perpX * noiseX + noiseY;
            double finalY = baseY + perpY * noiseX + noiseY;

            return new Point((int)finalX, (int)finalY);
        }


        // Update existing GenerateSmoothPath to include deadzone
        internal static List<Point> GenerateSmoothPathWithDeadzone(Point start, Point end, int segments = 20)
        {
            List<Point> path = new List<Point>();
            double distance = Math.Sqrt((end.X - start.X) * (end.X - start.X) + (end.Y - start.Y) * (end.Y - start.Y));

            // Deadzone check - if within 2 arm lengths (about 120 pixels), use flick behavior
            const double deadzoneRadius = 120.0;

            if (distance <= deadzoneRadius)
            {
                return GenerateFlickPath(start, end, distance);
            }

            // Adjust segments based on distance
            int dynamicSegments = Math.Max(5, Math.Min(30, (int)(distance / 10)));

            for (int i = 0; i <= dynamicSegments; i++)
            {
                double t = (double)i / dynamicSegments;

                // Ease-out curve to slow down near target
                double easeT = 1 - Math.Pow(1 - t, 2);

                Point point = PerlinNoise(start, end, easeT,
                    amplitude: distance > 100 ? 8.0 : 3.0,
                    frequency: 0.08);
                path.Add(point);
            }

            return path;
        }

        // New method for deadzone flick behavior
        internal static List<Point> GenerateFlickPath(Point start, Point end, double distance)
        {
            List<Point> path = new List<Point>();
            Random rand = new Random();

            // Quick flick directly to target with slight overshoot
            double overshootFactor = 1.05 + (rand.NextDouble() * 0.1); // 5-15% overshoot
            Point overshootTarget = new Point(
                (int)(start.X + (end.X - start.X) * overshootFactor),
                (int)(start.Y + (end.Y - start.Y) * overshootFactor)
            );

            // Fast movement to overshoot position (2-4 segments)
            int flickSegments = rand.Next(2, 5);
            for (int i = 0; i <= flickSegments; i++)
            {
                double t = (double)i / flickSegments;
                double flickT = t * t; // Ease-in for quick acceleration

                Point flickPoint = new Point(
                    (int)(start.X + (overshootTarget.X - start.X) * flickT),
                    (int)(start.Y + (overshootTarget.Y - start.Y) * flickT)
                );
                path.Add(flickPoint);
            }

            // Smooth centering back to exact target
            Point currentPos = path[path.Count - 1];
            int centerSegments = rand.Next(2, 4);
            for (int i = 1; i <= centerSegments; i++)
            {
                double t = (double)i / centerSegments;
                double centerT = 1 - Math.Pow(1 - t, 2); // Ease-out for smooth centering

                Point centerPoint = new Point(
                    (int)(currentPos.X + (end.X - currentPos.X) * centerT),
                    (int)(currentPos.Y + (end.Y - currentPos.Y) * centerT)
                );
                path.Add(centerPoint);
            }

            return path;
        }

        // Add this method to control movement speed
        internal static List<Point> GenerateSmoothPath(Point start, Point end, int segments = 20)
        {
            List<Point> path = new List<Point>();
            double distance = Math.Sqrt((end.X - start.X) * (end.X - start.X) + (end.Y - start.Y) * (end.Y - start.Y));

            // Adjust segments based on distance
            int dynamicSegments = Math.Max(5, Math.Min(30, (int)(distance / 10)));

            for (int i = 0; i <= dynamicSegments; i++)
            {
                double t = (double)i / dynamicSegments;

                // Ease-out curve to slow down near target
                double easeT = 1 - Math.Pow(1 - t, 2);

                Point point = PerlinNoise(start, end, easeT,
                    amplitude: distance > 100 ? 8.0 : 3.0,
                    frequency: 0.08);
                path.Add(point);
            }

            return path;
        }

        private static double Fade(double t)
        {
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + t * (b - a);
        }

        private static double Grad(int hash, double x, double y)
        {
            int h = hash & 15;
            double u = h < 8 ? x : y;
            double v = h < 4 ? y : h == 12 || h == 14 ? x : 0;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        private static double Noise(double x, double y)
        {
            int X = (int)Math.Floor(x) & 255;
            int Y = (int)Math.Floor(y) & 255;

            x -= Math.Floor(x);
            y -= Math.Floor(y);

            double u = Fade(x);
            double v = Fade(y);

            int A = permutation[X] + Y;
            int AA = permutation[A];
            int AB = permutation[A + 1];
            int B = permutation[X + 1] + Y;
            int BA = permutation[B];
            int BB = permutation[B + 1];

            return Lerp(Lerp(Grad(permutation[AA], x, y),
                           Grad(permutation[BA], x - 1, y), u),
                      Lerp(Grad(permutation[AB], x, y - 1),
                           Grad(permutation[BB], x - 1, y - 1), u), v);
        }
    }
}
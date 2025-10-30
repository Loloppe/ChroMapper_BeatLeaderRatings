using System;
using System.Collections.Generic;
using System.Linq;

namespace BeatLeaderRatings.AccAi
{
    internal class Curve
    {
        public List<(float x, float y)> baseCurve = new()
        {
                (1.0f, 7.424f),
                (0.999f, 6.241f),
                (0.9975f, 5.158f),
                (0.995f, 4.010f),
                (0.9925f, 3.241f),
                (0.99f, 2.700f),
                (0.9875f, 2.303f),
                (0.985f, 2.007f),
                (0.9825f, 1.786f),
                (0.98f, 1.618f),
                (0.9775f, 1.490f),
                (0.975f, 1.392f),
                (0.9725f, 1.315f),
                (0.97f, 1.256f),
                (0.965f, 1.167f),
                (0.96f, 1.094f),
                (0.955f, 1.039f),
                (0.95f, 1.000f),
                (0.94f, 0.931f),
                (0.93f, 0.867f),
                (0.92f, 0.813f),
                (0.91f, 0.768f),
                (0.9f, 0.729f),
                (0.875f, 0.650f),
                (0.85f, 0.581f),
                (0.825f, 0.522f),
                (0.8f, 0.473f),
                (0.75f, 0.404f),
                (0.7f, 0.345f),
                (0.65f, 0.296f),
                (0.6f, 0.256f),
                (0.0f, 0.000f)};

        public List<Point> GetCurve(float predictedAcc, float accRating)
        {
            List<(float x, float y)> points = baseCurve.ToList();

            Point point = new();
            List<Point> curve = point.ToPoints(points).ToList();
            curve = curve.OrderBy(x => x.x).Reverse().ToList();

            return curve;
        }

        public float ToStars(float acc, float accRating, float passRating, float techRating, List<Point> curve)
        {
            float passPP = 15.2f * MathF.Exp(MathF.Pow(passRating, 1 / 2.62f)) - 30f;
            if (float.IsInfinity(passPP) || float.IsNaN(passPP) || float.IsNegativeInfinity(passPP) || passPP < 0)
            {
                passPP = 0;
            }
            float accPP = Curve2(acc, curve) * accRating * 34f;
            float techPP = MathF.Exp((float)(1.9 * acc)) * 1.08f * techRating;

            float pp = 650f * MathF.Pow((float)(passPP + accPP + techPP), 1.3f) / MathF.Pow(650f, 1.3f);

            return pp / 52;
        }

        public float Curve2(float acc, List<Point> curve)
        {
            int i = 0;
            for (; i < curve.Count; i++)
            {
                if (curve[i].x <= acc)
                {
                    break;
                }
            }

            if (i == 0)
            {
                i = 1;
            }

            float middle_dis = (acc - curve[i - 1].x) / (curve[i].x - curve[i - 1].x);
            return (float)(curve[i - 1].y + middle_dis * (curve[i].y - curve[i - 1].y));
        }
    }
}

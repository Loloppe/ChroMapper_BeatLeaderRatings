using System.Collections.Generic;

namespace Ratings.AccAi
{
    public class Point
    {
        public float x { get; set; } = 0;
        public float y { get; set; } = 0;

        public Point()
        {

        }

        public Point(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public List<Point> ToPoints(List<(float x, float y)> curve)
        {
            List<Point> points = new();

            foreach (var p in curve)
            {
                points.Add(new(p.x, p.y));
            }

            return points;
        }
    }
}

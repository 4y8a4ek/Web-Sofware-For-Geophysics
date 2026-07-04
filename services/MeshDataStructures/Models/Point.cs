using System.Collections.Generic;

namespace MeshDataModel
{
    public class Point
    {
        public int Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public List<int> ElementIds { get; set; }

        public Point(int id, double x, double y, double z)
        {
            Id = id;
            X = x; Y = y; Z = z;
            ElementIds = new List<int>();
        }
    }
}
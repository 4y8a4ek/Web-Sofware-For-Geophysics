using System.Collections.Generic;

namespace MeshDataModel
{
    public class Edge
    {
        public int Id { get; set; }
        public int PointId1 { get; set; }
        public int PointId2 { get; set; }
        public List<int> ElementIds { get; set; }

        public Edge(int id, int p1, int p2)
        {
            Id = id;
            PointId1 = p1;
            PointId2 = p2;
            ElementIds = new List<int>();
        }
    }
}
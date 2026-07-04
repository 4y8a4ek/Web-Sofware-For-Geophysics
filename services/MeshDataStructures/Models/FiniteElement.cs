using System.Collections.Generic;
using System.Numerics;

namespace MeshDataModel
{
    public class FiniteElement
    {
        public int Id { get; set; }
        public int[] PointIds { get; set; } 
        public int[] EdgeIds { get; set; }   
        public int[] FaceIds { get; set; }   
        public List<int> NeighborElementIds { get; set; }
        public Vector3 Normal { get; set; }

        public FiniteElement(int id, int p1, int p2, int p3)
        {
            Id = id;
            PointIds = new int[] { p1, p2, p3 };
            EdgeIds = new int[3];
            FaceIds = new int[1];
            NeighborElementIds = new List<int>();
        }
    }
}
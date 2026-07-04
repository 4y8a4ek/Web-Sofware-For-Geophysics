using System.Collections.Generic;

namespace MeshDataModel
{
    public class Face
    {
        public int Id { get; set; }
        public int[] PointIds { get; set; }  
        public int[] EdgeIds { get; set; }  
        public List<int> ElementIds { get; set; }
        public int BoundaryConditionTag { get; set; } // 0-3

        public Face(int id, int p1, int p2, int p3)
        {
            Id = id;
            PointIds = new int[] { p1, p2, p3 };
            EdgeIds = new int[3];
            ElementIds = new List<int>();
            BoundaryConditionTag = 0;
        }
    }
}
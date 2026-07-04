using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MeshDataModel
{
    public static class MeshBuilder
    {
        public static (List<Point> points, List<Edge> edges, List<Face> faces, List<FiniteElement> elements)
            BuildFromTriangles(List<Vector3> vertices, List<(int, int, int)> triangles)
        {
            var points = new List<Point>();
            var edges = new List<Edge>();
            var faces = new List<Face>();
            var elements = new List<FiniteElement>();

            // 1. Создаём точки
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                points.Add(new Point(i, v.X, v.Y, v.Z));
            }

            var edgeMap = new Dictionary<(int, int), int>();
            int edgeId = 0;
            int faceId = 0;

            // 2. Создаём элементы (треугольники) и грани
            foreach (var tri in triangles)
            {
                int id = elements.Count;
                var elem = new FiniteElement(id, tri.Item1, tri.Item2, tri.Item3);
                elements.Add(elem);

                // Создаём грань для этого треугольника
                var face = new Face(faceId++, tri.Item1, tri.Item2, tri.Item3);
                face.ElementIds.Add(id);
                faces.Add(face);
                elem.FaceIds[0] = face.Id;
                elem.Normal = Vector3.Normalize(
                    Vector3.Cross(
                        vertices[tri.Item2] - vertices[tri.Item1],
                        vertices[tri.Item3] - vertices[tri.Item1]
                    )
                );

                // Добавляем элемент в точки
                points[tri.Item1].ElementIds.Add(id);
                points[tri.Item2].ElementIds.Add(id);
                points[tri.Item3].ElementIds.Add(id);
            }

            // 3. Строим рёбра
            foreach (var elem in elements)
            {
                var pts = elem.PointIds;
                var pairs = new (int, int)[]
                {
                    (pts[0], pts[1]), (pts[0], pts[2]), (pts[1], pts[2])
                };
                for (int i = 0; i < 3; i++)
                {
                    var (a, b) = pairs[i];
                    int min = Math.Min(a, b);
                    int max = Math.Max(a, b);
                    var key = (min, max);
                    if (!edgeMap.TryGetValue(key, out int eid))
                    {
                        eid = edgeId++;
                        var edge = new Edge(eid, min, max);
                        edges.Add(edge);
                        edgeMap[key] = eid;
                    }
                    elem.EdgeIds[i] = edgeMap[key];
                    edges[edgeMap[key]].ElementIds.Add(elem.Id);
                }
            }

            // 4. Связываем рёбра с гранями
            foreach (var face in faces)
            {
                var pts = face.PointIds;
                var pairs = new (int, int)[]
                {
                    (pts[0], pts[1]),
                    (pts[0], pts[2]),
                    (pts[1], pts[2])
                };
                for (int i = 0; i < 3; i++)
                {
                    var (a, b) = pairs[i];
                    int min = Math.Min(a, b);
                    int max = Math.Max(a, b);
                    var key = (min, max);
                    if (edgeMap.TryGetValue(key, out int eid))
                    {
                        face.EdgeIds[i] = eid;
                    }
                }
            }

            // 5. Поиск соседей по граням (для треугольников)
            var faceToElements = new Dictionary<int, List<int>>();
            foreach (var face in faces)
            {
                faceToElements[face.Id] = face.ElementIds;
            }

            foreach (var elem in elements)
            {
                int faceIdLocal = elem.FaceIds[0];
                var elemList = faceToElements[faceIdLocal];
                foreach (var otherElemId in elemList)
                {
                    if (otherElemId != elem.Id)
                    {
                        elem.NeighborElementIds.Add(otherElemId);
                    }
                }
            }

            return (points, edges, faces, elements);
        }
    }
}
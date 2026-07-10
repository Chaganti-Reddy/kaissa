using System.Collections.Generic;
using UnityEngine;

// Builds a surface of revolution ("turned" shape) by spinning a 2D profile (radius, height) around
// the Y axis. Most chess pieces are rotationally symmetric, so this yields clean lathed pawns,
// rooks, bishops, queens, and kings from a silhouette - our own geometry, no assets.
public static class LatheMesh
{
    public static Mesh Build(Vector2[] profile, int segments = 28)
    {
        int cols = segments + 1;
        var vertices = new List<Vector3>(profile.Length * cols);

        for (int r = 0; r < profile.Length; r++)
        {
            for (int s = 0; s < cols; s++)
            {
                float angle = 2f * Mathf.PI * s / segments;
                vertices.Add(new Vector3(
                    profile[r].x * Mathf.Cos(angle),
                    profile[r].y,
                    profile[r].x * Mathf.Sin(angle)));
            }
        }

        var triangles = new List<int>();
        for (int r = 0; r < profile.Length - 1; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int a = r * cols + s;
                int b = a + 1;
                int c = (r + 1) * cols + s;
                int d = c + 1;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }
        }

        var mesh = new Mesh { name = "lathe" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}

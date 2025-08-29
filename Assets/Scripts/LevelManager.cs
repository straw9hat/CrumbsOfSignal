using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PolygonCollider2D))]
public class LevelManager : MonoBehaviour
{
    [Header("Materials")]
    [SerializeField] private Material topMaterial;    // gradient top
    [SerializeField] private Material sideMaterial;   // darker sides

    [Header("Initial Fill Gradient")]
    [SerializeField] private Color baseInner = new Color(0.25f, 0.75f, 0.65f, 1f);
    [SerializeField] private Color baseOuter = new Color(0.10f, 0.45f, 0.40f, 1f);

    [Header("Geometry")]
    [SerializeField, Min(0.01f)] private float height = 0.2f;  // thickness (Z)
    [SerializeField, Min(0.05f)] private float defaultRingThickness = 1.0f;
    [SerializeField] private bool drawGizmos = true;

    private PolygonCollider2D poly;
    private List<Vector2> currentPolyWS; // current outer boundary in world space

    void Awake()
    {
        poly = GetComponent<PolygonCollider2D>();

        var arr = poly.GetPath(0);
        currentPolyWS = new List<Vector2>(arr.Length);
        for (int i = 0; i < arr.Length; i++)
            currentPolyWS.Add(transform.TransformPoint(arr[i]));

        // Build initial filled “plateau”
        BuildFilledPlateau(currentPolyWS, baseInner, baseOuter);
    }

    // Call this after each wave
    public void ExpandOnce(float ringThickness, Color inner, Color outer)
    {
        var outerPoly = OffsetPolygon(currentPolyWS, ringThickness <= 0 ? defaultRingThickness : ringThickness);

        // Create top ring (between old and new)
        var ringTopGO = new GameObject($"RingTop_{transform.childCount}", typeof(MeshFilter), typeof(MeshRenderer));
        ringTopGO.transform.SetParent(transform, true);
        BuildRingTopMesh(ringTopGO, currentPolyWS, outerPoly, topMaterial, inner, outer);

        // Create side walls around the new outer
        var sideGO = new GameObject($"RingSides_{transform.childCount}", typeof(MeshFilter), typeof(MeshRenderer));
        sideGO.transform.SetParent(transform, true);
        BuildSideWalls(sideGO, outerPoly, sideMaterial, height);

        currentPolyWS = outerPoly;
    }

    // ---------- Builders ----------

    void BuildFilledPlateau(List<Vector2> polyWS, Color c0, Color c1)
    {
        // Top
        var topGO = new GameObject("BaseTop", typeof(MeshFilter), typeof(MeshRenderer));
        topGO.transform.SetParent(transform, true);
        BuildFillTopMesh(topGO, polyWS, topMaterial, c0, c1);

        // Sides (around the outer edge)
        var sidesGO = new GameObject("BaseSides", typeof(MeshFilter), typeof(MeshRenderer));
        sidesGO.transform.SetParent(transform, true);
        BuildSideWalls(sidesGO, polyWS, sideMaterial, height);
    }

    void BuildFillTopMesh(GameObject go, List<Vector2> polyWS, Material mat, Color inner, Color outer)
    {
        Triangulate(polyWS, out var verts, out var tris, out var uvs);

        // Raise slightly so sides don’t z-fight
        for (int i = 0; i < verts.Count; i++)
            verts[i] = new Vector3(verts[i].x, verts[i].y, 0f);

        var mesh = new Mesh { name = "FillTop" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        go.GetComponent<MeshFilter>().sharedMesh = mesh;

        var m = new Material(mat);
        // Expect shader to have _InnerColor / _OuterColor (or rename here to match your shader)
        if (m.HasProperty("_InnerColor")) m.SetColor("_InnerColor", inner);
        if (m.HasProperty("_OuterColor")) m.SetColor("_OuterColor", outer);
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
    }

    void BuildRingTopMesh(GameObject go, List<Vector2> innerWS, List<Vector2> outerWS, Material mat, Color inner, Color outer)
    {
        // Build a strip (two verts per vertex index) with V=0 at inner, V=1 at outer
        int n = Mathf.Min(innerWS.Count, outerWS.Count);
        var verts = new List<Vector3>(n * 4);
        var uvs = new List<Vector2>(n * 4);
        var tris = new List<int>(n * 6);

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;

            Vector3 i0 = new Vector3(innerWS[i].x, innerWS[i].y, 0f);
            Vector3 o0 = new Vector3(outerWS[i].x, outerWS[i].y, 0f);
            Vector3 i1 = new Vector3(innerWS[j].x, innerWS[j].y, 0f);
            Vector3 o1 = new Vector3(outerWS[j].x, outerWS[j].y, 0f);

            int baseIdx = verts.Count;
            verts.Add(i0); uvs.Add(new Vector2(0, 0));
            verts.Add(o0); uvs.Add(new Vector2(0, 1));
            verts.Add(i1); uvs.Add(new Vector2(1, 0));
            verts.Add(o1); uvs.Add(new Vector2(1, 1));

            tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 3); tris.Add(baseIdx + 1);
        }

        var mesh = new Mesh { name = "RingTop" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        go.GetComponent<MeshFilter>().sharedMesh = mesh;

        var m = new Material(mat);
        if (m.HasProperty("_InnerColor")) m.SetColor("_InnerColor", inner);
        if (m.HasProperty("_OuterColor")) m.SetColor("_OuterColor", outer);
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
    }

    void BuildSideWalls(GameObject go, List<Vector2> borderWS, Material mat, float h)
    {
        int n = borderWS.Count;
        var verts = new List<Vector3>(n * 4);
        var uvs = new List<Vector2>(n * 4);
        var tris = new List<int>(n * 6);

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;

            Vector3 topA = new Vector3(borderWS[i].x, borderWS[i].y, 0f);
            Vector3 topB = new Vector3(borderWS[j].x, borderWS[j].y, 0f);
            Vector3 botA = new Vector3(borderWS[i].x, borderWS[i].y, -h);
            Vector3 botB = new Vector3(borderWS[j].x, borderWS[j].y, -h);

            int baseIdx = verts.Count;
            // A strip per edge (A?B), UV.y: 0 at top, 1 at bottom for vertical gradient if desired
            verts.Add(topA); uvs.Add(new Vector2(0, 0));
            verts.Add(topB); uvs.Add(new Vector2(1, 0));
            verts.Add(botA); uvs.Add(new Vector2(0, 1));
            verts.Add(botB); uvs.Add(new Vector2(1, 1));

            // two tris
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 3); tris.Add(baseIdx + 1);
        }

        var mesh = new Mesh { name = "SideWalls" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // ---------- Geometry utilities (simple offset + triangulation) ----------

    List<Vector2> OffsetPolygon(List<Vector2> polyWS, float d)
    {
        int n = polyWS.Count;
        var result = new List<Vector2>(n);
        for (int i = 0; i < n; i++)
        {
            Vector2 p0 = polyWS[(i - 1 + n) % n];
            Vector2 p1 = polyWS[i];
            Vector2 p2 = polyWS[(i + 1) % n];

            Vector2 e0 = (p1 - p0).normalized;
            Vector2 n0 = new Vector2(-e0.y, e0.x);
            Vector2 e1 = (p2 - p1).normalized;
            Vector2 n1 = new Vector2(-e1.y, e1.x);

            Vector2 l0p = p1 + n0 * d;
            Vector2 l0d = e0;
            Vector2 l1p = p1 + n1 * d;
            Vector2 l1d = e1;

            if (LineLineIntersection(l0p, l0d, l1p, l1d, out Vector2 inter))
                result.Add(inter);
            else
                result.Add(l0p);
        }
        return result;
    }

    static bool LineLineIntersection(Vector2 p, Vector2 r, Vector2 q, Vector2 s, out Vector2 hit)
    {
        float rxs = r.x * s.y - r.y * s.x;
        if (Mathf.Abs(rxs) < 1e-6f) { hit = p; return false; }
        Vector2 qp = q - p;
        float t = (qp.x * s.y - qp.y * s.x) / rxs;
        hit = p + r * t;
        return true;
    }

    void Triangulate(List<Vector2> polyWS, out List<Vector3> verts, out List<int> tris, out List<Vector2> uvs)
    {
        verts = new List<Vector3>(polyWS.Count);
        foreach (var p in polyWS) verts.Add(new Vector3(p.x, p.y, 0f));

        var bounds = GetBounds(polyWS);
        uvs = new List<Vector2>(polyWS.Count);
        for (int i = 0; i < polyWS.Count; i++)
        {
            float u = Mathf.InverseLerp(bounds.min.x, bounds.max.x, polyWS[i].x);
            float v = Mathf.InverseLerp(bounds.min.y, bounds.max.y, polyWS[i].y);
            uvs.Add(new Vector2(u, v));
        }

        tris = EarClip(polyWS);
    }

    static List<int> EarClip(List<Vector2> poly)
    {
        var indices = new List<int>();
        int n = poly.Count;
        var V = new List<int>(n);
        for (int i = 0; i < n; i++) V.Add(i);

        bool ccw = PolygonArea(poly) > 0;
        int guard = 0;
        while (V.Count > 2 && guard++ < 10000)
        {
            bool ear = false;
            for (int i = 0; i < V.Count; i++)
            {
                int a = V[(i - 1 + V.Count) % V.Count];
                int b = V[i];
                int c = V[(i + 1) % V.Count];

                if (!IsConvex(poly[a], poly[b], poly[c], ccw)) continue;

                bool contains = false;
                for (int k = 0; k < V.Count; k++)
                {
                    int p = V[k];
                    if (p == a || p == b || p == c) continue;
                    if (PointInTri(poly[p], poly[a], poly[b], poly[c])) { contains = true; break; }
                }
                if (contains) continue;

                indices.Add(a); indices.Add(b); indices.Add(c);
                V.RemoveAt(i);
                ear = true;
                break;
            }
            if (!ear) break;
        }
        return indices;
    }

    static bool IsConvex(Vector2 a, Vector2 b, Vector2 c, bool ccw)
    {
        float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
        return ccw ? cross > 0 : cross < 0;
    }

    static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float c1 = Cross(b - a, p - a);
        float c2 = Cross(c - b, p - b);
        float c3 = Cross(a - c, p - c);
        bool neg = (c1 < 0) || (c2 < 0) || (c3 < 0);
        bool pos = (c1 > 0) || (c2 > 0) || (c3 > 0);
        return !(neg && pos);
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    static float PolygonArea(List<Vector2> p)
    {
        float area = 0;
        for (int i = 0; i < p.Count; i++)
        {
            int j = (i + 1) % p.Count;
            area += p[i].x * p[j].y - p[j].x * p[i].y;
        }
        return 0.5f * area;
    }

    static Bounds GetBounds(List<Vector2> p)
    {
        Vector2 min = new(float.MaxValue, float.MaxValue);
        Vector2 max = new(float.MinValue, float.MinValue);
        for (int i = 0; i < p.Count; i++)
        {
            min = Vector2.Min(min, p[i]);
            max = Vector2.Max(max, p[i]);
        }
        return new Bounds((min + max) * 0.5f, max - min);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || currentPolyWS == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < currentPolyWS.Count; i++)
        {
            var a = currentPolyWS[i];
            var b = currentPolyWS[(i + 1) % currentPolyWS.Count];
            Gizmos.DrawLine(a, b);
        }
    }
}

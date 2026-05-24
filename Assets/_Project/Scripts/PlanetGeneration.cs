using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlanetGeneration : MonoBehaviour
{
    [Header("Icosphere")]
    [SerializeField, Range(1, 6)] private int subdivisions = 4;
    [SerializeField] private float radius = 1f;

    [Header("Planet Terrain"), Space(5)]
    [SerializeField] float noiseScale = 1f;
    [SerializeField] float continentScale = 1.2f;
    [SerializeField] float continentStrength = 0.3f;
    [SerializeField] float mountainScale = 4f;
    [SerializeField] float mountainStrength = 0.15f;
    [SerializeField] float oceanDepth = 0.5f;
    [SerializeField] float oceanFloor = 0.2f;
    [SerializeField, Range(1, 8)] int noiseOctaves = 5;
    [SerializeField] float lacunarity = 2.2f;
    [SerializeField] float persistence = 0.55f;

    [Header("Compute"), Space(5)]
    [SerializeField] ComputeShader computeShader;

    Mesh mesh;
    ComputeBuffer vertexBuffer;
    ComputeBuffer heightBuffer;
    Vector3[] originalVerts;
    int kernel;

    void OnValidate() => Generate();
    void Awake() => Generate();
    void OnDestroy() { vertexBuffer?.Release(); heightBuffer?.Release(); }

    void Generate()
    {
        if (computeShader == null) return;

        mesh = CreateIcosphere(subdivisions, radius);
        GetComponent<MeshFilter>().mesh = mesh;
        originalVerts = mesh.vertices;

        vertexBuffer?.Release();
        heightBuffer?.Release();

        vertexBuffer = new ComputeBuffer(originalVerts.Length, sizeof(float) * 3);
        vertexBuffer.SetData(originalVerts);
        heightBuffer = new ComputeBuffer(originalVerts.Length, sizeof(float));

        kernel = computeShader.FindKernel("CSMain");
    }

    void Update()
    {
        if (computeShader == null || vertexBuffer == null) return;

        UpdateShaderProperties();
        computeShader.Dispatch(kernel, Mathf.CeilToInt(originalVerts.Length / 512f), 1, 1);

        float[] heights = new float[originalVerts.Length];
        heightBuffer.GetData(heights);

        Vector3[] newVerts = new Vector3[originalVerts.Length];
        for (int i = 0; i < originalVerts.Length; i++)
            newVerts[i] = originalVerts[i] + originalVerts[i].normalized * heights[i];

        mesh.vertices = newVerts;
        mesh.RecalculateNormals();
    }

    void UpdateShaderProperties()
    {
        computeShader.SetBuffer(kernel, "vertices", vertexBuffer);
        computeShader.SetBuffer(kernel, "heights", heightBuffer);
        computeShader.SetInt("numVertices", originalVerts.Length);
        computeShader.SetFloat("noiseScale", noiseScale);
        computeShader.SetFloat("continentScale", continentScale);
        computeShader.SetFloat("continentStrength", continentStrength);
        computeShader.SetFloat("mountainScale", mountainScale);
        computeShader.SetFloat("mountainStrength", mountainStrength);
        computeShader.SetFloat("oceanDepth", oceanDepth);
        computeShader.SetFloat("oceanFloor", oceanFloor);
        computeShader.SetInt("noiseOctaves", noiseOctaves);
        computeShader.SetFloat("lacunarity", lacunarity);
        computeShader.SetFloat("persistence", persistence);
    }

    Mesh CreateIcosphere(int subdivisions, float radius){
        float t = (1f + Mathf.Sqrt(5f)) / 2f;

        var verts = new List<Vector3>{
            new(-1,  t,  0), new( 1,  t,  0), new(-1, -t,  0), new( 1, -t,  0),
            new( 0, -1,  t), new( 0,  1,  t), new( 0, -1, -t), new( 0,  1, -t),
            new( t,  0, -1), new( t,  0,  1), new(-t,  0, -1), new(-t,  0,  1)
        };

        for (int i = 0; i < verts.Count; i++)
            verts[i] = verts[i].normalized * radius;

        var tris = new List<int>{
            0,11,5,  0,5,1,  0,1,7,  0,7,10,  0,10,11,
            1,5,9,  5,11,4,  11,10,2,  10,7,6,  7,1,8,
            3,9,4,  3,4,2,  3,2,6,  3,6,8,  3,8,9,
            4,9,5,  2,4,11,  6,2,10,  8,6,7,  9,8,1
        };

        var midCache = new Dictionary<long, int>();
        for (int s = 0; s < subdivisions; s++){
            var newTris = new List<int>();
            for (int i = 0; i < tris.Count; i += 3){
                int a = GetMid(tris[i],   tris[i+1], verts, midCache, radius);
                int b = GetMid(tris[i+1], tris[i+2], verts, midCache, radius);
                int c = GetMid(tris[i+2], tris[i],   verts, midCache, radius);
                newTris.AddRange(new[] { tris[i],a,c, tris[i+1],b,a, tris[i+2],c,b, a,b,c });
            }
            tris = newTris;
        }

        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    int GetMid(int a, int b, List<Vector3> verts, Dictionary<long, int> cache, float radius){
        long key = ((long)Mathf.Min(a, b) << 32) + Mathf.Max(a, b);
        if (cache.TryGetValue(key, out int idx)) return idx;
        Vector3 mid = ((verts[a] + verts[b]) / 2f).normalized * radius;
        verts.Add(mid);
        cache[key] = verts.Count - 1;
        return verts.Count - 1;
    }

}
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Splines;
#endif

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using Interpolators = UnityEngine.Splines.Interpolators;


[ExecuteInEditMode]
[DisallowMultipleComponent]
[RequireComponent(typeof(SplineContainer), typeof(MeshRenderer), typeof(MeshFilter))]
public class SplineRoadMesh : MonoBehaviour
{


    public Material roadMaterials;
    private Material prevRoadMaterials;
    [SerializeField]
    public float RoadWidth = 8f;
    public float RoadHeight = 0f;
    public bool MeshEnable = true;

    private float _pWidths = 0f;
    private float _pHeights = 0f;
    private bool _pMeshEnable = false;

    [SerializeField]
    SplineContainer _spline;

    private Transform KnotTarget;

    public SplineContainer Container
    {
        get
        {
            if (_spline == null)
                _spline = GetComponent<SplineContainer>();

            return _spline;
        }
        set => _spline = value;
    }

    [SerializeField]
    private int _segmentsPerMeter = 1;

    [SerializeField]
    private Mesh _mesh;

    [SerializeField]
    private float _textureScale = 1f;

    private float _pTextureScale = 1f;

    public IReadOnlyList<Spline> Splines => LoftSplines;

    public IReadOnlyList<Spline> LoftSplines
    {
        get
        {
            if (_spline == null)
                _spline = GetComponent<SplineContainer>();

            if (_spline == null)
            {
                Debug.LogError("Cannot loft road mesh because Spline reference is null");
                return null;
            }

            return _spline.Splines;
        }
    }

    [Obsolete("Use LoftMesh instead.", false)]
    public Mesh mesh => LoftMesh;
    public Mesh LoftMesh
    {
        get
        {
            if (_mesh != null)
                return _mesh;
            _mesh = new Mesh();
            return _mesh;
        }
    }

    [Obsolete("Use SegmentsPerMeter instead.", false)]
    public int segmentsPerMeter => SegmentsPerMeter;
    public int SegmentsPerMeter => Mathf.Min(10, Mathf.Max(1, _segmentsPerMeter));

    List<Vector3> m_Positions = new List<Vector3>();
    List<Vector3> m_Normals = new List<Vector3>();
    List<Vector2> m_Textures = new List<Vector2>();
    List<int> m_Indices = new List<int>();

    public void OnEnable()
    {
        // Avoid to point to an existing instance when duplicating the GameObject
        if (_mesh != null)
            _mesh = null;

        if (_spline == null)
            _spline = GetComponent<SplineContainer>();

        LoftAllRoads();

#if UNITY_EDITOR
        EditorSplineUtility.AfterSplineWasModified += OnAfterSplineWasModified;
        Undo.undoRedoPerformed += LoftAllRoads;
#endif

        Spline.Changed += OnSplineChanged;
    }

    public void OnDisable()
    {
#if UNITY_EDITOR
        EditorSplineUtility.AfterSplineWasModified -= OnAfterSplineWasModified;
        Undo.undoRedoPerformed -= LoftAllRoads;
#endif

        if (_mesh != null)
#if UNITY_EDITOR
            DestroyImmediate(_mesh);
#else
                Destroy(_mesh);
#endif
        Spline.Changed -= OnSplineChanged;
    }
#if UNITY_EDITOR
    void Update()
    {

        if (_pWidths != RoadWidth)
        {
            LoftAllRoads();
            _pWidths = RoadWidth;
        }

        if (_pHeights != RoadHeight)
        {
            LoftAllRoads();
            _pHeights = RoadHeight;
        }
        if (MeshEnable != _pMeshEnable)
        {
            LoftAllRoads();
            _pMeshEnable = MeshEnable;
        }

        if (_textureScale != _pTextureScale)
        {
            LoftAllRoads();
            _pTextureScale = _textureScale;
        }
        if(prevRoadMaterials != roadMaterials)
        {
            LoftAllRoads();
            prevRoadMaterials = roadMaterials;
        }

    }
#endif

    void OnAfterSplineWasModified(Spline s)
    {
        if (LoftSplines == null)
            return;

        foreach (var spline in LoftSplines)
        {
            if (s == spline)
            {
                LoftAllRoads();
                break;
            }
        }
    }



    void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
    {
        OnAfterSplineWasModified(spline);
    }


    public void LoftAllRoads()
    {
        if (!MeshEnable)
        {
            LoftMesh.Clear();
        }

        else
        {
            LoftMesh.Clear();

            m_Positions.Clear();
            m_Normals.Clear();
            m_Textures.Clear();
            m_Indices.Clear();

            for (var i = 0; i < LoftSplines.Count; i++)
                Loft(LoftSplines[i], RoadWidth, RoadHeight);

            LoftMesh.SetVertices(m_Positions);
            LoftMesh.SetNormals(m_Normals);
            LoftMesh.SetUVs(0, m_Textures);
            LoftMesh.subMeshCount = 1;
            LoftMesh.SetIndices(m_Indices, MeshTopology.Triangles, 0);
            LoftMesh.UploadMeshData(false);
            GetComponent<MeshRenderer>().sharedMaterial = roadMaterials;
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }



    }

    public void Loft(Spline spline, float width, float height)
    {
        if (spline == null || spline.Count < 2)
            return;

        LoftMesh.Clear();

        float length = spline.GetLength();

        if (length <= 0.001f)
            return;

        var segmentsPerLength = SegmentsPerMeter * length;
        var segments = Mathf.CeilToInt(segmentsPerLength);
        var segmentStepT = (1f / SegmentsPerMeter) / length;
        var steps = segments + 1;
        var vertexCount = steps * 2;
        var triangleCount = segments * 6;
        var prevVertexCount = m_Positions.Count;

        m_Positions.Capacity += vertexCount;
        m_Normals.Capacity += vertexCount;
        m_Textures.Capacity += vertexCount;
        m_Indices.Capacity += triangleCount;

        var t = 0f;
        for (int i = 0; i < steps; i++)
        {
            SplineUtility.Evaluate(spline, t, out var pos, out var dir, out var up);

            // If dir evaluates to zero (linear or broken zero length tangents?)
            // then attempt to advance forward by a small amount and build direction to that point
            if (math.length(dir) == 0)
            {
                var nextPos = spline.GetPointAtLinearDistance(t, 0.01f, out _);
                dir = math.normalizesafe(nextPos - pos);

                if (math.length(dir) == 0)
                {
                    nextPos = spline.GetPointAtLinearDistance(t, -0.01f, out _);
                    dir = -math.normalizesafe(nextPos - pos);
                }

                if (math.length(dir) == 0)
                    dir = new float3(0, 0, 1);
            }

            var scale = transform.lossyScale;

            var tanFlat = new float3(dir.x, dir.y, 0f);

            var tangent = math.normalizesafe(math.cross(up, dir)) * new float3(1f / scale.x, 1f / scale.y, 1f / scale.z);

            var w = width;
            var newPos = new float3(pos.x, pos.y + height, pos.z);

            m_Positions.Add(newPos - (tangent * w));
            m_Positions.Add(newPos + (tangent * w));
            m_Normals.Add(up);
            m_Normals.Add(up);
            m_Textures.Add(new Vector2(0f, t * _textureScale));
            m_Textures.Add(new Vector2(1f, t * _textureScale));

            t = math.min(1f, t + segmentStepT);
        }

        for (int i = 0, n = prevVertexCount; i < triangleCount; i += 6, n += 2)
        {
            m_Indices.Add((n + 2) % (prevVertexCount + vertexCount));
            m_Indices.Add((n + 1) % (prevVertexCount + vertexCount));
            m_Indices.Add((n + 0) % (prevVertexCount + vertexCount));
            m_Indices.Add((n + 2) % (prevVertexCount + vertexCount));
            m_Indices.Add((n + 3) % (prevVertexCount + vertexCount));
            m_Indices.Add((n + 1) % (prevVertexCount + vertexCount));
        }
    }


}


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

namespace Unity.Splines.Roads
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SplineContainer), typeof(MeshRenderer), typeof(MeshFilter))]
    public class SplineWaterMesh : MonoBehaviour
    {

        [SerializeField]
        public float RiverWidth = 8f;
        public float RiverHeight = 0f;
        public bool MeshEnable = true;


        private float p_Widths = 0f;
        private float p_Heights = 0f;
        private bool pMeshEnable = false;

        [SerializeField]
        SplineContainer m_Spline;

        private Transform KnotTarget;

        public SplineContainer Container
        {
            get
            {
                if (m_Spline == null)
                    m_Spline = GetComponent<SplineContainer>();

                return m_Spline;
            }
            set => m_Spline = value;
        }

        [Range(1,10)]
        [SerializeField]
        int m_LengthVertices = 1;
        [Range(2,50)]
        [SerializeField]
        int m_WidthVertices = 5;

        private float p_LengthVertices = 0f;
        private float p_WidthVertices = 0f;

        [SerializeField]
        Mesh m_Mesh;

        [SerializeField]
        float m_TextureScale = 1f;

        private float m_pTextureScale = 1f;

        public IReadOnlyList<Spline> splines => LoftSplines;

        public IReadOnlyList<Spline> LoftSplines
        {
            get
            {
                if (m_Spline == null)
                    m_Spline = GetComponent<SplineContainer>();

                if (m_Spline == null)
                {
                    Debug.LogError("Cannot loft road mesh because Spline reference is null");
                    return null;
                }

                return m_Spline.Splines;
            }
        }

        [Obsolete("Use LoftMesh instead.", false)]
        public Mesh mesh => LoftMesh;
        public Mesh LoftMesh
        {
            get
            {
                if (m_Mesh != null)
                    return m_Mesh;
                m_Mesh = new Mesh();


                
                return m_Mesh;
            }
        }

        [Obsolete("Use SegmentsPerMeter instead.", false)]
        public int segmentsPerMeter => SegmentsPerMeter;
        public int SegmentsPerMeter => Mathf.Min(10, Mathf.Max(1, m_LengthVertices));

        List<Vector3> m_Positions = new List<Vector3>();
        List<Vector3> m_Normals = new List<Vector3>();
        List<Vector2> m_Textures = new List<Vector2>();
        List<int> m_Indices = new List<int>();




        public void OnEnable()
        {
            // Avoid to point to an existing instance when duplicating the GameObject
            if (m_Mesh != null)
                m_Mesh = null;

            if (m_Spline == null)
                m_Spline = GetComponent<SplineContainer>();

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

            if (m_Mesh != null)
#if  UNITY_EDITOR
                DestroyImmediate(m_Mesh);
#else
                Destroy(m_Mesh);
#endif
            Spline.Changed -= OnSplineChanged;
        }
#if  UNITY_EDITOR
        void Update()
        {

            if (p_Widths != RiverWidth)
            {
                LoftAllRoads();
                p_Widths = RiverWidth;
             }

            if (p_Heights != RiverHeight)
            {
                LoftAllRoads();
                p_Heights = RiverHeight;
            }
            if (MeshEnable != pMeshEnable)
            {
                LoftAllRoads();
                pMeshEnable = MeshEnable;
            }

            
            if(m_TextureScale != m_pTextureScale)
            {
                LoftAllRoads();
                m_pTextureScale = m_TextureScale;
            }
            if (m_LengthVertices != p_LengthVertices)
            {
                LoftAllRoads();
                p_LengthVertices = m_LengthVertices;
            }
            if (m_WidthVertices != p_WidthVertices)
            {
                LoftAllRoads();
                p_WidthVertices = m_WidthVertices;
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
            if(!MeshEnable)
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
                Loft(LoftSplines[i], RiverWidth, RiverHeight);

            LoftMesh.SetVertices(m_Positions);
            LoftMesh.SetNormals(m_Normals);
            LoftMesh.SetUVs(0, m_Textures);
            LoftMesh.subMeshCount = 1;
            LoftMesh.SetIndices(m_Indices, MeshTopology.Triangles, 0);
            LoftMesh.UploadMeshData(false);

            GetComponent<MeshFilter>().sharedMesh = m_Mesh;
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
            var triangleCount = segments * 6 * (m_WidthVertices - 1);
            var prevVertexCount = m_Positions.Count;

            var t = 0f;
            for (int i = 0; i < steps; i++)
            {
                SplineUtility.Evaluate(spline, t, out var pos, out var dir, out var up);
                AddWidthVertices(pos, up, dir, width, height, t, steps); // Use the new function to add vertices
                t = math.min(1f, t + segmentStepT);
            }

            // Add indices for the new vertices
            for (int i = 0; i < steps - 1; i++) // Loop through each segment
            {
                for (int w = 0; w < m_WidthVertices - 1; w++) // Loop through width vertices
                {
                    int start = i * m_WidthVertices + w;
                    int nextRow = start + m_WidthVertices;

                    // First triangle of quad
                    m_Indices.Add(start);
                    m_Indices.Add(nextRow);
                    m_Indices.Add(start + 1);

                    // Second triangle of quad
                    m_Indices.Add(start + 1);
                    m_Indices.Add(nextRow);
                    m_Indices.Add(nextRow + 1);
                }
            }

            LoftMesh.SetVertices(m_Positions);
            LoftMesh.SetNormals(m_Normals);
            LoftMesh.SetUVs(0, m_Textures);
            LoftMesh.subMeshCount = 1;
            LoftMesh.SetIndices(m_Indices, MeshTopology.Triangles, 0);
            LoftMesh.UploadMeshData(false);

            GetComponent<MeshFilter>().sharedMesh = m_Mesh;
        }


        private void AddWidthVertices(Vector3 pos, Vector3 up, Vector3 dir, float width, float height, float t, int steps)
        {
            var tangent = Vector3.Cross(up, dir).normalized; // Ensure tangent is perpendicular to 'up'
            var widthStep = width / (m_WidthVertices - 1); // Calculate spacing between vertices along the width.

            for (int w = 0; w < m_WidthVertices; w++)
            {
                // Calculate offset for each width vertex
                var widthOffset = (w - (m_WidthVertices - 1) / 2.0f) * widthStep;
                var vertexPos = pos + tangent * widthOffset + new Vector3(0, height, 0); // Adjust position based on widthOffset

                // Adjust texture coordinates to account for m_TextureScale properly
                float u = (float)w / (m_WidthVertices - 1);
                float v = t * m_TextureScale; // Apply m_TextureScale to scale texture along the spline

                m_Positions.Add(vertexPos);
                m_Normals.Add(Vector3.up); // Point normals upwards
                m_Textures.Add(new Vector2(u, v)); // Use adjusted texture coordinates
            }
        }


    }
}

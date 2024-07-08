using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;


    [ExecuteInEditMode]
    public class SplineRoadMeshController : MonoBehaviour
    {

        public bool DisplayElevationMesh = true;
        public bool manualMode = false;
        public float RoadWidth = 8f;
        public float RoadHeight = 0f;
        public List<SplineRoadMesh> MVSpline = new List<SplineRoadMesh>();
        private float _widths = 0f;
        private float _heights = 0f;
        private bool _pMeshEnable = false;
        private bool _updateAll = false;



        private void ChangeAll()
        {
            MVSpline.Clear();
            FindSplineRoadMeshInChildren(transform);

            foreach (SplineRoadMesh splineRoadMesh in MVSpline)
            {
                splineRoadMesh.RoadHeight = RoadHeight;
                splineRoadMesh.RoadWidth = RoadWidth;
                splineRoadMesh.MeshEnable = DisplayElevationMesh;
            }
        }

        private void FindSplineRoadMeshInChildren(Transform parent)
        {
            foreach (Transform child in parent)
            {
                SplineRoadMesh roadMesh = child.GetComponent<SplineRoadMesh>();
                if (roadMesh != null)
                {
                    MVSpline.Add(roadMesh);
                }

                // Recursively search for SplineRoadMesh in grandchildren
                FindSplineRoadMeshInChildren(child);
            }
        }

        public void HideMesh()
        {
            foreach (SplineRoadMesh splineRoadMesh in MVSpline)
            {
                splineRoadMesh.MeshEnable = false;
            }
        }

        public void DisplayMesh()
        {
            foreach (SplineRoadMesh splineRoadMesh in MVSpline)
            {
                splineRoadMesh.MeshEnable = true;
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (Application.isPlaying)
            {
                DisplayElevationMesh = false;
                return;
            }

            if (manualMode)
            {
                return;
            }
            if (_updateAll)
            {
                ChangeAll();
                _updateAll = false;

            }
            if (_widths != RoadWidth)
            {
                _widths = RoadWidth;
                _updateAll = true;
            }

            if (_heights != RoadHeight)
            {
                _heights = RoadHeight;
                _updateAll = true;
            }
            if (!DisplayElevationMesh != _pMeshEnable)
            {
                _pMeshEnable = DisplayElevationMesh;
                _updateAll = true;
            }

            if(Application.isPlaying)
            {
                DisplayElevationMesh = false;
            }
        }
    }


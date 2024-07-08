using UnityEditor;
using UnityEngine;
using System.Collections.Generic;


[CustomEditor(typeof(SplineRoadMesh))]
public class Celestial_AreasEditor : Editor
{
    private List<string> roadType = new List<string>();
    private List<Material> roadMaterial = new List<Material>();


    public override void OnInspectorGUI()
    {

        LoadTempRoads();
        SplineRoadMesh script = (SplineRoadMesh)target;
        // Dropdown for selecting a temp bed
        int selectedRoadIndex = roadMaterial.IndexOf(script.roadMaterials);
        int newIndex = EditorGUILayout.Popup("Road Material", selectedRoadIndex, roadType.ToArray());

        if (newIndex != selectedRoadIndex)
        {
            // Assign the selected temp bed
            script.roadMaterials = roadMaterial[newIndex];
            EditorUtility.SetDirty(script); // Mark the script as dirty to ensure changes are saved
        }

        DrawDefaultInspector();
    }
    private void LoadTempRoads()
    {
        roadType.Clear();
        roadMaterial.Clear();

        roadType.Add("None");
        roadMaterial.Add(null);

        var beds = Resources.LoadAll<Material>("RoadMaterial");
        foreach (var bed in beds)
        {

            roadType.Add(bed.name);
            roadMaterial.Add(bed);

        }
    }

}

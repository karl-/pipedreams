using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(PipeSpawner))]
public class PipeSpawnerEditor : Editor
{
	PipeSpawner ps;

	void OnEnable()
	{
		ps = (PipeSpawner)target;
	}

	public override void OnInspectorGUI()
	{
		EditorGUI.BeginChangeCheck();

		GUILayout.Label("Scene Settings", EditorStyles.boldLabel);
		
		ps.desiredActivePipeCount = (int)EditorGUILayout.Slider(new GUIContent("Active Pipes", "The number of pipes to run concurrently."), ps.desiredActivePipeCount, 1, 5);
		ps.maxPipesOnScreen = (int)EditorGUILayout.Slider(new GUIContent("Max Pipes on Screen", "The maxmimum number of pipes to show on screen at any point."), ps.maxPipesOnScreen, ps.desiredActivePipeCount, 10);

		GUILayout.Label("Pipe Settings", EditorStyles.boldLabel);

		ps.material = (Material) EditorGUILayout.ObjectField(new GUIContent("Material", "The material that will be used for this pipe."), ps.material, typeof(Material), false);
		ps.fadeMaterial = (Material) EditorGUILayout.ObjectField(new GUIContent("Material", "The material that will be used for this pipe when fading out."), ps.fadeMaterial, typeof(Material), false);

		ps.pipeSize = EditorGUILayout.Slider(new GUIContent("Pipe Size", "The size that each pipe will instantiate with."), ps.pipeSize, .1f, 3f);
		ps.pipeSpeed = EditorGUILayout.Slider(new GUIContent("Pipe Speed", "How fast the pipes will extend themselves."), ps.pipeSpeed, 1f, 30f);
	
		float min = (float)ps.minPipeTurns;
		float max = (float)ps.maxPipeTurns;

		GUILayout.Label("Min: " + ps.minPipeTurns + " Max: " + ps.maxPipeTurns);
		EditorGUILayout.MinMaxSlider(new GUIContent("Max Pipe Turns", "How many turns this pipe should make before ending itself."), ref min, ref max, 50f, 300f);

		ps.minPipeTurns = (int)min;
		ps.maxPipeTurns = (int)max;

		GUILayout.Label("Area Bounds");
		ps.bounds = EditorGUILayout.BoundsField(ps.bounds);

		ps.pausedImage = (Texture2D)EditorGUILayout.ObjectField("Paused Image", ps.pausedImage, typeof(Texture2D), false);

		if(EditorGUI.EndChangeCheck())
			EditorUtility.SetDirty(ps);
	}
}

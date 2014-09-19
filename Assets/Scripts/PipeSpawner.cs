using UnityEngine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using ProBuilder2.Common;

/**
 *	Responsible for spawning and removing pipes.  Also controls
 *  pipe running variables, like speed, size, and length.
 */
public class PipeSpawner : MonoBehaviour
{
	public int desiredActivePipeCount = 1;		///< How many pipes to keep active at once.
	public float pipeSize = 1f;					///< The size each pipe will be instantiated with.
	public float pipeSpeed = 13f;				///< The speed with which pipes will propogate themselves.
	public int maxPipesOnScreen = 7;				///< How many pipes to allow on screen at once.

	public int minPipeTurns = 100;				///< The minimum number of turns a pipe must make before completing itself.
	public int maxPipeTurns = 200;				///< The maximum number of turns a pipe may make before completing itself.

	public Material material;					///< The material to used for pipes.
	public Material fadeMaterial;				///< The material to used for pipes.

	public float fadeTime = 2f;					///< How many seconds it takes to fade a pipe into oblivion.

	public Texture2D pausedImage;				///< Shown when composition is paused.

	public Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 10f);	///< The bounds to stay inside when pipes are running.

	[SerializeField] int activePipes = 0;
	[SerializeField] List<Pipe> finishedPipes = new List<Pipe>();	///< Pipes that have been built out and run out of moves are saved so that we can reset the screen.

#region Initialization

	void Start()
	{
		StartCoroutine( SpawnPipes() );
	}

	/**
	 *	Begin by Spawning pipes at intervals so that you can see it happening.
	 */
	IEnumerator SpawnPipes()
	{
		while(activePipes < desiredActivePipeCount && !paused)
		{
			SpawnPipe();
			yield return new WaitForSeconds( Random.Range(.5f, 1f));
		}
	}
#endregion

#region Set / Get

	public bool IsPaused()
	{
		return paused;
	}

	void SetSpeed(float speed)
	{
		foreach(Pipe pipe in FindObjectsOfType(typeof(Pipe)))
			pipe.SetSpeed(speed);
	}
#endregion

#region Update Loop / GUI

	// GUI Settings vars
	bool showSettings = false;
	public Rect SettingsWindowRect { get { return settingsWindowRect; } }
	Rect settingsWindowRect = new Rect(10, 20, 180, 200);

	void OnGUI()
	{
		showSettings = GUILayout.Toggle(showSettings, "Settings");

		if(showSettings)
		{
			settingsWindowRect = GUILayout.Window(0, settingsWindowRect, SettingsWindow, "Settings");
			settingsWindowRect.x = Mathf.Clamp(settingsWindowRect.x, 0, Screen.width-settingsWindowRect.width);
			settingsWindowRect.y = Mathf.Clamp(settingsWindowRect.y, 0, Screen.height-20);
		}

		if(paused)
		{
			if(pausedImage != null)
			{
				if(GUI.Button(new Rect(Screen.width/2f-pausedImage.width/2f, Screen.height-pausedImage.height-20, pausedImage.width, pausedImage.height), pausedImage, GUIStyle.none))
					TogglePause();
			}
			else
				GUI.Label(new Rect(Screen.width/2f-60, Screen.height-40, 120, 24), "PAUSED");
		}
	}

	bool paused = false;

	void Update()
	{
		if(Input.GetKeyUp(KeyCode.Space))
		{
			TogglePause();
		}
	}

	public void TogglePause()
	{
		paused = !paused;

		if(!paused && activePipes < desiredActivePipeCount)
		{
			SetSpeed(pipeSpeed);
			StartCoroutine(SpawnPipes());
		}

		SetSpeed(paused ? 0f : pipeSpeed);
	}

	void SettingsWindow(int id)
	{
		int tmp = 0;
		bool doUpdate = false;

		GUILayout.Label("\"Escape\" key to Quit\n\"Space\" key to Pause");

		GUILayout.Label("Active Pipes: " + desiredActivePipeCount);
		{
			tmp = desiredActivePipeCount;
			desiredActivePipeCount = (int)GUILayout.HorizontalSlider(desiredActivePipeCount, 1, 8);

			if(desiredActivePipeCount != tmp)
			{
				if(activePipes > desiredActivePipeCount)
				{
					int end = activePipes - desiredActivePipeCount, i = 0;

					foreach(Pipe p in FindObjectsOfType(typeof(Pipe)))
					{
						if(!p.IsPaused())
						{
							p.EndPipe();

							if(++i > end)
								break;
						}
					}
				}
				else
				{
					if(!paused)
						doUpdate = true;
				}
			}
		}

		GUILayout.Label("Max Pipes on Screen: " + maxPipesOnScreen);
		{
			tmp = maxPipesOnScreen;
			maxPipesOnScreen = (int)GUILayout.HorizontalSlider(maxPipesOnScreen, desiredActivePipeCount, 12);
			if(tmp != maxPipesOnScreen)
				doUpdate = true;
		}

		GUILayout.Label("Speed: " + pipeSpeed);
		{
			tmp = (int)pipeSpeed;
			pipeSpeed = GUILayout.HorizontalSlider(pipeSpeed, 1f, 40f);

			if( (int)pipeSpeed != tmp && !paused )
				SetSpeed(pipeSpeed);
		}

		if( GUILayout.Button("Reset") )
		{
			foreach(Pipe pipe in FindObjectsOfType(typeof(Pipe)).Where(x => !((Pipe)x).IsPaused()))
				pipe.EndPipe();
		}

		if(doUpdate)
		{
			ClearPipes();
			if( activePipes < desiredActivePipeCount )
				StartCoroutine( SpawnPipes() );
		}

		GUI.DragWindow(new Rect(0,0,10000,20));
	}
#endregion

#region Pipe Management

	/**
	 * The delegate that Pipe's event handler will call when out of moves.
	 */
	public void OnPipeFinished(Pipe pipe)
	{
		pipe.OnPipeFinished -= this.OnPipeFinished;

		activePipes--;

		finishedPipes.Add(pipe);
			
		// Check if we need to clear any old pipes from the screen to meet the maxPipesOnScreen limit.
		ClearPipes();

		// And finally spawn a new pipe to replace the finished one.
		if(activePipes < desiredActivePipeCount)
			StartCoroutine( SpawnPipes() );
	}

	/**
	 * Create a new Pipe and add it to the activePipes list.
	 */
	void SpawnPipe()
	{
		// pb_Shape_Generator provides API access to all ProBuilder primitives.  All *Generator methods
		// return a reference to the pb_Object created, so you'll need to get the gameObject yourself.
		pb_Object pb = pb_Shape_Generator.CubeGenerator(Vector3.one * pipeSize);

		// Get the gameObject.
		GameObject pipeGameObject = pb.gameObject;
			
		// Move the new gameObject to a random position inside the bounds.
		pipeGameObject.transform.position = GetStartPosition();
		
		// Set the entire object's material.  You can also set just a subset of faces this way.
		pb.SetFaceMaterial(pb.faces, material);

		// Set the AutoUV generation parameter for useWorldSpace - this keeps UVs looking
		// uniform.  You can also set your own UVs using the pb.SetUVs(Vector2[] uv) calls.
		foreach(pb_Face face in pb.faces)
			face.uv.useWorldSpace = true;

		// Add the Pipe component.  This is the script responsible for moving the pipe around
		// and doing most of the interesting stuff.
		Pipe pipe = pipeGameObject.AddComponent<Pipe>();

		// Set the size of the pipe.
		pipe.SetSize(pipeSize);
		// and the speed to build itself out.
		pipe.SetSpeed(pipeSpeed);
		// aaand the bounds.
		pipe.SetBounds(bounds);
		// and finally the amount of turns before it finishes itself.
		pipe.SetMaxTurns( (int) Random.Range(minPipeTurns, maxPipeTurns) );

		// pipe.spawner = this;
		
		// Register for a callback when the pipe completes it's tasks.
		// We'll use this to keep track of what pipes are completed and
		// remove them when necessary.
		pipe.OnPipeFinished += OnPipeFinished;

		// Add this pipe to the activePipes count/
		activePipes++;
	}

	/**
	 *	Will remove completed pipes until the total on screen pipe count is less than maxPipesOnScreen.
	 */
	void ClearPipes()
	{
		int totalPipeCount = activePipes + finishedPipes.Count;

		while( totalPipeCount > maxPipesOnScreen && finishedPipes.Count > 0 )
		{
			finishedPipes[0].GetComponent<Pipe>().FadeOut(fadeTime, 0f, fadeMaterial);
			finishedPipes.RemoveAt(0);
			totalPipeCount--;
		}
	}
#endregion

#region Utility / Gizmos

	/**
	 * Returns a random Vector3 within the bounds.
	 * @todo - This should try to put new pipes in less populated quadants.
	 */
	Vector3 GetStartPosition()
	{
		return new Vector3(
			Random.Range(bounds.center.x - bounds.extents.x, bounds.center.x + bounds.extents.x),
			Random.Range(bounds.center.y - bounds.extents.y, bounds.center.y + bounds.extents.y),
			Random.Range(bounds.center.z - bounds.extents.z, bounds.center.z + bounds.extents.z) );
	}

	/**
	 * Show the active bounds with neat lookin' gizmos.
	 */
	void OnDrawGizmos()
	{
		// // Draw Wireframe
		Vector3 cen = bounds.center;
		Vector3 ext = bounds.extents;

		DrawBoundsEdge(cen, -ext.x, -ext.y, -ext.z, 1f);
		DrawBoundsEdge(cen, -ext.x, -ext.y,  ext.z, 1f);
		DrawBoundsEdge(cen,  ext.x, -ext.y, -ext.z, 1f);
		DrawBoundsEdge(cen,  ext.x, -ext.y,  ext.z, 1f);

		DrawBoundsEdge(cen, -ext.x,  ext.y, -ext.z, 1f);
		DrawBoundsEdge(cen, -ext.x,  ext.y,  ext.z, 1f);
		DrawBoundsEdge(cen,  ext.x,  ext.y, -ext.z, 1f);
		DrawBoundsEdge(cen,  ext.x,  ext.y,  ext.z, 1f);
	}

	private void DrawBoundsEdge(Vector3 center, float x, float y, float z, float size)
	{
		Vector3 p = center;
		p.x += x;
		p.y += y;
		p.z += z;
		Gizmos.DrawLine(p, p + ( -(x/Mathf.Abs(x)) * Vector3.right 		* Mathf.Min(size, Mathf.Abs(x))));
		Gizmos.DrawLine(p, p + ( -(y/Mathf.Abs(y)) * Vector3.up 		* Mathf.Min(size, Mathf.Abs(y))));
		Gizmos.DrawLine(p, p + ( -(z/Mathf.Abs(z)) * Vector3.forward 	* Mathf.Min(size, Mathf.Abs(z))));
	}
#endregion
}

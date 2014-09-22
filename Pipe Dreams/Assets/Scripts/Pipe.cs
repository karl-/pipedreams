using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ProBuilder2.Common;
using ProBuilder2.Math;
using ProBuilder2.MeshOperations;
using System.Linq;

[RequireComponent(typeof(pb_Object))]
[RequireComponent(typeof(MeshCollider))]
public class Pipe : MonoBehaviour
{	
#region Members

	int maximumPipeTurns = 200;							///< How many turns to make before ending this pipe.
	const int MAX_TURNS_PER_SEGMENT = 10;				///< Extruding from large pb_Objects can be slow - so every 10 turns detach to a new object.

	float minimumStretchDistance = 1f;					///< Minimum distance a pipe stretch can be.
	float maximumStretchDistance = 10f;					///< Maximum distance a pipe stretch can be.
	float speed = 3f;									///< Meters per second.

	pb_Object pb;										///< Cache the pb_Object component
	pb_Face[] movingFaces;								///< The faces we're current extruding
	List<pb_Face> neighborFaces = new List<pb_Face>();	///< All faces connected to the extruded faces (they'll need to have their UVs refreshed)
	
	float size = 1f;
	int[] selectedTriangles;							///< All the triangles contained in the movingFaces array.
	Vector3 nrm = Vector3.zero;							///< The normal of the currently extruding face.
	float extrudeDist = 0f;								///< How far to extrude to the current faces.
	float currentDistanceTraveled = 0f;					// How far this pipe has extruded.

	bool isPaused = false;								///< Is this segment paused?
	protected bool isTurn = false;						///< Used to toggle the traveling distance of faces.  If is a turn, only go one meter
														///< then choose a new direction.  Otherwise, choose a random distance and direction.

	Bounds bounds;										///< How far in any direction to go before turning back.

	int turnCount = 0;

	Pipe parent = null;									///< If this pipe is a child, keep track of the parent so we can call OnPipeFinished for only the top.
	Pipe child = null;

	public delegate void OnPipeFinishedEvent(Pipe pipe);
	public event OnPipeFinishedEvent OnPipeFinished;
#endregion

#region Initialization

	/**
	 * Sets things in motion!
	 */
	public void Start()
	{
		pb = GetComponent<pb_Object>();

		// When detaching, we're going right into a turn so we won't get the opportunity to set this in the usual Turn() spot.
		// Set it here.  If this is the first segment in a pipe, this value will be overwritten by Turn().
		movingFaces = new pb_Face[1] { pb.faces[0] };
		selectedTriangles = pb_Face.AllTriangles(movingFaces);
		nrm = pb_Math.Normal(pb, movingFaces[0]);

		Turn();
	}
#endregion

#region Get

	/**
	 * Is this segment finished running?
	 */
	public bool IsPaused()
	{
		return isPaused;
	}
#endregion

#region Set

	/**
	 *	Stops new appendages from sprouting and fires the OnPipeFinished event.
	 */
	public void EndPipe()
	{
		Pipe p = this;
		while(p.child != null)
			p = p.child;

		p.EndPipe_Internal();
	}

	/**
	 * We only ever really want to call EndPipe from the most recent child in the chain, so expose 
	 * that in EndPipe() and keep the actual endpipe call here where we know it's being called 
	 * correctly.
	 */
	protected void EndPipe_Internal()
	{
		Pause();

		if( OnPipeFinished != null)
			OnPipeFinished(this);

		this.enabled = false;
	}

	/**
	 * OnPipeFinished should only be called once, from the first spawned pipe in this chain.
	 * This allows instance objects to keep track of who's top.
	 */
	public void SetParent(Pipe parent)
	{
		this.parent = parent;
	}

	/**
	 * Pause extrusion.  This segment is finished.
	 */
	void Pause()
	{
		isPaused = true;
	}

	/**
	 *	Set the size of the pipe.  Used when creating elbows for turns.
	 */
	public void SetSize(float size)
	{
		this.size = size;
	}

	/**
	 *	Set the maximum bounds within which appendages may roam.
	 */
	public void SetBounds(Bounds bounds)
	{
		this.bounds = bounds;
	}

	/**
	 *	Set the speed with which this pipe will be elongated.
	 */
	public void SetSpeed(float speed)
	{
		this.speed = speed;
	}

	/**
	 *	Set the minimum and maximum distance that pipe lengths may travel before turning.
	 */
	public void SetStretchRange(float min, float max)
	{
		this.minimumStretchDistance = min;
		this.maximumStretchDistance = max;
	}

	/**
	 *	If the pipe doesn't run out of turns before this value, complete the pipe.
	 */
	public void SetMaxTurns(int maxTurns)
	{
		this.maximumPipeTurns = maxTurns;
	}
#endregion

#region Loop

	/**
	 * The Update loop.  Moves vertices using TranslateVertices and updates the UVs where necesary.
	 */
	void Update()
	{
		// If paused, don't do anything.
		if(isPaused) return;

		float delta = speed * Time.deltaTime;

		// Check if we're going to overshoot our target extrude distance, and if so clip the delta
		if(currentDistanceTraveled + delta >= extrudeDist)
			delta = extrudeDist - currentDistanceTraveled;

		// Increment the currentDistanceTraveled var
		currentDistanceTraveled += delta;

		// Move the selected faces.
		pb.TranslateVertices(selectedTriangles, nrm * delta);

		// When moving faces, you also need to update the UVs that would be affected by movement.
		// RefreshUVs() accepts an array of faces so that you can control exactly which UVs are 
		// updated.  This is important because re-projecting UVs can be expensive, so you want to
		// keep these calls to as few faces as is necessary.
		pb.RefreshUV(neighborFaces.ToArray());

		// If we've traveled as far as necessary, turn!
		if(currentDistanceTraveled >= extrudeDist)
			Turn();
	}	
#endregion

#region Branching

	/**
	 * Extrudes a faces out from the current arm, then picks
	 * a new face to extrude from and determines how far it should extrude.
	 */
	void Turn()
	{
		if(turnCount > MAX_TURNS_PER_SEGMENT && isTurn)
		{
			DetachChild();
			return;
		}

		// Check to see if this object is out of turns.
		if(turnCount > maximumPipeTurns)
			EndPipe_Internal();

		// Set the extrude distance to either elbow distance, or a new pipe distance.
		extrudeDist = isTurn ? size : Random.Range(minimumStretchDistance, maximumStretchDistance);
	
		// Assign the MeshCollider's mesh to match the currently rendering mesh.  `pb.msh` is just 
		// an alias for `pb.gameObject.GetComponent<MeshFilter>().sharedMesh`.
		GetComponent<MeshCollider>().sharedMesh = pb.msh;

		// If this is a turning peice, just extrude and move the faces along the same normal for an additional 1m.
		if(!isTurn)
		{
			bool invalidDirection = true;

			List<int> invalidFaces = new List<int>();
			int faceCount = neighborFaces.Count > 0 ? neighborFaces.Count : pb.faces.Length;

			/**
			 *	If the face chosen to extrude from will collide with either the bounds or another pipe, try extruding from a different face.
			 *	In the event that no face can be extruded safely, end the pipe.
			 */
			while( invalidDirection && invalidFaces.Count < faceCount )
			{
				//If this is the first turn, just grab any face on the object.  Otherwise, use
				//one of the neighbor faces.
				int faceIndex;

				// Get a face that hasn't already failed collision detection.
				int n = 0;
				do {
					faceIndex = (int)Random.Range(0, faceCount);
					if(n++ > 20) {	// this shouldn't happen often, but when it does it's crazy annoying and Unity freezes.
						EndPipe_Internal();
						return;
					}
				} while(invalidFaces.Contains(faceIndex));

				// If this is the first run, neighborFaces won't be populated yet, so just choose a random face to start with.
				if(neighborFaces.Count > 0)
					movingFaces = new pb_Face[] { neighborFaces[ faceIndex ] };
				else
					movingFaces = new pb_Face[] { pb.faces[ faceIndex ] };

				// Get all the triangles contained in the currently moving faces array.  TranslateVertices
				// does not contain overloads for moving faces or edges, you'll always need to feed it 
				// triangle data.
				selectedTriangles = pb_Face.AllTriangles(movingFaces);
				
				// Get the direction vector for the first face.  We could get the average of all selected
				// faces, but in this case it's safe to assume that the faces are all sharing a normal
				// (because we wrote it that way!).
				nrm = pb_Math.Normal(pb, movingFaces[0]);

				// Check that the selected face won't be extending into another pipe (or itself), and will remain within the bounds.
				invalidDirection = DetectCollision( pb_Math.Average(pb.VerticesInWorldSpace(selectedTriangles)), pb.transform.TransformDirection(nrm), extrudeDist );

				// Keep track of faces that failed collision detection (a collision was detected).
				if(invalidDirection)
					invalidFaces.Add(faceIndex);
			}

			// There's nowhere for this pipe to go.  Destroy it and tell the PipeSpawner
			// that it needs to create a new one.
			if(invalidFaces.Count >= faceCount)
			{
				EndPipe_Internal();
				return;
			}
		}

		// Extrude the movingFaces out a tiny distance to avoid popping graphics, but enough to allow 
		// normals and tangents to calculate properly (the Refresh() call does this for you).  The last
		// out param in Extrude is optional, and is populated with the newly created faces on each
		// perimeter edge of the extuded faces.
		pb.Extrude(movingFaces, .0001f, out neighborFaces);

		// Refresh the normals, tangents, and uvs.  In the Editor, you should also call pb.GenerateUV2(),
		// but since this is runtime we don't care about UV2 channels.
		pb.Refresh();

		 // Remove the currently moving faces from the connectedFaces array, since we don't want to go
		 // direction twice.
		neighborFaces.RemoveAll(x => System.Array.IndexOf(movingFaces, x) > -1);

		// Reset currentDistanceTraveled
		currentDistanceTraveled = .0001f;

		// If this is a turn, increment the turn count.
		if(isTurn) turnCount++;

		// Now toggle isTurn.
		isTurn = !isTurn;
	}

	/**
	 * Extruding can get expensive when an object has many faces.  This creates a new
	 * pb_Object pipe by detaching the currently moving faces into a new object, then
	 * sets that pipe in motion.
	 */
	void DetachChild()
	{
		// First order of business - stop extruding from this segment.
		Pause();

		// DetachFacesToObject can fail, so it returns a bool with the success status.
		// If it fails, end this pipe tree.  Otherwise, copy will be set to the new
		// pb_Object.
		pb_Object copy;

		if(DetachFacesToObject(pb, movingFaces, out copy))
		{
			// Huzzah!  DetachFacesToObject worked, and we now have 2 separate pb_Objects.
			// The first gets all the faces in movingFaces deleted, and the duplicate gets
			// all faces that *aren't* movingFaces deleted.

			child = copy.gameObject.AddComponent<Pipe>();
			child.gameObject.name = "ChildPipe: " + child.gameObject.GetInstanceID();
			
			// Let the child know who's boss.
			child.SetParent(this);

			// Aaand child inherits all the same paremeters that this branch has.
			child.SetSpeed(this.speed);
			child.SetSize(this.size);
			child.SetBounds(this.bounds);
			child.SetStretchRange(this.minimumStretchDistance, maximumStretchDistance);
			child.SetMaxTurns(this.maximumPipeTurns - turnCount);

			// Unlike the first segment, children should start with a turn.
			child.isTurn = true;

			// Now pass a reference to PipeSpawner's OnPipeFinished delegate to the child's OnPipeFinished event handler.
			child.OnPipeFinished += OnPipeFinished;
		}
		else
		{
			// Poop.  DetachFacesToObject failed.  Put this branch out of it's misery now.
			EndPipe_Internal();
		}
	}

	/**
	 * Deletes @faces from the passed pb_Object, and creates a new pb_Object using @faces.  On success,
	 * detachedObject will be set to the new pb_Object.
	 * 
	 * NOTE - As of 2.3, `DetachFacesToObject` was not publicly available in the pbMeshOps.  This method
	 * was made available in 2.3.1 as an extension method to pb_Object:
	 * `pbMeshOps::DetachFacesToObject(this pb_Object pb, pb_Face[] faces, out pb_Object detachedObject)`
	 */
	static bool DetachFacesToObject(pb_Object pb, pb_Face[] faces, out pb_Object detachedObject)
	{
		detachedObject = null;

		if(faces.Length < 1 || faces.Length == pb.faces.Length)
			return false;

		int[] primary = new int[faces.Length];
		for(int i = 0; i < primary.Length; i++)
			primary[i] = System.Array.IndexOf(pb.faces, faces[i]);
		
		int[] inverse = new int[pb.faces.Length - primary.Length];
		int n = 0;

		for(int i = 0; i < pb.faces.Length; i++)
			if(System.Array.IndexOf(primary, i) < 0)
				inverse[n++] = i;
				
		detachedObject = pb_Object.InitWithObject(pb);

		detachedObject.transform.position = pb.transform.position;
		detachedObject.transform.localScale = pb.transform.localScale;
		detachedObject.transform.localRotation = pb.transform.localRotation;

		pb.DeleteFaces(primary);
		detachedObject.DeleteFaces(inverse);

		pb.Refresh();
		detachedObject.Refresh();
	
		detachedObject.gameObject.name = pb.gameObject.name + "-detach";
		
		return true;
	}
#endregion

#region Collision Logic

	/**
	 * Given a point and direction in normal space, casts a ray checking for possible collision
	 * with either itself or the pipe's bounding box.
	 * @todo - Return the distance from position to collision point so that we can adjust the
	 * extrude distance.  This way we'd see a lot less failed collision detections.
	 */
	bool DetectCollision(Vector3 position, Vector3 direction, float distance)
	{
		distance += size;

		/**
		 * First check if the ending position will be inside the pipe's bounds.
		 */
		Vector3 end = position + direction * distance;
		
		if(!bounds.Contains(end))
		{
			Debug.DrawRay(position, direction * distance, new Color( Mathf.Abs(direction.x),  Mathf.Abs(direction.y),  Mathf.Abs(direction.z), 1f), 2f, true);
			return true;
		}

		Ray ray = new Ray(position, direction);
		RaycastHit hit;

		if(Physics.Raycast(ray, out hit, distance))
		{
			return true;
		}

		return false;
	}
#endregion

#region Cleanup

	/**
	 *	Begin fading out this object.  At the end of the fade, destroy this gameObject.
	 */
	public void FadeOut(float fadeTime, float delay, Material fadeMaterial)
	{
		if(parent != null)
			parent.FadeOut(fadeTime, delay, fadeMaterial);

		pb.SetFaceMaterial(pb.faces, fadeMaterial);

		this.enabled = true;

		StartCoroutine( Fade(fadeTime, delay) );
	}

	/**
	 *	Lerp the material's alpha channel down to 0f over @time.  Then destroy the object.
	 */
	IEnumerator Fade(float time, float delay)
	{
		yield return new WaitForSeconds(delay);
		
		// we *do* want to instance the material in this case.
		Material mat = GetComponent<MeshRenderer>().material;
		Color col = mat.color;

		float timer = 0f;
		while(timer < 1f)
		{
			timer += Time.deltaTime / time;

			col.a = Mathf.Lerp(1f, 0f, timer);

			mat.color = col;

			yield return null;
		}

		GameObject.Destroy(mat);
		GameObject.Destroy(gameObject);

		yield return null;
	}
#endregion
}

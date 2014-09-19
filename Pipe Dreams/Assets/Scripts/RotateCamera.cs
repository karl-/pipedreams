using UnityEngine;
using System.Collections;

/**
 *	Simple script to rotate the scene camera around the pipe bounds.
 */
[RequireComponent(typeof(Camera))]
public class RotateCamera : MonoBehaviour
{
	const float MIN_CAM_DISTANCE = 60f;
	const float MAX_CAM_DISTANCE = 200f;
	public float scrollModifier = 50f;

	public PipeSpawner pipeSpawner;
	public float distanceFromPivot = 80f;
	Vector3 target = Vector3.zero;

	public float orbitSpeed = 100f;
	public float idleSpeed = 5f;

	float sign = 1f;
	
	void Start()
	{
		if(pipeSpawner == null)
		{
			PipeSpawner[] spawners = (PipeSpawner[])FindObjectsOfType(typeof(PipeSpawner));
			if(spawners.Length > 0)
				pipeSpawner = spawners[0];
			else
				Debug.LogError("Please create a GameObject with a PipeSpawner component.");
		}
		target = pipeSpawner.transform.position;
	}

	Vector2 mouse = Vector2.zero;
	Vector3 eulerRotation = Vector3.zero;
	bool ignore = false;
	void LateUpdate()
	{
		eulerRotation = transform.localRotation.eulerAngles;

		// Toggle accepting mouse input because otherwise you can accidentally trigger camera movement
		// when the mouse is dragging the settings window.
		if(Input.GetMouseButtonDown(0))
		{
			Vector2 mpos = Input.mousePosition;
			mpos.y = Screen.height - mpos.y;
			if( pipeSpawner.SettingsWindowRect.Contains(mpos) )
				ignore = true;
		}

		if(Input.GetMouseButtonUp(0))
			ignore = false;

		if(Input.GetMouseButton(0) && !ignore)
		{
			mouse.x = Input.GetAxis("Mouse X");
			mouse.y = -Input.GetAxis("Mouse Y");

			eulerRotation.x += mouse.y * orbitSpeed * Time.deltaTime;
			eulerRotation.y += mouse.x * orbitSpeed * Time.deltaTime;
			eulerRotation.z = 0f;
			
			float x = eulerRotation.x > 180f ? -(360 - eulerRotation.x) : eulerRotation.x;

			eulerRotation.x = Mathf.Clamp(x, -30f, 30f);

			sign = mouse.x >= 0f ? 1f : -1f;
		}

		if(Input.GetAxis("Mouse ScrollWheel") != 0f)
		{
			distanceFromPivot -= Input.GetAxis("Mouse ScrollWheel") * (distanceFromPivot/MAX_CAM_DISTANCE) * scrollModifier;
			distanceFromPivot = Mathf.Clamp(distanceFromPivot, MIN_CAM_DISTANCE, MAX_CAM_DISTANCE);
		}

		if(!pipeSpawner.IsPaused())
			eulerRotation.y += sign * idleSpeed * Time.deltaTime;

		transform.localRotation = Quaternion.Euler( eulerRotation );

		transform.position = transform.localRotation * (Vector3.forward * -distanceFromPivot) + target;
	}
}

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using ProBuilder2.Common;

/**
 * A really simple FPS counter.
 */
public class FPSUpdate : MonoBehaviour 
{
	const int ROLLING_AVERAGE_SAMPLES = 30;
	float[] deltas = new float[ROLLING_AVERAGE_SAMPLES];
	float avg = 0f;
	int index = 0;

	Rect r;
	int WIDTH = 120;
	int PAD = 4;

	void OnEnable()
	{
		r = new Rect(Screen.width - WIDTH, PAD, WIDTH-PAD, 64);
	}

	void OnGUI()
	{
		GUI.Label(r, "fps (30f): " + ((1f/avg)*(60f/ROLLING_AVERAGE_SAMPLES)).ToString("F2") );	/// will be inaccurate for first 30 frames
	}

	// Update is called once per frame
	int c = 0;
	void Update ()
	{
		c++;
		
		avg *= (float)(c < ROLLING_AVERAGE_SAMPLES ? c : ROLLING_AVERAGE_SAMPLES);
		avg -= deltas[index];

		index = index >= ROLLING_AVERAGE_SAMPLES-1 ? 0 : index + 1;
		deltas[index] = Time.deltaTime;

		avg += deltas[index];
		avg /= (float)(c < ROLLING_AVERAGE_SAMPLES ? c : ROLLING_AVERAGE_SAMPLES);
	}
}

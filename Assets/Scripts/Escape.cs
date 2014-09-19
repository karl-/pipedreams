using UnityEngine;
using System.Collections;

public class Escape : MonoBehaviour
{
	void Update()
	{
		if(Input.GetKeyUp(KeyCode.Escape))
			Application.Quit();
	}
}

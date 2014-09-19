using UnityEngine;
using System.Collections;
using ProBuilder2.Common;
using ProBuilder2.MeshOperations;

public class Examples : MonoBehaviour
{
	void Start ()
	{
		pb_Object pb = GetComponent<pb_Object>();
		pb.Extrude( new pb_Face[] { pb.faces[0] }, 1f);
		pb.Refresh();
	}
}

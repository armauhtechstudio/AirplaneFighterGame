/// <summary>
/// Basic controller.
/// </summary>

using UnityEngine;
using System.Collections;

public class BasicController : MonoBehaviour {
	void Start () {
	
	}
	// Update is called once per frame
	void Update () {
		if(ControlFreak2.CF2Input.GetKey(KeyCode.W)){
			this.transform.position += new Vector3(0,0,1);
		}
		if(ControlFreak2.CF2Input.GetKey(KeyCode.A)){
			this.transform.position += new Vector3(1,0,0);
		}
		if(ControlFreak2.CF2Input.GetKey(KeyCode.S)){
			this.transform.position += new Vector3(0,0,-1);
		}
		if(ControlFreak2.CF2Input.GetKey(KeyCode.D)){
			this.transform.position += new Vector3(-1,0,0);
		}
		if(ControlFreak2.CF2Input.GetKey(KeyCode.Q)){
			this.transform.position += new Vector3(0,1,0);
		}
		if(ControlFreak2.CF2Input.GetKey(KeyCode.E)){
			this.transform.position += new Vector3(0,-1,0);
		}
	}
}

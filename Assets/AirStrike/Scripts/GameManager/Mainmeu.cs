using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class Mainmeu : MonoBehaviour {

	void Start () {
        MouseLock.MouseLocked = false;
    }
	
	void Update () {
	
	}
	
	public void PlayBtn()
	{
        SceneManager.LoadScene("Classic");
    }
	
}

using UnityEngine;
using System.Collections;

public class PlayerDead : FlightOnDead
{
	void Start (){}
	
	// if player dead 
	public override void OnDead (GameObject killer)
	{
		GameManager.instance.GameOver ();
		base.OnDead (killer);
	}
}

using UnityEngine;
using System.Collections;

public class WeaponController : MonoBehaviour
{
	public string[] TargetTag = new string[1]{"Enemy"};
	public WeaponLauncher[] WeaponLists;
	public int CurrentWeapon = 0;
	public bool ShowCrosshair;
	
	void Awake ()
	{
		// find all attached weapons.
		if (this.transform.GetComponentsInChildren (typeof(WeaponLauncher)).Length > 0) {
			var weas = this.transform.GetComponentsInChildren (typeof(WeaponLauncher));
			WeaponLists = new WeaponLauncher[weas.Length];
			for (int i=0; i<weas.Length; i++) {
				WeaponLists [i] = weas [i].GetComponent<WeaponLauncher> ();
				WeaponLists [i].TargetTag = TargetTag;
			}
		}
	}
	public WeaponLauncher GetCurrentWeapon(){
		if (CurrentWeapon < WeaponLists.Length && WeaponLists [CurrentWeapon] != null) {
			return WeaponLists [CurrentWeapon];
		}
		return null;
	}
	
	private void Start ()
	{
		for (int i=0; i<WeaponLists.Length; i++) {
			if (WeaponLists [i] != null) {
				WeaponLists [i].TargetTag = TargetTag;
				WeaponLists [i].ShowCrosshair = ShowCrosshair;
			}
		}
	}

	// Indices currently being fired by UI buttons this frame (cleared each frame by GameUI)
	private System.Collections.Generic.HashSet<int> activeButtonIndices = new System.Collections.Generic.HashSet<int>();

	private void Update ()
	{
		// Start by marking all weapons inactive
		for (int i = 0; i < WeaponLists.Length; i++) {
			if (WeaponLists [i] != null) {
				WeaponLists [i].OnActive = false;
			}
		}
		// Always keep the selected current weapon active (HUD / reload)
		if (CurrentWeapon < WeaponLists.Length && WeaponLists [CurrentWeapon] != null) {
			WeaponLists [CurrentWeapon].OnActive = true;
		}
		// Also keep any button-fired weapon active so its reload logic runs
		foreach (int idx in activeButtonIndices) {
			if (idx < WeaponLists.Length && WeaponLists [idx] != null) {
				WeaponLists [idx].OnActive = true;
			}
		}
		// Clear for next frame
		activeButtonIndices.Clear();
	}

	public void LaunchWeapon (int index)
	{
		CurrentWeapon = index;
		if (CurrentWeapon < WeaponLists.Length && WeaponLists [index] != null) {
			WeaponLists [index].Shoot ();
		}
	}

	/// <summary>
	/// Fires the weapon at the given index WITHOUT changing CurrentWeapon.
	/// Also marks the launcher OnActive this frame so reloading works correctly.
	/// </summary>
	public void FireWeaponAtIndex (int index)
	{
		if (index < WeaponLists.Length && WeaponLists [index] != null) {
			activeButtonIndices.Add(index);   // keep OnActive for reload
			WeaponLists [index].Shoot ();
		}
	}
	
	public void SwitchWeapon ()
	{
		CurrentWeapon += 1;
		if (CurrentWeapon >= WeaponLists.Length) {
			CurrentWeapon = 0;	
		}
	}
	
	public void LaunchWeapon ()
	{
		if (CurrentWeapon < WeaponLists.Length && WeaponLists [CurrentWeapon] != null) {
			WeaponLists [CurrentWeapon].Shoot ();
		}
	}
}

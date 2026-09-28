using UnityEngine;
using SickscoreGames.HUDNavigationSystem;

// Makes a house built from many DamageManager pieces count as ONE mission target.
// Damage dealt to any piece is added up here; when HP runs out the house scores once,
// its HUD marker is removed and its loose pieces collapse.
public class HouseTarget : MonoBehaviour
{
    public string objName = "house";
    [Tooltip("Total damage (summed over all pieces) before the house counts as destroyed.")]
    public int HP = 400;
    public GameObject destroyedEffect;

    bool destroyed = false;
    public bool IsDestroyed => destroyed;

    // A house has ~180 pieces, each with an idle kinematic Rigidbody that costs physics time every step.
    // They're only needed for the collapse, so remove them now and add them back in MarkDestroyed.
    void Awake()
    {
        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>(true))
            if (rb.isKinematic) Destroy(rb);
    }

    /// <summary>
    /// Player weapon hit on any piece of a house (whatever the piece's tag): damages that house.
    /// Returns the house, or null if the collider isn't part of one.
    /// </summary>
    public static HouseTarget HitPiece(Collider piece, int damage)
    {
        HouseTarget house = piece != null ? piece.GetComponentInParent<HouseTarget>() : null;
        if (house != null) house.ApplyDamage(damage);
        return house;
    }

    /// <summary>
    /// Explosion over many colliders: each house inside is damaged ONCE, not once per piece
    /// (a house has ~180 pieces, so per-piece damage made one rocket flatten it).
    /// </summary>
    public static void HitArea(Collider[] hits, int damage)
    {
        var houses = new System.Collections.Generic.HashSet<HouseTarget>();
        foreach (Collider hit in hits)
        {
            HouseTarget house = hit != null ? hit.GetComponentInParent<HouseTarget>() : null;
            if (house != null) houses.Add(house);
        }
        foreach (HouseTarget house in houses) house.ApplyDamage(damage);
    }

    public void ApplyDamage(int damage)
    {
        if (destroyed) return;
        HP -= damage;
        if (HP <= 0) MarkDestroyed();
    }

    public void MarkDestroyed()
    {
        if (destroyed) return;
        destroyed = true;

        if (GameManagerMode2.instance != null)
            GameManagerMode2.instance.AddScore(250, objName);
        else if (GameManager.instance != null)
            GameManager.instance.AddScore(250, objName);

        HUDNavigationElement marker = GetComponent<HUDNavigationElement>();
        if (marker != null) marker.enabled = false;

        if (destroyedEffect)
            Instantiate(destroyedEffect, transform.position, transform.rotation);

        // Collapse: give the pieces physics again (removed in Awake to keep the idle house cheap)
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            if (col.isTrigger || col.gameObject == gameObject) continue;
            if (col is MeshCollider mesh && !mesh.convex) // can't be a moving body: just clear it with the rest
            {
                Destroy(col.gameObject, 10f);
                continue;
            }

            Rigidbody rb = col.attachedRigidbody != null ? col.attachedRigidbody : col.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            Destroy(rb.gameObject, 10f);
        }
    }
}

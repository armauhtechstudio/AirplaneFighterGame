using UnityEngine;
using UnityEngine.UI;

public class DamageManager : MonoBehaviour
{
    public AudioClip[] HitSound;
    public GameObject Effect;
    public int HP = 100;
    private int HPmax;
    public ParticleSystem OnFireParticle;
    public Slider healthSlider;
    public bool isPlayer=false;
    public string objName;

    // Set when this is a piece of a house target: the house does the scoring, not each piece
    private HouseTarget house;

    private void Start()
    {
        HPmax = HP;
        house = GetComponentInParent<HouseTarget>();
        if (OnFireParticle)
        {
            OnFireParticle.Stop();
        }

        // Initialize the slider value
        UpdateHealthSlider();
    }

    // Only add score when the player (bullets, rockets, etc.) dealt the damage, not environment/collision
    private static bool IsPlayerDamage(DamagePackage dm)
    {
        return dm.Owner != null && dm.Owner.GetComponent<PlayerManager>() != null;
    }

    // Damage function
    public void ApplyDamage(DamagePackage dm)
    {
        if (isDead) return; // e.g. the player's wreck waiting for a revive
        bool byPlayer = IsPlayerDamage(dm);
        if (byPlayer && house != null) house.ApplyDamage(dm.Damage);

        bool died = TakeDamage(dm.Damage);
        if (died)
        {
            if (byPlayer)
            {
                AddScoreToActiveManager();
            }
            this.gameObject.SendMessage("OnDead", dm.Owner, SendMessageOptions.DontRequireReceiver);
            Dead();
        }
    }

    // Used by weapon systems (e.g. AirplaneControllerwithShooting's BulletScript) that don't carry an
    // Owner/PlayerManager reference and just want to damage whatever they hit, no ownership check.
    public void ApplyDirectDamage(int damage) => ApplyDirectDamage(damage, true);

    // forwardToHouse false: the caller damages the house itself (explosions hit a house once, not per piece)
    public void ApplyDirectDamage(int damage, bool forwardToHouse)
    {
        if (isDead) return;
        if (forwardToHouse && house != null) house.ApplyDamage(damage);

        bool died = TakeDamage(damage);
        if (died)
        {
            AddScoreToActiveManager();
            Dead();
        }
    }

    void AddScoreToActiveManager()
    {
        if (house != null)
        {
            // A single piece breaking doesn't destroy the house, unless it's the house root itself
            if (house.gameObject == gameObject) house.MarkDestroyed();
            return;
        }

        // GameManagerMode2 is a standalone MonoBehaviour (not a GameManager subclass), so it needs
        // its own instance check alongside GameManager's.
        if (GameManagerMode2.instance != null)
            GameManagerMode2.instance.AddScore(250, objName);
        else if (GameManager.instance != null)
            GameManager.instance.AddScore(250, objName);
    }

    bool TakeDamage(int damage)
    {
        if (HP < 0)
            return false;
        if (HitSound.Length > 0)
        {
            AudioSource.PlayClipAtPoint(HitSound[Random.Range(0, HitSound.Length)], transform.position);
        }
        HP -= damage;

        // Update the slider value
        UpdateHealthSlider();

        if (OnFireParticle)
        {
            if (HP < (int)(HPmax / 2.0f))
            {
                OnFireParticle.Play();
            }
        }
        return HP <= 0;
    }
    bool isDead = false;
    private void Dead()
    {
        if(!isDead)
        {
            isDead = true;
            if (Effect)
            {
                GameObject obj = (GameObject)GameObject.Instantiate(Effect, transform.position, transform.rotation);
                if (this.GetComponent<Rigidbody>())
                {
                    if (obj.GetComponent<Rigidbody>())
                    {
                        obj.GetComponent<Rigidbody>().velocity = this.GetComponent<Rigidbody>().velocity;
                        obj.GetComponent<Rigidbody>().AddTorque(Random.rotation.eulerAngles * Random.Range(100, 2000));
                    }
                }
            }
            // Classic: the player may be revived (rewarded ad), so keep the plane as a hidden wreck
            if (isPlayer && GameManager.instance != null && GameManager.instance.HoldingPlayerForRevive)
            {
                HideWreck();
                return;
            }
            Destroy(this.gameObject);
        }

    }

    public bool IsDead => isDead;

    // What HideWreck switched off, so Revive can switch exactly that back on
    Renderer[] hiddenRenderers;
    Collider[] hiddenColliders;
    Behaviour[] pausedControls;
    bool wreckWasKinematic;

    void HideWreck()
    {
        hiddenRenderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(), r => r.enabled);
        foreach (Renderer r in hiddenRenderers) r.enabled = false;
        hiddenColliders = System.Array.FindAll(GetComponentsInChildren<Collider>(), c => c.enabled);
        foreach (Collider c in hiddenColliders) c.enabled = false;

        // No flying / shooting while dead
        pausedControls = System.Array.FindAll(new Behaviour[] { GetComponent<FlightSystem>(), GetComponent<PlayerController>() }, b => b != null && b.enabled);
        foreach (Behaviour b in pausedControls) b.enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            wreckWasKinematic = rb.isKinematic;
            rb.isKinematic = true;
        }
        if (OnFireParticle) OnFireParticle.Stop();
    }

    /// <summary>Brings a wreck kept by HideWreck back to life with full health.</summary>
    public void Revive()
    {
        if (!isDead) return;
        isDead = false;
        HP = HPmax;

        if (hiddenRenderers != null) foreach (Renderer r in hiddenRenderers) if (r) r.enabled = true;
        if (hiddenColliders != null) foreach (Collider c in hiddenColliders) if (c) c.enabled = true;
        if (pausedControls != null) foreach (Behaviour b in pausedControls) if (b) b.enabled = true;
        hiddenRenderers = null;
        hiddenColliders = null;
        pausedControls = null;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = wreckWasKinematic;
        if (OnFireParticle) OnFireParticle.Stop();

        UpdateHealthSlider();
    }

    // Update the health slider value based on the current health
    private void UpdateHealthSlider()
    {
        if (healthSlider)
        {
            // Ensure that HP is within the valid range (0 to HPmax)
            HP = Mathf.Clamp(HP, 0, HPmax);
            // Score for enemy kills is added only in ApplyDamage when killed by player
            // Calculate the health percentage and update the slider value
            float healthPercentage = (float)HP / HPmax;
            healthSlider.value = healthPercentage;
        }
        if (isPlayer)
        {
            HP = Mathf.Clamp(HP, 0, HPmax);

            // Calculate the health percentage and update the slider value
            float healthPercentage = (float)HP / HPmax;
            GameUI.instance.slider.value = healthPercentage;
        }
    }
}

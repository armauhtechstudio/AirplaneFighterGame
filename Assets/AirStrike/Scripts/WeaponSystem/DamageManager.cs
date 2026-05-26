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

    private void Start()
    {
        HPmax = HP;
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
        if (HP < 0)
            return;
        if (HitSound.Length > 0)
        {
            AudioSource.PlayClipAtPoint(HitSound[Random.Range(0, HitSound.Length)], transform.position);
        }
        HP -= dm.Damage;

        // Update the slider value
        UpdateHealthSlider();

        if (OnFireParticle)
        {
            if (HP < (int)(HPmax / 2.0f))
            {
                OnFireParticle.Play();
            }
        }
        if (HP <= 0)
        {
            if (IsPlayerDamage(dm) && GameManager.instance != null)
                GameManager.instance.AddScore(250, objName);
            this.gameObject.SendMessage("OnDead", dm.Owner, SendMessageOptions.DontRequireReceiver);
            Dead();
            
        }
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
            Destroy(this.gameObject);
        }
        
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

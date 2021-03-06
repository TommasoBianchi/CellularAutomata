﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Player3D : MonoBehaviour
{

    #region variables
    [Range(1, 1000)]
    public int maxHealth;
    public GameObject bloodImage;
    
    int currentHealth;
    public int CurrentHealth
    {
        get { return currentHealth; }
        set
        {
            if (value > maxHealth)
                currentHealth = maxHealth;
            else if (value < 0)
                currentHealth = 0;
            else
                currentHealth = value;

            healthText.text = "Health: " + currentHealth;
        }
    }
    Transform HUD;
    Text healthText;
    Animator animator;
    #endregion

    void Start () {        
        HUD = GameObject.FindGameObjectWithTag("HUD").transform;
        healthText = HUD.FindChild("HealthPanel").GetChild(0).GetComponent<Text>();
        animator = GetComponent<Animator>();

        CurrentHealth = maxHealth;
    }

    void Update()
    {
        if (currentHealth <= 0)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        if (Input.GetMouseButtonDown(0))
        {
            animator.Play("Attack");
        }
    }
	
	public void Hit (int damage) {
        CurrentHealth -= damage;

        Vector3 randomPosition = new Vector3(Random.Range(0, Screen.width), Random.Range(0, Screen.height), 0);
        float randomRotation = Random.Range(0, 360f);
        RectTransform blood = (Instantiate(bloodImage) as GameObject).GetComponent<RectTransform>();
        blood.transform.SetParent(HUD);
        blood.position = randomPosition;
        blood.rotation = Quaternion.Euler(0, 0, randomRotation);
        blood.GetComponent<BloodAlpha>().SetFade(((float) maxHealth - CurrentHealth) / maxHealth);
	}

    public bool IsAttacking()
    {
        return animator.GetCurrentAnimatorStateInfo(0).IsName("Attack");
    }
}

﻿

using System;
using TouchScript.Gestures;
using UnityEngine;
using Random = UnityEngine.Random;


/// <exclude />
public class DropCellScript : MonoBehaviour
{
    public bool contact_on = false;
    Collider2D player_coll;

    public int dropTapCounter = 0;
    public int pelletCounter = 0;

    private void Start()
    {

    }

    private void OnEnable()
    {
        GetComponent<TapGesture>().Tapped += tappedHandler2;
    }

    private void OnDisable()
    {
        GetComponent<TapGesture>().Tapped -= tappedHandler2;
    }


    private void OnCollisionEnter2D(Collision2D collision)
    {
        player_coll = collision.collider;

        contact_on = true;

    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        contact_on = true;
        player_coll = other;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        contact_on = false;
        dropTapCounter = 0;
    }

    //this happens when the pellet is tapped
    private void tappedHandler2(object sender, EventArgs eventArgs)
    {

        if (contact_on)
        {
            if (player_coll.transform.childCount != 0)
            {

                Debug.Log(player_coll.transform.GetChild(0).tag);

                dropTapCounter++;

                if(dropTapCounter == 3)
                {
                    float offset = 5.4f / 10;

                    if (pelletCounter == 0)
                        offset *= -1;

                    GameObject pellet_ = player_coll.transform.GetChild(0).gameObject;

                    pellet_.GetComponent<PelletScript>().saveToBuffer("DR_DROP");

                    pellet_.GetComponent<SpriteRenderer>().color = new Color(1.0f, 0.5f, 0.0f);
                    pellet_.transform.SetParent(null);
                    pellet_.transform.position = transform.position + new Vector3(offset,0,0);
                    pellet_.GetComponent<Collider2D>().enabled = true;
                    pellet_.GetComponent<Rigidbody2D>().isKinematic = true;
                    player_coll.GetComponent<BeeTap>().grabbed_on = false;

                    pelletCounter++;

                    if (player_coll.tag.Equals("bee_free"))
                    {
                        player_coll.GetComponent<SpriteRenderer>().color = Color.red;
                    }
                    else
                    {
                        player_coll.GetComponent<SpriteRenderer>().color = Color.blue;
                    }

                    dropTapCounter = 0;

                    Camera.main.GetComponent<Exit_app_script>().addPellet(pellet_);

                }

                
            }
        }

    }
}

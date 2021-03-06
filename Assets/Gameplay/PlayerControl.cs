﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerControl : MonoBehaviour
{
    public float charSize; //the height of the main character, in world units
    [ReadOnly] public Rect collider_bounds;

    //player state machine related 
    [ReadOnly] public bool in_climb;
    private bool light_toggle;
    private float climb_extent; //the initial height difference when initiating a climb

    // other flags
    [ReadOnly] public Vector3 spawn_root; //.x < 0 means there is no root assigned

    //movement related
    public float climb_speed, accel, x_vel_max;
    [ReadOnly] public Vector3 destination;
    [ReadOnly] public Vector3 destination_override;
    [ReadOnly] public Vector3 relation_to_destination; //negative or positive; 
                                                       //sign change means the player has either 
                                                       //arrived or rushed pass the destination
    private Vector3 relation_temp;
    private ContactPoint2D[] cp;

    //connect to other game components
    private CameraControler cControler;
    private ReadingManager rManager;
    private SpriteRenderer renderer_; //the sprite renderer assigned to the main character
    private Rigidbody2D rigid;

    private Animator animator_head;
    private Animator animator_torso;

    private BoxCollider2D box;

    private HeadLightControl head_light_controller;

    [ReadOnly] public List<Word> word_blocks_in_contact;
    [ReadOnly] public string word_blocks_in_contact_str;

    private float stuck_time = 0.0f; //to deal with really weird situations

    void Awake()
    {
        destination = new Vector3(-1, 0, 0);
        destination_override = new Vector3(-1, 0, 0);
        word_blocks_in_contact = new List<Word>();
    }

    // Start is called before the first frame update
    void Start()
    {
        //register events
        EventManager.Instance.OnCorrectKeyPressed += CorrectKeyPressed;
        EventManager.Instance.OnIncorrectKeyPressed += IncorrectKeyPressed;
        EventManager.Instance.OnCharacterDeleted += OnCharacterDeleted;

        //connect to rest of the game
        rigid = GetComponent<Rigidbody2D>();

        animator_head = GetComponent<Animator>();
        animator_torso = transform.GetChild(0).gameObject.GetComponent<Animator>();

        head_light_controller = transform.GetChild(1).gameObject.GetComponent<HeadLightControl>();

        cControler = GameObject.FindGameObjectWithTag("General Manager").GetComponent<CameraControler>();
        rManager = GameObject.FindGameObjectWithTag("General Manager").GetComponent<ReadingManager>();

        renderer_ = GetComponent<SpriteRenderer>();
        box = GetComponent<BoxCollider2D>();

        //set character state
        in_climb = false;
        light_toggle = false;

        //set coordinate related fields
        transform.localScale = new Vector3(charSize, charSize, charSize);
        UpdateRelativePosition();
        relation_temp = new Vector3(
            relation_to_destination.x,
            relation_to_destination.y,
            relation_to_destination.z
            );
        
        collider_bounds = new Rect(
            box.bounds.min,
            box.bounds.size
            );

        renderer_.enabled = false;
    }

    public void SpawnAtRoot()
    {
        Debug.Log("spawned");

        transform.position = new Vector3(
            spawn_root.x,
            spawn_root.y + charSize / 2f,
            0
            );

        renderer_.enabled = true;
    }

    // Update is called once per frame
    void Update()
    {
        bool accelerating = false;
        float hor_spd_temp = rigid.velocity.x;

        if (!renderer_.enabled)
        {
            if (spawn_root != null)
            {
                SpawnAtRoot();
            }
            return;
        }

        //basic variables for the rest of the method
        collider_bounds = new Rect(
            box.bounds.min,
            box.bounds.size
            );
        //Debug.Log(collider_bounds.yMin + " to " + collider_bounds.yMax);

        relation_temp = new Vector3(
            relation_to_destination.x,
            relation_to_destination.y,
            relation_to_destination.z);

        UpdateRelativePosition();


        word_blocks_in_contact_str = "";
        for(int i = 0; i < word_blocks_in_contact.Count; i++)
        {
            word_blocks_in_contact_str += i + ": " + word_blocks_in_contact[i].content + " ";
        }

        //control the motion of the player:

        //freeze the character if it is not inside camera range
        if (transform.position.x < cControler.CAM.xMin
            || transform.position.x > cControler.CAM.xMax)
        {
            //Debug.Log("outside camera scope");
            rigid.velocity = Vector2.zero;
            return;
        }

        if (!in_climb)
        {
            if (!Mathf.Approximately(relation_to_destination.x, 0))
            {
                float x_vel = rigid.velocity.x;

                //stopping distance under constant acceleration
                float stopping_distance_x = rigid.velocity.x * rigid.velocity.x / 2 / accel;

                //change of destination by external scripts
                bool new_order = destination_override.x >= 0;
                if (new_order)
                {
                    destination = destination_override;
                    destination_override = new Vector3(-1, 0, 0);
                }

                //decide first if should decelerate or accelerate
                bool should_decel =
                    //the player is going in the right direction
                    Mathf.Sign(rigid.velocity.x) != Mathf.Sign(relation_to_destination.x)
                    //and the destination is within stopping distance
                    && Mathf.Abs(relation_to_destination.x) <= stopping_distance_x;

                if (should_decel)
                {

                    //plain decel
                    //Debug.Log("decelerating");
                    float original_sign = Mathf.Sign(x_vel);
                    x_vel -= Mathf.Sign(relation_to_destination.x) * -1 * accel * Time.deltaTime;
                    //prevent over-decelerating
                    if (original_sign != Mathf.Sign(x_vel))
                    {
                        x_vel = 0;
                    }
                }
                else
                {
                    //accelerate accordingly
                    float dvdt = Mathf.Sign(relation_to_destination.x) * -1 * accel * Time.deltaTime;
                    //Debug.Log("accelerating " + dvdt);
                    accelerating = true;
                    x_vel += dvdt;

                    //clamp to maximum velocity
                    x_vel = Mathf.Min(Mathf.Abs(x_vel), x_vel_max) * Mathf.Sign(x_vel);
                }

                rigid.velocity = new Vector2(x_vel, rigid.velocity.y);

                float yMax = transform.position.y - charSize / 2f;
                for(int i = 0; i < word_blocks_in_contact.Count; i++)
                {
                    if (word_blocks_in_contact[i].top > yMax)
                    {
                        yMax = word_blocks_in_contact[i].top + charSize / 2f + 0.1f;
                        in_climb = true;
                    }
                }

                if (in_climb)
                {
                    destination.y = yMax;
                    climb_extent = yMax - transform.position.y;
                }
                else
                {
                    destination.y = transform.position.y;
                }
            }
        }
        else
        {
            //while climbing:

            //set horizontal velocity to 0
            rigid.velocity = new Vector2(0, climb_speed);

            //stop climbing when destination is reached
            if (relation_to_destination.y >= 0) {

                in_climb = false;
                climb_extent = 0;
                rigid.velocity = new Vector2(0, 0);
            }

        }

        if (accelerating && 
            (relation_temp.x == relation_to_destination.x 
            || Mathf.Approximately(hor_spd_temp, 0)))
        {
            stuck_time += Time.deltaTime;
            if(stuck_time > 0.5f)
            {
                in_climb = true;
                destination.y = rigid.position.y + 0.1f;
                Debug.Log("glitch jumped... couldn't do anything else");
                //TODO: do a special glitch jump animation :)
                //animator.SetBool("glitch_jump", true);

                //rigid.MovePosition(new Vector2(
                    //rigid.position.x,
                    //rigid.position.y + 0.1f));
            }
        }
        else
        {
            stuck_time = 0.0f;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("light on");
            light_toggle = true;
        }
        if (Input.GetKeyUp(KeyCode.Space))
        {
            Debug.Log("light off");
            light_toggle = false;
        }

        animator_head.SetFloat("speed", Mathf.Abs(rigid.velocity.x));
        animator_head.SetBool("in_climb", in_climb);
        animator_head.SetFloat("climb_extent", climb_extent);
        animator_head.SetBool("light_toggle", light_toggle);

        animator_torso.SetFloat("speed", Mathf.Abs(rigid.velocity.x));
        animator_torso.SetBool("in_climb", in_climb);
        animator_torso.SetFloat("climb_extent", climb_extent);
        animator_torso.SetBool("light_toggle", light_toggle);

        head_light_controller.light_ = light_toggle;
        head_light_controller.direction = !renderer_.flipX;
    }


    //update the stored relative position of the player to the cursor
    private void UpdateRelativePosition() {
        relation_to_destination = transform.position - destination;
        relation_to_destination.x = Mathf.Approximately(relation_to_destination.x, 0) ? 0 : relation_to_destination.x;
        relation_to_destination.y = Mathf.Approximately(relation_to_destination.y, 0) ? 0 : relation_to_destination.y;
        relation_to_destination.z = Mathf.Approximately(relation_to_destination.z, 0) ? 0 : relation_to_destination.z;
    }

    //handle collisions
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Word Block"))
        {
            //Debug.Log(collision.gameObject.GetComponent<WordBlockBehavior>().content.content);
            word_blocks_in_contact.Add(collision.gameObject.GetComponent<WordBlockBehavior>().content);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Word Block"))
        {
            word_blocks_in_contact.Remove(collision.gameObject.GetComponent<WordBlockBehavior>().content);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Cover Object"))
        {
            Debug.Log("coming into contact with cover object");
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Cover Object"))
        {
            Debug.Log("exiting contact with cover object");
        }
    }

    private void OnCharacterDeleted()
    {
        renderer_.flipX = true;
    }

    private void CorrectKeyPressed()
    {
        renderer_.flipX = false;
        //Debug.Log("correct!");
    }
    private void IncorrectKeyPressed()
    {
        //Debug.Log("incorrect!");
    }
}

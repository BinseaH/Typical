using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortalManager : MonoBehaviour
{
    public GameObject portal_prefab;
    public RuntimeAnimatorController portal_animator;

    public static PortalManager instance;

    public float margin;

    public List<PortalData> destinations;

    //portal manager is always initialized to the current portal manager in the scene
    private void Awake()

    {
        instance = this;
    }

    void Start()
    {
        EventManager.instance.OnPortalOpen += OnPortalOpen;
        EventManager.instance.OnPortalClose += OnPortalClose;

        //fetch destinations from current scene data
    }

    //open portals according to script and location
    //beginning marks the left middle position of the collection of portal blocks
    private void OnPortalOpen(Vector2 beginning)
    {
        if (destinations == null || destinations.Count == 0)
        {
            Debug.LogError("no destination specified, skipping portal opening procedure");
            return;
        }

        transform.position = new Vector3(beginning.x, beginning.y, 0);
        Vector2 s = portal_prefab.GetComponent<SpriteRenderer>().size;
        float block_raw_height = s.y;
        float block_whole_height = s.y + 2 * margin;

        for (int i = 0; i < destinations.Count; i++)
        {
            float portional_h = i - destinations.Count / 2f + 0.5f;
            GameObject go = GameObject.Instantiate(
                portal_prefab,
                new Vector3(
                    transform.position.x + 3,
                    transform.position.y + block_raw_height / 2 
                        + portional_h * block_whole_height,
                    transform.position.z
                    ),
                Quaternion.identity,
                transform);

            //TODO: set portal data
            Portal p_ = go.GetComponent<Portal>();
            p_.data = destinations[i];
        }
    }

    //close all portals opened
    private void OnPortalClose()
    {

    }

    //TODO: implement this and link to portal prefab script
    private void SceneTransition(string destination)
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

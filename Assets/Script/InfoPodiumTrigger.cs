using UnityEngine;

public class InfoPodiumTrigger : MonoBehaviour
{
    [Header("Keo Canvas hoac Text vao day")]
    public GameObject infoDisplay;

    void Start()
    {
        if (infoDisplay != null)
        {
            infoDisplay.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            if (infoDisplay != null)
            {
                infoDisplay.SetActive(true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            if (infoDisplay != null)
            {
                infoDisplay.SetActive(false);
            }
        }
    }
}
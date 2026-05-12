using UnityEngine;

public class NetworkManagerDontDestroy : MonoBehaviour
{
    private static NetworkManagerDontDestroy instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
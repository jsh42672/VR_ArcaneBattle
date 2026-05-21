using UnityEngine;

public class PortalManager : MonoBehaviour
{
    private static PortalManager instance;
    
    public static PortalManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("PortalManager");
                instance = obj.AddComponent<PortalManager>();
                DontDestroyOnLoad(obj);
            }
            return instance;
        }
    }
    
    private string currentPortalID;
    
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    public void SavePortalEntry(string portalID)
    {
        currentPortalID = portalID;
        PlayerPrefs.SetString("LastPortalID", portalID);
    }
    
    public string GetLastPortalID()
    {
        return PlayerPrefs.GetString("LastPortalID", "");
    }
}

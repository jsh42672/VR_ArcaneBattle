using UnityEngine;

public class PortalManager : MonoBehaviour
{
    private const string LastPortalKey = "LastPortalID";
    private const string ReturnWorldSceneKey = "ReturnWorldScene";
    private const string DefaultWorldSceneName = "World_main";

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
        PlayerPrefs.SetString(LastPortalKey, portalID);
    }

    public void SavePortalEntry(string portalID, string worldSceneName)
    {
        SavePortalEntry(portalID);

        if (!string.IsNullOrWhiteSpace(worldSceneName))
            PlayerPrefs.SetString(ReturnWorldSceneKey, worldSceneName);
    }
    
    public string GetLastPortalID()
    {
        return PlayerPrefs.GetString(LastPortalKey, "");
    }

    public string GetReturnWorldScene()
    {
        var sceneName = PlayerPrefs.GetString(ReturnWorldSceneKey, DefaultWorldSceneName);
        return sceneName == "World" ? DefaultWorldSceneName : sceneName;
    }
}

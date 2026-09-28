using Firebase.Extensions;
using Firebase.RemoteConfig;
using System;
using System.Collections;
using UnityEngine;
//using ByteBrewSDK;
using System.Threading.Tasks;

public class FirebaseRemoteConfigManager : MonoBehaviour
{
    public static FirebaseRemoteConfigManager Instance;

    /// <summary>Raised on the main thread once the fetched config has been applied (AdsManager listens).</summary>
    public static event Action<GameConfigData> OnConfigApplied;

    /// <summary>Current ads config: defaults until the fetch arrives, null if there's no manager.</summary>
    public static GameConfigData Config => Instance != null ? Instance.configData : null;

    [Header("Configuration")]
    public GameConfigData configData;
    public float delayRemote = 6f;

    [Header("Debugging")]
    public bool debugDefaultString;
    public bool debugChangeString;

    private FirebaseRemoteConfig firebaseRemoteConfig;
    private bool isFirebaseInitialized;
    private Task fetchTask;
    public string defaultValues;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {

        //#if UNITY_EDITOR
        //        Debug.unityLogger.logEnabled = true;
        //#else
        //        Debug.unityLogger.logEnabled = false;
        //#endif


        defaultValues = JsonUtility.ToJson(configData);
        Invoke(nameof(InitializeRemoteConfig), delayRemote);
    }

    private void InitializeRemoteConfig()
    {
        
        firebaseRemoteConfig = FirebaseRemoteConfig.DefaultInstance;
        firebaseRemoteConfig.SetConfigSettingsAsync(new ConfigSettings());

        firebaseRemoteConfig.ActivateAsync().ContinueWithOnMainThread(task =>
        {
            isFirebaseInitialized = true;
            FetchConfigData();
            if (debugDefaultString)
                Debug.LogError("Default Config: " + defaultValues);
        });
    }

    private void FetchConfigData()
    {
        fetchTask = firebaseRemoteConfig.FetchAsync(TimeSpan.Zero);
        StartCoroutine(FetchRemoteConfigData());
    }

    private IEnumerator FetchRemoteConfigData()
    {
        yield return new WaitUntil(() => fetchTask.IsCompleted);
        firebaseRemoteConfig.ActivateAsync().ContinueWithOnMainThread(task => ApplyChanges());
    }

    private void ApplyChanges()
    {
        if (!isFirebaseInitialized) return;

        string remoteConfigJson = firebaseRemoteConfig.GetValue("AduAdsString").StringValue;
        if (!string.IsNullOrEmpty(remoteConfigJson))
        {
            configData = JsonUtility.FromJson<GameConfigData>(remoteConfigJson);
            if (debugChangeString)
                Debug.LogError("Firebase Config: " + JsonUtility.ToJson(configData));
        }
        OnConfigApplied?.Invoke(configData);
      //  ByteBrew.InitializeByteBrew();
    }
}

[System.Serializable]
public class GameConfigData
{

    [Header("Ads Settings")]
    public bool AppOpenFromBackground = true;
    public bool TopBanner = true;
    public bool IsMedRect = true;
    public bool IsInterstialAd = true;

    public bool isInterMenu = true;
    public bool isInterMode = true;
    public bool isInterLevelSelect = true;

    public bool isInterRestart = true;
    public bool isInterHome = true;
    public bool isInterNext = true;

    // Show an app open ad right after an interstitial closes. Off by default: AdMob advises against
    // back-to-back full-screen ads, so switch it on from remote config only if you want it.
    public bool AppOpenAfterInterstitial = false;


}

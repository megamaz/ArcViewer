using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

public class UrlArgHandler : MonoBehaviour
{
    public const string ArcViewerName = "ArcViewer";
    public const string ArcViewerURL = "https://allpoland.github.io/ArcViewer/";
    public const string BeatLeaderViewerURL = "https://replay.beatleader.xyz/";

    [DllImport("__Internal")]
    public static extern string GetParameters();

    [DllImport("__Internal")]
    public static extern void SetPageTitle(string title);

    private static string _loadedMapID;
    public static string LoadedMapID
    {
        get => _loadedMapID;

        set
        {
            _loadedMapID = value;
            _loadedMapURL = null;
        }
    }

    private static string _loadedMapURL;
    public static string LoadedMapURL
    {
        get => _loadedMapURL;

        set
        {
            _loadedMapURL = value;
            _loadedMapID = null;
        }
    }

    private static string _loadedReplayID;
    public static string LoadedReplayID
    {
        get => _loadedReplayID;

        set
        {
            _loadedReplayID = value;
            _loadedReplayURL = null;
        }
    }

    private static string _loadedReplayURL;
    public static string LoadedReplayURL
    {
        get => _loadedReplayURL;

        set
        {
            _loadedReplayURL = value;
            _loadedReplayID = null;
        }
    }

    public static DifficultyCharacteristic? LoadedCharacteristic;
    public static DifficultyRank? LoadedDiffRank;
    public static bool ignoreMapForSharing;

    private static string mapID;
    private static string mapURL;
    private static string mapPath;
    private static string replayID;
    private static string replayURL;
    private static float startTime;
    private static DifficultyCharacteristic? mode;
    private static DifficultyRank? diffRank;
    private static bool noProxy;

    private static Dictionary<string, string> unknownArgs = new Dictionary<string, string>();

    [SerializeField] private MapLoader mapLoader;


    private static string AddUnknownParameters(Uri uri)
    {
        List<string> newArguments = new List<string>();
        foreach(KeyValuePair<string, string> arg in unknownArgs)
        {
            newArguments.Add($"{arg.Key}={arg.Value}");
        }

        string newQuery = string.Join('&', newArguments);
        string url = uri.OriginalString;

        NameValueCollection existingArgs = HttpUtility.ParseQueryString(uri.Query);
        if(existingArgs.AllKeys.Length == 0)
        {
            url += $"?{newQuery}";
        }
        else
        {
            url += $"&{newQuery}";
        }

        return url;
    }


    private void ParseParameter(string name, string value)
    {
        if(string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value))
        {
            return;
        }

        switch(name)
        {
            case "id":
                mapID = value;
                break;
            case "url":
                mapURL = value;
                break;
            case "scoreID":
                replayID = value;
                break;
            case "replayURL":
                replayURL = value;
                break;
            case "t":
                if(!float.TryParse(value, out startTime)) startTime = 0;
                break;
            case "mode":
                DifficultyCharacteristic parsedMode;
                mode = Enum.TryParse(value, true, out parsedMode) ? parsedMode : null;
                break;
            case "difficulty":
                DifficultyRank parsedRank;
                diffRank = Enum.TryParse(value, true, out parsedRank) ? parsedRank : null;
                break;
#if UNITY_WEBGL && !UNITY_EDITOR
            case "noProxy":
                noProxy = bool.TryParse(value, out noProxy) ? noProxy : false;
                break;
#else
            case "path":
                mapPath = value;
                break;
#endif
            default:
                //Unknown arguments are stored, since they're likely to be part of a download url
                unknownArgs.Add(name, value);
                break;
        }
    }


    private void ApplyArguments()
    {
        bool setTime = false;
        bool setDiff = false;

        if(!string.IsNullOrEmpty(replayID))
        {
            StartCoroutine(mapLoader.LoadReplayIDCoroutine(replayID, mapURL, mapID, noProxy));
            LoadedReplayID = replayID;
            
            //Don't set the diff cause that depends on the replay
            setTime = true;
        }
        else if(!string.IsNullOrEmpty(replayURL))
        {
            replayURL = AddUnknownParameters(new Uri(replayURL));

            StartCoroutine(mapLoader.LoadReplayURLCoroutine(replayURL, mapURL, mapID, noProxy));
            LoadedReplayURL = replayURL;

            setTime = true;
        }
        else if(!string.IsNullOrEmpty(mapID))
        {
            StartCoroutine(mapLoader.LoadMapIDCoroutine(mapID));
            LoadedMapID = mapID;

            setTime = true;
            setDiff = true;
        }
        else if(!string.IsNullOrEmpty(mapURL))
        {
            mapURL = AddUnknownParameters(new Uri(mapURL));

            StartCoroutine(mapLoader.LoadMapZipURLCoroutine(mapURL, null, null, noProxy));
            LoadedMapURL = mapURL;

            setTime = true;
            setDiff = true;
        }
#if !UNITY_WEBGL || UNITY_EDITOR
        else if(!string.IsNullOrEmpty(mapPath))
        {
            mapLoader.LoadMapDirectory(mapPath);
            setTime = true;
            setDiff = true;
        }
#endif

        //Only apply start time and diff when a map is also included in the arguments
        if(setTime && startTime > 0)
        {
            MapLoader.OnMapLoaded += SetTime;
        }

        if(setDiff && (mode != null || diffRank != null))
        {
            MapLoader.OnMapLoaded += SetDifficulty;
        }
    }


    public void LoadMapFromShareableURL(string url)
    {
        if(MapLoader.Loading)
        {
            Debug.LogWarning("Tried to load from url args while already loading!");
            return;
        }

        if(string.IsNullOrEmpty(url))
        {
            Debug.LogWarning("Shareable link is null or empty!");
            ErrorHandler.Instance.ShowPopup(ErrorType.Error, "Empty shareable link!");
            return;
        }

        ResetArguments();

        url = HttpUtility.UrlDecode(url);
        Uri uri = new Uri(url);

        NameValueCollection args = HttpUtility.ParseQueryString(uri.Query);
        if(args.AllKeys.Length == 0)
        {
            Debug.LogWarning($"Invalid sharing URL: {url}");
            ErrorHandler.Instance.ShowPopup(ErrorType.Error, "Invalid sharing URL!");
        }

        foreach(string name in args.AllKeys)
        {
            ParseParameter(name, args.Get(name));
        }

        ApplyArguments();
    }


    private void LoadMapFromCommandLineParameters(string[] parameters)
    {
        if(MapLoader.Loading)
        {
            Debug.LogWarning("Tried to load from command line args while already loading!");
            return;
        }

        ResetArguments();

        if(parameters.Length <= 1)
        {
            //The first parameter is always the app name, so it shouldn't be counted
            return;
        }

        for(int i = 1; i < parameters.Length; i++)
        {
            string[] args = parameters[i].Split('=');
            if(args.Length != 2)
            {
                //A parameter should always have a single `=`, leading to two args
                continue;
            }

            ParseParameter(args[0], args[1]);
        }

        ApplyArguments();
    }


    private void SetTime()
    {
        TimeManager.CurrentTime = startTime;
        MapLoader.OnMapLoaded -= SetTime;
    }


    public void SetDifficulty()
    {
        if(mode != null)
        {
            //Since mode is nullable I have to cast it (cringe)
            DifficultyCharacteristic characteristic = (DifficultyCharacteristic)mode;

            List<Difficulty> difficulties = BeatmapManager.GetDifficultiesByCharacteristic(characteristic);
            Difficulty difficulty = null;

            if(diffRank != null)
            {
                difficulty = difficulties.FirstOrDefault(x => x.difficultyRank == diffRank);
            }
            BeatmapManager.CurrentDifficulty = difficulty ?? difficulties.Last();
        }
        else if(diffRank != null)
        {
            DifficultyCharacteristic defaultCharacteristic = BeatmapManager.GetDefaultDifficulty().characteristic;
            List<Difficulty> difficulties = BeatmapManager.GetDifficultiesByCharacteristic(defaultCharacteristic);

            Difficulty difficulty = difficulties.FirstOrDefault(x => x.difficultyRank == diffRank);
            BeatmapManager.CurrentDifficulty = difficulty ?? difficulties.Last();
        }
        MapLoader.OnMapLoaded -= SetDifficulty;
    }


    public void ResetArguments()
    {
        mapID = "";
        mapURL = "";
        mapPath = "";
        startTime = 0;
        mode = null;
        diffRank = null;
        noProxy = false;

        unknownArgs = new Dictionary<string, string>();
    }


    public void ClearSubscriptions()
    {
        MapLoader.OnMapLoaded -= SetTime;
        MapLoader.OnMapLoaded -= SetDifficulty;
    }


    public void UpdateLoadedDifficulty(Difficulty newDifficulty)
    {
        Difficulty defaultDifficulty = BeatmapManager.GetDefaultDifficulty();
        if(newDifficulty == defaultDifficulty)
        {
            //No need to specify for the default difficulty
            LoadedCharacteristic = null;
            LoadedDiffRank = null;
            return;
        }

        LoadedCharacteristic = newDifficulty.characteristic;
        LoadedDiffRank = newDifficulty.difficultyRank;
    }


#if UNITY_WEBGL && !UNITY_EDITOR
    public void UpdateMapTitle(BeatmapInfo info)
    {
        string mapTitle = "";
        if(info != BeatmapInfo.Empty)
        {
            string authorName = info._songAuthorName;
            string songName = info._songName;
            if(!string.IsNullOrEmpty(authorName))
            {
                mapTitle += authorName;
                if(!string.IsNullOrEmpty(songName))
                {
                    //Add a separator when there's an author and and song name
                    //(This will be the case 99% of the time)
                    mapTitle += " - ";
                }
            }
            mapTitle += songName;

            if(!string.IsNullOrEmpty(mapTitle))
            {
                //Add a separator between the webpage title and map title
                mapTitle = " | " + mapTitle;
            }
        }

        SetPageTitle($"{ArcViewerName}{mapTitle}");
    }
#endif


    private void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string parameters = GetParameters();

        if(!string.IsNullOrEmpty(parameters))
        {
            LoadMapFromShareableURL(ArcViewerURL + parameters);
        }

        BeatmapManager.OnBeatmapInfoChanged += UpdateMapTitle;
#else
        try
        {
            LoadMapFromCommandLineParameters(Environment.GetCommandLineArgs());
        }
        catch(NotSupportedException)
        {
            Debug.LogWarning("The system doesn't support command-line arguments!");
        }
#endif
        MapLoader.OnLoadingFailed += ClearSubscriptions;
        BeatmapManager.OnBeatmapDifficultyChanged += UpdateLoadedDifficulty;
    }
}
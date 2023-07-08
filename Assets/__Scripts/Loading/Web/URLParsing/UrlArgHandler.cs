using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

public class UrlArgHandler : MonoBehaviour
{
    public const string ArcViewerName = "ArcViewer";
    public const string ArcViewerURL = "https://allpoland.github.io/ArcViewer/";

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
            _loadedReplayID = null;
            _loadedReplayURL = null;
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
            _loadedReplayID = null;
            _loadedReplayURL = null;
        }
    }

    private static string _loadedReplayID;
    public static string LoadedReplayID
    {
        get => _loadedReplayID;

        set
        {
            _loadedReplayID = value;
            _loadedMapID = null;
            _loadedMapURL = null;
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
            _loadedMapID = null;
            _loadedReplayID = null;
            _loadedMapURL = null;
        }
    }

    public static DifficultyCharacteristic? LoadedCharacteristic;
    public static DifficultyRank? LoadedDiffRank;

    private static string mapID;
    private static string mapURL;
    private static string mapPath;
    private static float startTime;
    private static DifficultyCharacteristic? mode;
    private static DifficultyRank? diffRank;
    private static bool noProxy;

    [SerializeField] private MapLoader mapLoader;


    private void ParseParameter(string parameter)
    {
        string[] args = parameter.Split('=');
        if(args.Length != 2)
        {
            //A parameter should always have a single `=`, leading to two args
            return;
        }

        string name = args[0];
        string value = args[1];
        switch(name)
        {
            case "id":
                mapID = value;
                break;
            case "url":
                mapURL = value;
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
        }
    }


    private void ApplyArguments()
    {
        bool autoLoad = false;
        if(!string.IsNullOrEmpty(mapID))
        {
            StartCoroutine(mapLoader.LoadMapIDCoroutine(mapID));
            LoadedMapID = mapID;

            autoLoad = true;
        }
        else if(!string.IsNullOrEmpty(mapURL))
        {
            StartCoroutine(mapLoader.LoadMapZipURLCoroutine(mapURL, null, noProxy));
            LoadedMapURL = mapURL;

            autoLoad = true;
        }
#if !UNITY_WEBGL || UNITY_EDITOR
        else if(!string.IsNullOrEmpty(mapPath))
        {
            mapLoader.LoadMapDirectory(mapPath);
            autoLoad = true;
        }
#endif

        if(autoLoad)
        {
            //Only apply start time and diff when a map is also included in the arguments
            if(startTime > 0)
            {
                MapLoader.OnMapLoaded += SetTime;
            }

            if(mode != null || diffRank != null)
            {
                MapLoader.OnMapLoaded += SetDifficulty;
            }
        }
    }


    public void LoadMapFromURLParameters(string parameters)
    {
        ResetArguments();

        if(MapLoader.Loading)
        {
            return;
        }

        //URL arguments start with a `?`
        parameters = parameters.TrimStart('?');

        string[] args = parameters.Split('&');
        if(args.Length <= 0) return;

        for(int i = 0; i < args.Length; i++)
        {
            ParseParameter(args[i]);
        }

        ApplyArguments();
    }


    public void LoadMapFromCommandLineParameters(string[] parameters)
    {
        ResetArguments();

        if(MapLoader.Loading)
        {
            return;
        }

        if(parameters.Length <= 1)
        {
            //The first parameter is always the app name, so it shouldn't be counted
            return;
        }

        for(int i = 1; i < parameters.Length; i++)
        {
            ParseParameter(parameters[i]);
        }

        ApplyArguments();
    }


    public void SetTime()
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
            LoadMapFromURLParameters(parameters);
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
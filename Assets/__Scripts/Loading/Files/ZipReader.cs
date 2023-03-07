using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEngine;

public class ZipReader
{
    public static async Task<(BeatmapInfo, List<Difficulty>, MemoryStream, byte[])> GetMapFromZipArchiveAsync(ZipArchive archive)
    {
        BeatmapInfo info = null;
        List<Difficulty> difficulties = new List<Difficulty>();
        MemoryStream song = null;
        byte[] coverImageData = new byte[0];

        Stream infoData = null;
        MapLoader.LoadingMessage = "Loading Info.dat";
        await Task.Yield();

        foreach(ZipArchiveEntry entry in archive.Entries)
        {
            if(entry.Name.Equals("Info.dat", System.StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    infoData = entry.Open();
                }
                catch(Exception e)
                {
                    Debug.LogWarning($"Failed to read Info.dat with error: {e.Message}, {e.StackTrace}");
                    infoData = null;
                    break;
                }

                if(!entry.FullName.Equals("Info.dat", System.StringComparison.OrdinalIgnoreCase))
                {
                    //This means Info.dat was in a subfolder
                    ErrorHandler.Instance.QueuePopup(ErrorType.Warning, "Map files aren't in the root!");
                    Debug.LogWarning("Map files aren't in the root!");
                }
                break;
            }
        }

        if(infoData == null)
        {
            ErrorHandler.Instance.QueuePopup(ErrorType.Error, "Unable to load Info.dat!");
            Debug.LogWarning("Zip file has no Info.dat!");
            return (info, difficulties, song, coverImageData);
        }

        string infoJson = System.Text.Encoding.UTF8.GetString(FileUtil.StreamToBytes(infoData));
        try
        {
            info = JsonUtility.FromJson<BeatmapInfo>(infoJson);
            infoData.Dispose();
        }
        catch(Exception e)
        {
            ErrorHandler.Instance.QueuePopup(ErrorType.Error, "Unable to parse Info.dat!");
            Debug.LogWarning($"Failed to parse Info.dat with error: {e.Message}, {e.StackTrace}");

            info = null;
            infoData.Dispose();

            return (info, difficulties, song, coverImageData);
        }

        Debug.Log($"Loaded info for {info._songAuthorName} - {info._songName}, mapped by {info._levelAuthorName}");

        if(info?._difficultyBeatmapSets == null || info._difficultyBeatmapSets.Length < 1)
        {
            ErrorHandler.Instance.QueuePopup(ErrorType.Warning, "The map lists no difficulty sets!");
            Debug.LogWarning("Info lists no difficulty sets!");
        }
        else
        {
            foreach(DifficultyBeatmapSet set in info._difficultyBeatmapSets)
            {
                string characteristicName = set._beatmapCharacteristicName;

                if(set._difficultyBeatmaps.Length == 0)
                {
                    ErrorHandler.Instance.QueuePopup(ErrorType.Warning, $"Characteristic {characteristicName} lists no difficulties!");
                    Debug.LogWarning($"{characteristicName} lists no difficulties!");
                    continue;
                }

                DifficultyCharacteristic setCharacteristic = BeatmapInfo.CharacteristicFromString(characteristicName);

                List<Difficulty> newDifficulties = new List<Difficulty>();
                foreach(DifficultyBeatmap beatmap in set._difficultyBeatmaps)
                {
                    MapLoader.LoadingMessage = $"Loading {beatmap._beatmapFilename}";
                    Debug.Log($"Loading {beatmap._beatmapFilename}");
                    
                    Difficulty diff = await GetDiff(beatmap, archive);
                    if(diff == null)
                    {
                        continue;
                    }

                    diff.characteristic = setCharacteristic;
                    diff.requirements = beatmap._customData?._requirements ?? new string[0];

                    newDifficulties.Add(diff);
                }

                difficulties.AddRange(newDifficulties);
                Debug.Log($"Finished loading {newDifficulties.Count} difficulties in characteristic {characteristicName}.");
            }
        }

        string songFilename = info?._songFilename ?? "";
        foreach(ZipArchiveEntry entry in archive.Entries)
        {
            if(entry.Name.Equals(songFilename))
            {
                //We need to convert the stream specifically to a Memory Stream to make it seekable
                //This is required for audio processing to work
                using(Stream songStream = entry.Open())
                {
                    song = new MemoryStream(FileUtil.StreamToBytes(songStream));
                }
                break;
            }
        }
        if(song == null)
        {
            ErrorHandler.Instance.QueuePopup(ErrorType.Error, "Song file not found!");
            Debug.LogWarning($"Didn't find audio file {songFilename}!");
            return (info, difficulties, song, coverImageData);
        }

        MapLoader.LoadingMessage = "Loading cover image";
        Debug.Log("Loading cover image.");
        await Task.Yield();

        string coverFilename = info?._coverImageFilename ?? "";
        foreach(ZipArchiveEntry entry in archive.Entries)
        {
            if(entry.Name.Equals(coverFilename))
            {
                using(Stream coverImageStream = entry.Open())
                {
                    coverImageData = FileUtil.StreamToBytes(coverImageStream);
                }
            }
        }
        if(coverImageData == null || coverImageData.Length <= 0)
        {
            ErrorHandler.Instance.QueuePopup(ErrorType.Warning, "Cover image not found!");
            Debug.Log($"Didn't find image file {coverFilename}!");
        }

        return (info, difficulties, song, coverImageData);
    }


    private static async Task<Difficulty> GetDiff(DifficultyBeatmap beatmap, ZipArchive archive)
    {
        await Task.Yield();

        Difficulty output = new Difficulty
        {
            difficultyRank = MapLoader.DiffValueFromString[beatmap._difficulty],
            NoteJumpSpeed = beatmap._noteJumpMovementSpeed,
            SpawnOffset = beatmap._noteJumpStartBeatOffset
        };
        output.Label = beatmap._customData?._difficultyLabel ?? Difficulty.DiffLabelFromRank(output.difficultyRank);

        string filename = beatmap._beatmapFilename;
        Debug.Log($"Loading json from {filename}");

        Stream diffData = null;
        foreach(ZipArchiveEntry entry in archive.Entries)
        {
            if(entry.Name.Equals(filename))
            {
                diffData = entry.Open();
                break;
            }
        }

        if(diffData == null)
        {
            ErrorHandler.Instance.QueuePopup(ErrorType.Warning, $"Unable to load {filename}!");
            Debug.LogWarning("Difficulty file not found!");
            return null;
        }

        Debug.Log($"Parsing {filename}");
        string diffJson = System.Text.Encoding.UTF8.GetString(FileUtil.StreamToBytes(diffData));
        output.beatmapDifficulty = JsonReader.ParseBeatmapFromJson(diffJson);
        diffData.Dispose();

        return output;
    }
}
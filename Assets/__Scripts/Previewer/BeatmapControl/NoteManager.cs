using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NoteManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject[] redNotePrefabs;
    [SerializeField] private GameObject[] blueNotePrefabs;
    [SerializeField] private GameObject bombPrefab;

    [Header("Object Parents")]
    [SerializeField] private GameObject noteParent;
    [SerializeField] private GameObject bombParent;

    [SerializeField] private float floorOffset;

    public List<Note> Notes = new List<Note>();
    public List<Note> RenderedNotes = new List<Note>();

    public List<Bomb> Bombs = new List<Bomb>();
    public List<Bomb> RenderedBombs = new List<Bomb>();

    private ObjectManager objectManager;

    public static readonly Dictionary<int, float> DirectionAngles = new Dictionary<int, float>
    {
        {0, 180},
        {1, 0},
        {2, -90},
        {3, 90},
        {4, -135},
        {5, 135},
        {6, -45},
        {7, 45},
        {8, 0}
    };


    public void LoadNotesFromDifficulty(Difficulty difficulty)
    {
        ClearRenderedNotes();
        BeatmapDifficulty beatmap = difficulty.beatmapDifficulty;

        List<Note> newNotes = new List<Note>();
        List<Bomb> newBombs = new List<Bomb>();

        if(beatmap.colorNotes.Length > 0)
        {
            foreach(ColorNote n in beatmap.colorNotes)
            {
                Note newNote = Note.NoteFromColorNote(n);
                newNotes.Add(newNote);
            }
        }
        else
        {
            newNotes = new List<Note>();
        }

        if(beatmap.bombNotes.Length > 0)
        {
            foreach(BombNote b in beatmap.bombNotes)
            {
                Bomb newBomb = Bomb.BombFromBombNote(b);
                newBombs.Add(newBomb);
            }
        }
        else
        {
            newBombs = new List<Bomb>();
        }

        //Precalculate startY and window snapping
        List<BeatmapObject> notesAndBombs = new List<BeatmapObject>();
        notesAndBombs.AddRange(newNotes);
        notesAndBombs.AddRange(newBombs);
        notesAndBombs = ObjectManager.SortObjectsByBeat<BeatmapObject>(notesAndBombs);

        //These get repopulated with the modified (precalculated) objects
        newNotes.Clear();
        newBombs.Clear();

        List<BeatmapObject> sameBeatObjects = new List<BeatmapObject>();
        List<Note> notesOnBeat = new List<Note>();
        List<Bomb> bombsOnBeat = new List<Bomb>();
        for(int i = 0; i < notesAndBombs.Count; i++)
        {
            BeatmapObject current = notesAndBombs[i];

            if(sameBeatObjects.Count == 0 || !ObjectManager.CheckSameBeat(current.Beat, sameBeatObjects[0].Beat))
            {
                //This object doesn't share the same beat with the previous objects
                sameBeatObjects.Clear();

                for(int x = i; x < notesAndBombs.Count; x++)
                {
                    //Gather all consecutive objects that share the same beat
                    BeatmapObject check = notesAndBombs[x];
                    if(ObjectManager.CheckSameBeat(check.Beat, current.Beat))
                    {
                        sameBeatObjects.Add(check);
                        //Skip to the first object that doesn't share this beat next loop
                        i = x;
                    }
                    else break;
                }

                notesOnBeat = sameBeatObjects.OfType<Note>().ToList();
                bombsOnBeat = sameBeatObjects.OfType<Bomb>().ToList();
            }

            for(int x = 0; x < notesOnBeat.Count; x++)
            {
                Note n = notesOnBeat[x];
                n.StartY = GetStartY(n, sameBeatObjects);
                n.WindowSnap = CheckAngleSnap(n, notesOnBeat);
                newNotes.Add(n);
            }
            for(int x = 0; x < bombsOnBeat.Count; x++)
            {
                Bomb b = bombsOnBeat[x];
                b.StartY = GetStartY(b, sameBeatObjects);
                newBombs.Add(b);
            }
        }

        Notes = ObjectManager.SortObjectsByBeat<Note>(newNotes);
        Bombs = ObjectManager.SortObjectsByBeat<Bomb>(newBombs);

        UpdateNoteVisuals(TimeManager.CurrentBeat);
    }


    private bool CheckSameDirection(Note n, Note other)
    {
        return n.Direction == other.Direction || n.Direction == 8 || other.Direction == 8;
    }


    private bool CheckDiagonalNote(Note n)
    {
        return n.Direction == 8 || DirectionAngles[n.Direction] % 90 != 0;
    }


    private bool CheckUpwardNote(Note n)
    {
        return n.Direction == 8 || Mathf.Abs(DirectionAngles[n.Direction]) > 90;
    }


    private bool CheckRightNote(Note n)
    {
        return n.Direction == 8 || DirectionAngles[n.Direction] > 0;
    }


    private float GetWindowAngleOrthogonal(WindowType type)
    {
        const float knightMoveAngle = 26.565f;
        const float fourWideAngle = 18.435f;
        const float maxWideAngle = 33.69f;

        switch(type)
        {
            case WindowType.None: return 0;
            case WindowType.KnightMove: return knightMoveAngle;
            case WindowType.FourWide: return fourWideAngle;
            case WindowType.MegaWide: return maxWideAngle;
            default: return 0;
        }
    }


    private float GetWindowAngleDiagonal(WindowType type)
    {
        const float knightMoveAngleDiagonal = 18.435f;
        const float fourWideAngleDiagonal = 26.565f;
        const float maxWideAngleDiagonal = 11.31f;

        switch(type)
        {
            case WindowType.None: return 0;
            case WindowType.KnightMove: return knightMoveAngleDiagonal;
            case WindowType.FourWide: return fourWideAngleDiagonal;
            case WindowType.MegaWide: return maxWideAngleDiagonal;
            default: return 0;
        }
    }


    private float CheckAngleSnap(Note n, List<Note> sameBeatNotes)
    {
        List<Note> notesOnBeat = new List<Note>(sameBeatNotes);

        float angleOffset = n.AngleOffset;

        //Returns the angle offset the note should use to snap, or 0 if it shouldn't
        if(notesOnBeat.Count == 0) return angleOffset;

        notesOnBeat.RemoveAll(x => x.Color != n.Color);

        if(notesOnBeat.Count != 2)
        {
            //Angle snapping requires exactly 2 notes
            return angleOffset;
        }

        //Disregard notes that are on the same row or column
        notesOnBeat.RemoveAll(x => x.x == n.x || x.y == n.y);
        if(notesOnBeat.Count == 0)
        {
            return angleOffset;
        }

        Note other = notesOnBeat[0];
        bool otherDot = other.Direction == 8;

        if(n.x == other.x || n.y == other.y)
        {
            //Straight horizontal or vertical angle doesn't need snapping
            return angleOffset;
        }
        if(!CheckSameDirection(n, other))
        {
            //Notes must have the same direction
            return angleOffset;
        }

        int xDist = (int)(n.x - other.x);
        int yDist = (int)(n.y - other.y);
        int absxDist = Mathf.Abs(xDist);
        int absyDist = Mathf.Abs(yDist);

        bool dot = n.Direction == 8;
        bool counterClockwise = xDist >= 0 != yDist >= 0;
        int directionMult = counterClockwise ? 1 : -1;

        bool equalDistance = absxDist == absyDist;
        if(equalDistance)
        {
            //Equal distances always means a 45 degree angle
            if(!dot)
            {
                //No snapping needed for 45 degrees
                return angleOffset;
            }
            else
            {
                if(otherDot)
                {
                    //Dots snap to 45 degrees
                    return 45 * directionMult;
                }
                if(!CheckDiagonalNote(other))
                {
                    //Don't snap if the other note isn't diagonal
                    return angleOffset;
                }
                if((CheckUpwardNote(other) == yDist < 0) == (CheckRightNote(other) == xDist < 0))
                {
                    //Arrow points towards this dot or directly away from this dot
                    return 45 * directionMult;
                }
            }
        }

        //Windows!!!
        WindowType windowType = WindowType.None;
        if((absxDist == 2 && absyDist == 1) || (absxDist == 1 && absyDist == 2))
        {
            windowType = WindowType.KnightMove;
        }
        else if(absxDist == 3 && absyDist == 1)
        {
            windowType = WindowType.FourWide;
        }
        else if(absxDist == 3 && absyDist == 2)
        {
            windowType = WindowType.MegaWide;
        }
        
        bool mainVertical = windowType == WindowType.KnightMove && absyDist == 2;

        float angle = angleOffset;
        if(dot)
        {
            //Force dots to match rotation direction
            angle = DirectionAngles[other.Direction];
        }

        if(mainVertical)
        {
            if(n.Direction == 0 || n.Direction == 1 || (dot && (other.Direction == 0 || other.Direction == 1 || otherDot)))
            {
                //Vertical notes should snap
                if(dot && !otherDot) angle *= directionMult;
                angle += GetWindowAngleOrthogonal(windowType);

                return angle * directionMult;
            }
            directionMult *= -1;
        }
        else if(n.Direction == 2 || n.Direction == 3 || (dot && (other.Direction == 2 || other.Direction == 3 || otherDot)))
        {
            //Horizontal notes should snap
            if(dot && !otherDot) angle *= directionMult * -1;
            angle += GetWindowAngleOrthogonal(windowType);

            return angle * directionMult * -1;
        }

        if(CheckDiagonalNote(n) && CheckDiagonalNote(other))
        {
            bool up = yDist > 0;
            bool right = xDist > 0;
            bool correctDiagonal = (CheckUpwardNote(n) == up) == (CheckRightNote(n) == right) || dot;
            bool otherCorrectDiagonal = (CheckUpwardNote(other) == up) == (CheckRightNote(other) == right) || otherDot;

            if(correctDiagonal && otherCorrectDiagonal)
            {
                if(dot && !otherDot) angle *= directionMult;
                angle += GetWindowAngleDiagonal(windowType);

                return angle * directionMult;
            }
        }

        return angleOffset;
    }


    private static float SpawnParabola(float targetHeight, float baseHeight, float halfJumpDistance, float t)
    {
        float dSquared = Mathf.Pow(halfJumpDistance, 2);
        float tSquared = Mathf.Pow(t, 2);

        float movementRange = targetHeight - baseHeight;

        return -(movementRange / dSquared) * tSquared + targetHeight;
    }


    private int GetStartY(BeatmapObject n, List<BeatmapObject>sameBeatObjects)
    {
        List<BeatmapObject> objectsOnBeat = new List<BeatmapObject>(sameBeatObjects);

        if(n.y <= 0) return 0;

        if(objectsOnBeat.Count == 0) return 0;

        //Remove all notes that aren't directly below this one
        objectsOnBeat.RemoveAll(x => x.x != n.x || x.y >= n.y);

        if(objectsOnBeat.Count == 0) return 0;

        //Need to recursively calculate the startYs of each note underneath
        return objectsOnBeat.Max(x => GetStartY(x, objectsOnBeat)) + 1;
    }


    private float GetObjectY(float startY, float targetY, float objectTime)
    {
        float jumpTime = TimeManager.CurrentTime + BeatmapManager.ReactionTime;

        if(objectTime > jumpTime)
        {
            return startY;
        }
        
        float halfJumpDistance = BeatmapManager.JumpDistance / 2;
        return SpawnParabola(targetY, startY, halfJumpDistance, objectManager.GetZPosition(objectTime));
    }


    public void UpdateNoteVisual(Note n)
    {
        //Calculate the 2d position on the grid
        Vector2 gridPos = objectManager.bottomLeft;
        gridPos.x += n.x * objectManager.laneWidth;
        gridPos.y += n.y * objectManager.rowHeight;

        //Calculate the Z position based on time
        float noteTime = TimeManager.TimeFromBeat(n.Beat);

        float reactionTime = BeatmapManager.ReactionTime;
        float jumpTime = TimeManager.CurrentTime + reactionTime;

        float worldDist = objectManager.GetZPosition(noteTime);
        Vector3 worldPos = new Vector3(gridPos.x, gridPos.y, worldDist);

        float startY = n.StartY * objectManager.rowHeight + floorOffset;
        worldPos.y = GetObjectY(startY, worldPos.y, noteTime);

        //Default to 0 in case of over-sized direction value
        int directionIndex = n.Direction >= 0 && n.Direction < 9 ? n.Direction : 0;
        float targetAngle = DirectionAngles[directionIndex] + n.WindowSnap;

        float angle = targetAngle;
        float rotationAnimationLength = reactionTime * objectManager.rotationAnimationTime;

        if(noteTime > jumpTime)
        {
            //Note is still jumping in
            angle = 0;
        }
        else if(noteTime > jumpTime - rotationAnimationLength)
        {
            float timeSinceJump = reactionTime - (noteTime - TimeManager.CurrentTime);
            float rotationProgress = timeSinceJump / rotationAnimationLength;
            float angleDist = Easings.Sine.Out(rotationProgress);

            angle = targetAngle * angleDist;
        }

        if(n.Visual == null)
        {
            int dot = n.Direction == 8 ? 1 : 0;
            GameObject prefab = n.Color == 0 ? redNotePrefabs[dot] : blueNotePrefabs[dot];
            n.Visual = Instantiate(prefab);
            n.Visual.transform.SetParent(noteParent.transform);

            RenderedNotes.Add(n);
        }
        n.Visual.transform.localPosition = worldPos;
        n.Visual.transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }


    public void UpdateBombVisual(Bomb b)
    {
        //Calculate the 2d position on the grid
        Vector2 gridPos = objectManager.bottomLeft;
        gridPos.x += b.x * objectManager.laneWidth;
        gridPos.y += b.y * objectManager.rowHeight;

        float bombTime = TimeManager.TimeFromBeat(b.Beat);
        float worldDist = objectManager.GetZPosition(bombTime);

        Vector3 worldPos = new Vector3(gridPos.x, gridPos.y, worldDist);

        float startY = b.StartY * objectManager.rowHeight + floorOffset;
        worldPos.y = GetObjectY(startY, worldPos.y, bombTime);

        if(b.Visual == null)
        {
            b.Visual = Instantiate(bombPrefab);
            b.Visual.transform.SetParent(bombParent.transform);

            RenderedBombs.Add(b);
        }
        b.Visual.transform.localPosition = worldPos;
    }


    public void ClearOutsideNotes()
    {
        if(RenderedNotes.Count > 0)
        {
            for(int i = RenderedNotes.Count - 1; i >= 0; i--)
            {
                Note n = RenderedNotes[i];
                if(!objectManager.CheckInSpawnRange(n.Beat))
                {
                    n.ClearVisual();
                    RenderedNotes.Remove(n);
                }
            }
        }

        if(RenderedBombs.Count > 0)
        {
            for(int i = RenderedBombs.Count - 1; i >= 0; i--)
            {
                Bomb b = RenderedBombs[i];
                if(!CheckBombInSpawnRange(b))
                {
                    b.ClearVisual();
                    RenderedBombs.Remove(b);
                }
            }
        }
    }


    public void ClearRenderedNotes()
    {
        //Clear all rendered notes
        if(RenderedNotes.Count > 0)
        {
            foreach(Note n in RenderedNotes)
            {
                n.ClearVisual();
            }
            RenderedNotes.Clear();
        }

        if(RenderedBombs.Count > 0)
        {
            foreach(Bomb b in RenderedBombs)
            {
                b.ClearVisual();
            }
            RenderedBombs.Clear();
        }
    }


    public void UpdateNoteVisuals(float beat)
    {
        ClearOutsideNotes();

        if(Notes.Count > 0)
        {
            int firstNote = Notes.FindIndex(x => x.Beat > TimeManager.CurrentBeat);
            if(firstNote >= 0)
            {
                for(int i = firstNote; i < Notes.Count; i++)
                {
                    //Update each note's position
                    Note n = Notes[i];
                    if(objectManager.CheckInSpawnRange(n.Beat))
                    {
                        UpdateNoteVisual(n);
                    }
                    else break;
                }
            }
        }

        if(Bombs.Count > 0)
        {
            int firstBomb = Bombs.FindIndex(x => x.Beat > TimeManager.CurrentBeat + TimeManager.BeatFromTime(objectManager.BehindCameraTime));
            if(firstBomb >= 0)
            {
                for(int i = firstBomb; i < Bombs.Count; i++)
                {
                    Bomb b = Bombs[i];
                    if(CheckBombInSpawnRange(b))
                    {
                        UpdateBombVisual(b);
                    }
                    else break;
                }
            }
        }
    }


    private bool CheckBombInSpawnRange(Bomb b)
    {
        float bombTime = TimeManager.TimeFromBeat(b.Beat);
        bool timeInRange = TimeManager.CurrentTime > bombTime && TimeManager.CurrentTime <= bombTime - objectManager.BehindCameraTime;

        return objectManager.CheckInSpawnRange(b.Beat) || timeInRange;
    }


    private void Start()
    {
        objectManager = ObjectManager.Instance;

        BeatmapManager.OnBeatmapDifficultyChanged += LoadNotesFromDifficulty;
        TimeManager.OnBeatChanged += UpdateNoteVisuals;
    }
}


enum WindowType
{
    None,
    KnightMove,
    FourWide,
    MegaWide
}
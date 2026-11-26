using System;
using System.Collections.Generic;
using UnityEngine;

// Where a decoration can be stuck
[Flags]
public enum DecorationSpot
{
    None = 0,
    Floor = 1,      // standing on the floor (bushes)
    Ceiling = 2,    // hanging from a ceiling or from under a platform (vines)
    Wall = 4,       // stuck to the side of a wall (vine patches)
    OpenSpace = 8   // floating in a free area of the room (neon signs)
}

// One decoration. Several sprites can form one piece (a neon sign cut into the circle and its letters): they are
// put back together at the place they have in their texture.
[Serializable]
public class DecorationPiece
{
    public string name;
    [Tooltip("A ready-made sign (the animated LED signs of Assets/Carteles). When set, 'sprites' is not used: " +
             "the prefab is placed as it is, only scaled to the height")]
    public GameObject prefab;
    public Sprite[] sprites;
    public DecorationSpot spots = DecorationSpot.Floor;
    [Tooltip("Height in world units (tiles), picked at random between x and y")]
    public Vector2 height = new Vector2(2f, 3f);
    [Tooltip("Turned upside down (vines that hang)")]
    public bool flipY;
    [Tooltip("Can be mirrored left-right at random")]
    public bool randomFlipX = true;
    public Color color = Color.white;
    [Min(0f)] public float weight = 1f;
}

// Sprites and amounts used by RoomDecorator. The asset lives in Resources so the generator finds it without a
// reference in the scene (Resources/RoomDecorationSet).
[CreateAssetMenu(fileName = "RoomDecorationSet", menuName = "Dino Revenge/Room Decoration Set")]
public class RoomDecorationSet : ScriptableObject
{
    public const string ResourcePath = "RoomDecorationSet";

    [Header("Pieces")]
    public List<DecorationPiece> plants = new List<DecorationPiece>();
    public List<DecorationPiece> neons = new List<DecorationPiece>();

    [Header("Plants -> neon")]
    [Tooltip("Rooms at the end of the run that have only neon (2 = the second-to-last and the last one). " +
             "The first room has only plants and the rooms in between go from one to the other little by little")]
    [Min(1)] public int neonOnlyRoomsAtEnd = 2;

    [Header("Amounts per room (a room that is all plants / all neon)")]
    [Tooltip("Plants in a room with no neon. Scaled down as the rooms turn to neon")]
    public Vector2Int plantsPerRoom = new Vector2Int(12, 18);
    [Tooltip("Neon signs in a room that is all neon. Scaled down in the rooms that are still mostly plants")]
    public Vector2Int neonsPerRoom = new Vector2Int(2, 3);
    [Tooltip("Neon signs in every secret room (always at least the minimum)")]
    public Vector2Int neonsPerSecretRoom = new Vector2Int(2, 3);
    [Tooltip("Never more neon signs than this in one room (secret rooms too)")]
    [Min(0)] public int maxNeonsPerRoom = 3;

    [Header("Look")]
    public string plantSortingLayer = "EasterEggs";   // behind the tiles, so the part stuck in the rock is hidden
    public string neonSortingLayer = "Decoraciones";
    [Tooltip("How much of a plant goes into the rock it grows from (fraction of its size)")]
    [Range(0f, 0.5f)] public float plantSink = 0.2f;
    [Tooltip("Free tiles kept around a neon sign")]
    [Min(0)] public int neonMargin = 1;
    [Tooltip("Blink for the neon signs made of sprites (the prefabs bring their own animation)")]
    public bool neonFlicker = true;

    // 0 = only plants, 1 = only neon. Room 0 is 0, the last 'neonOnlyRoomsAtEnd' rooms are 1.
    public float NeonRatio(int roomIndex, int totalRooms)
    {
        int firstNeonOnly = totalRooms - neonOnlyRoomsAtEnd;
        if (firstNeonOnly <= 0) return 1f;
        return Mathf.Clamp01(roomIndex / (float)firstNeonOnly);
    }
}

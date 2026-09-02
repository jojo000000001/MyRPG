using UnityEngine;

[CreateAssetMenu(fileName = "HealthBarSpriteCatalog", menuName = "Game/Health Bar Sprite Catalog")]
public sealed class HealthBarSpriteCatalog : ScriptableObject
{
    [Header("Track")]
    public Sprite backLeft;
    public Sprite backMid;
    public Sprite backRight;

    [Header("Health Fill")]
    public Sprite greenLeft;
    public Sprite greenMid;
    public Sprite greenRight;
    public Sprite redLeft;
    public Sprite redMid;
    public Sprite redRight;

    [Header("Experience Fill")]
    public Sprite blueLeft;
    public Sprite blueMid;
    public Sprite blueRight;

    public Sprite GetBack(string segment)
    {
        return segment switch
        {
            "Left" => backLeft,
            "Right" => backRight,
            _ => backMid,
        };
    }

    public Sprite GetGreen(string segment)
    {
        return segment switch
        {
            "Left" => greenLeft,
            "Right" => greenRight,
            _ => greenMid,
        };
    }

    public Sprite GetRed(string segment)
    {
        return segment switch
        {
            "Left" => redLeft,
            "Right" => redRight,
            _ => redMid,
        };
    }

    public Sprite GetBlue(string segment)
    {
        return segment switch
        {
            "Left" => blueLeft,
            "Right" => blueRight,
            _ => blueMid,
        };
    }

    public bool HasBackSprites => backLeft != null && backMid != null && backRight != null;
}

using UnityEngine;

[CreateAssetMenu(fileName = "EnemyHealthBarStyle", menuName = "Game/Enemy Health Bar Style")]
public sealed class EnemyHealthBarStyle : ScriptableObject
{
    [Header("Sprites")]
    public Sprite frame;
    public Sprite fill;
    public Sprite back;

    [Header("World Layout")]
    public Vector2 worldBarSize = new Vector2(1.85f, 0.36f);
    public Vector3 worldOffset = new Vector3(0f, 2.25f, 0f);

    [Tooltip("Fill/back inset from the frame edge, in canvas pixels.")]
    public Vector2 fillInsetPixels = new Vector2(22f, 11f);

    [Tooltip("Larger values shrink sliced frame end-caps.")]
    public float framePixelsPerUnitMultiplier = 1f;
}

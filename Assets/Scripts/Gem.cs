using UnityEngine;

public enum GemType
{
    Red, Yellow, Green, Blue
}

public class Gem : MonoBehaviour
{
    public GemType type;
    public SpriteRenderer sr;

    public void Set(GemType type, Sprite sprite)
    {
        this.type = type;
        if (!sr) GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        name = $"Gem_{type}";
    }
}

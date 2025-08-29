using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class SortByY : MonoBehaviour
{
    [SerializeField] private int offset = 0;   // tweak per asset if needed
    [SerializeField] private float multiplier = 100f;

    private SpriteRenderer sr;
    private Transform root;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        root = transform.root;
    }

    private void LateUpdate()
    {
        // Use world Y of the root (character position), convert to an int sorting order
        float y = root.position.y;
        sr.sortingOrder = offset + Mathf.RoundToInt(-y * multiplier);
    }
}

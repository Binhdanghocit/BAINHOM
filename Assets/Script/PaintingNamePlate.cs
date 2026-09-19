using UnityEngine;
using TMPro;

[ExecuteAlways]
public class PaintingNamePlate : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private void Start()
    {
        UpdatePlateText();
    }

    private void OnValidate()
    {
        UpdatePlateText();
    }

    public void UpdatePlateText()
    {
        var info = GetComponentInParent<PaintingInfo>();
        if (info != null && label != null)
        {
            label.text = info.paintingTitle;
        }
    }
}
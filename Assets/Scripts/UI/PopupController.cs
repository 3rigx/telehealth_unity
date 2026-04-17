using JetBrains.Annotations;
using TMPro;
using UnityEngine;

public class PopupController : MonoBehaviour
{
    [CanBeNull] public GameObject closeButton;

    public bool IsClosable = true;

    // Start is called before the first frame update
    public GameObject popup;
    public TextMeshProUGUI text;

    private void Start()
    {
    }

    // Update is called once per frame
    private void Update()
    {
    }

    public void SetText(string text)
    {
        this.text.text = text;
    }

    public void Show()
    {
        popup.SetActive(true);
        if (closeButton) closeButton.SetActive(IsClosable);
    }

    public void Show(string text)
    {
        SetText(text);
        Show();
    }

    public void Close()
    {
        popup.SetActive(false);
    }
}
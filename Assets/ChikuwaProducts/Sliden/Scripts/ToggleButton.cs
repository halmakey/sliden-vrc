
using UdonSharp;

public class ToggleButton : UdonSharpBehaviour
{
    public UnityEngine.UI.Image onImage;
    public UnityEngine.UI.Image offImage;
    public bool InitialOn;

    private bool _on;
    private bool _highlight;

    private void Start()
    {
        _on = InitialOn;

        RefreshImages();
    }
    
    public void OnPointerEnter()
    {
        _highlight = true;
        RefreshImages();
    }

    public void OnPointerExit()
    {
        _highlight = false;
        RefreshImages();
    }

    public void SetOn()
    {
        _on = true;
        RefreshImages();
    }

    public void SetOff()
    {
        _on = false;
        RefreshImages();
    }

    private void RefreshImages()
    {
        onImage.enabled = _on ^ _highlight;
        offImage.enabled = !_on ^ _highlight;
    }
}

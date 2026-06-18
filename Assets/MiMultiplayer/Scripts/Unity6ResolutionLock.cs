using UnityEngine;

public class Unity6ResolutionLock : MonoBehaviour
{
    private int lastWidth;
    private int lastHeight;

    private void Start()
    {
        // Force initial Windowed mode to 920p at the start
        if (Screen.fullScreenMode == FullScreenMode.Windowed)
        {
            Screen.SetResolution(960, 540, FullScreenMode.Windowed);
        }

        lastWidth = Screen.width;
        lastHeight = Screen.height;
    }

    private void Update()
    {
        // Detect if the user is trying to resize the window manually
        if (Screen.fullScreenMode == FullScreenMode.Windowed)
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight)
            {
                // If they altered the window dimensions, snap it directly back to 960x540
                if (Screen.width != 960 || Screen.height != 540)
                {
                    Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
                }

                lastWidth = Screen.width;
                lastHeight = Screen.height;
            }
        }
    }
}
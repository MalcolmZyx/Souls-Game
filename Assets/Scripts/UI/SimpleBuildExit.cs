using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// A basic settings/exit menu updated for the New Input System.
/// Works with both Keyboard and Controller.
/// </summary>
public class SimpleBuildExit : MonoBehaviour
{
    private bool isPaused = false;
    private int selectedIndex = 0; // 0 = Resume, 1 = Exit
    private float lastNavTime = 0f;
    private const float navCooldown = 0.2f;

    void Update()
    {
        bool togglePressed = false;
        bool submitPressed = false;
        float verticalMove = 0f;

#if ENABLE_INPUT_SYSTEM
        // --- NEW INPUT SYSTEM LOGIC ---
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) togglePressed = true;
        if (gamepad != null && (gamepad.startButton.wasPressedThisFrame || gamepad.selectButton.wasPressedThisFrame)) togglePressed = true;

        if (isPaused)
        {
            if (keyboard != null && keyboard.enterKey.wasPressedThisFrame) submitPressed = true;
            if (gamepad != null && gamepad.aButton.wasPressedThisFrame) submitPressed = true;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) verticalMove = 1f;
                else if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) verticalMove = -1f;
            }
            if (gamepad != null)
            {
                verticalMove = gamepad.leftStick.y.ReadValue();
                if (Mathf.Abs(verticalMove) < 0.2f) verticalMove = gamepad.dpad.y.ReadValue();
            }
        }
#else
        // --- LEGACY INPUT SYSTEM LOGIC ---
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7) || Input.GetKeyDown(KeyCode.JoystickButton9)) togglePressed = true;
        
        if (isPaused)
        {
            if (Input.GetButtonDown("Submit") || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.JoystickButton0)) submitPressed = true;
            verticalMove = Input.GetAxisRaw("Vertical");
        }
#endif

        // Process Input
        if (togglePressed) ToggleMenu();

        if (isPaused)
        {
            // Navigation
            if (Mathf.Abs(verticalMove) > 0.5f && Time.unscaledTime > lastNavTime + navCooldown)
            {
                selectedIndex = (selectedIndex == 0) ? 1 : 0;
                lastNavTime = Time.unscaledTime;
            }

            // Selection
            if (submitPressed) ExecuteSelection();
        }
    }

    void OnGUI()
    {
        if (isPaused)
        {
            GUI.skin.box.fontSize = 18;
            GUI.skin.button.fontSize = 16;

            float width = 300;
            float height = 220;
            Rect boxRect = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
            
            GUI.Box(boxRect, "PAUSED / SETTINGS");

            // --- RESUME ---
            Rect resumeRect = new Rect(boxRect.x + 50, boxRect.y + 60, 200, 40);
            if (selectedIndex == 0) GUI.color = Color.yellow;
            if (GUI.Button(resumeRect, "RESUME")) Resume();
            GUI.color = Color.white;

            // --- EXIT ---
            Rect exitRect = new Rect(boxRect.x + 50, boxRect.y + 115, 200, 40);
            if (selectedIndex == 1) GUI.color = Color.yellow;
            if (GUI.Button(exitRect, "EXIT GAME")) Application.Quit();
            GUI.color = Color.white;

            // --- INSTRUCTIONS ---
            GUIStyle textStyle = new GUIStyle(GUI.skin.label);
            textStyle.alignment = TextAnchor.MiddleCenter;
            textStyle.fontSize = 14;
            GUI.Label(new Rect(boxRect.x + 20, boxRect.y + height - 55, width - 40, 45), 
                "NAV: Arrows / Stick\nSELECT: Enter / (A Button)", textStyle);
        }
        else
        {
            // --- GAMEPLAY HINT ---
            // Draw a small, subtle hint in the bottom corner while playing
            GUIStyle hintStyle = new GUIStyle(GUI.skin.label);
            hintStyle.alignment = TextAnchor.LowerRight;
            hintStyle.fontSize = 12;
            GUI.color = new Color(1, 1, 1, 0.5f); // 50% transparency
            
            Rect hintRect = new Rect(Screen.width - 210, Screen.height - 40, 200, 30);
            GUI.Label(hintRect, "[ESC] or [START] for Settings", hintStyle);
            GUI.color = Color.white;
        }
    }

    private void ToggleMenu()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        Cursor.visible = isPaused;
        Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        selectedIndex = 0; 
    }

    private void ExecuteSelection()
    {
        if (selectedIndex == 0) Resume();
        else Application.Quit();
    }

    private void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}

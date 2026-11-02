using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 平台跳跃游戏的输入中心。
/// 这个工程只启用了新版 Input System（Active Input Handling = Input System Package），
/// 所以这里直接读取 Keyboard / Gamepad，同时提供 override 接口，
/// 便于自动化测试或过场动画在没有真人输入时驱动角色。
/// </summary>
public static class GameInput
{
    public static bool UseOverride { get; private set; }

    static Vector2 overrideMove;
    static bool overrideJumpHeld;
    static bool overrideJumpPressed;
    static bool overrideRestartPressed;

    public static void BeginOverride()
    {
        UseOverride = true;
        overrideMove = Vector2.zero;
        overrideJumpHeld = false;
        overrideJumpPressed = false;
        overrideRestartPressed = false;
    }

    public static void EndOverride()
    {
        UseOverride = false;
        overrideMove = Vector2.zero;
        overrideJumpHeld = false;
        overrideJumpPressed = false;
        overrideRestartPressed = false;
    }

    public static void SetMove(Vector2 direction)
    {
        overrideMove = Vector2.ClampMagnitude(direction, 1f);
    }

    public static void HoldJump(bool held)
    {
        overrideJumpHeld = held;
        if (held) overrideJumpPressed = true;
    }

    public static void PressRestart()
    {
        overrideRestartPressed = true;
    }

    public static float Horizontal
    {
        get
        {
            if (UseOverride)
                return Mathf.Clamp(overrideMove.x, -1f, 1f);

#if ENABLE_INPUT_SYSTEM
            float x = 0f;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                float stick = gamepad.leftStick.ReadValue().x;
                if (Mathf.Abs(stick) > 0.25f) x = stick;
                if (gamepad.dpad.left.isPressed) x = -1f;
                if (gamepad.dpad.right.isPressed) x = 1f;
            }

            return Mathf.Clamp(x, -1f, 1f);
#else
            return Input.GetAxisRaw("Horizontal");
#endif
        }
    }

    public static bool JumpHeld
    {
        get
        {
            if (UseOverride) return overrideJumpHeld;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.isPressed || keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed))
                return true;
            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonSouth.isPressed)
                return true;
            return false;
#else
            return Input.GetButton("Jump");
#endif
        }
    }

    /// <summary>本帧是否按下跳跃。读取即消耗（每帧只应有一个消费者）。</summary>
    public static bool JumpPressed
    {
        get
        {
            if (UseOverride)
            {
                bool value = overrideJumpPressed;
                overrideJumpPressed = false;
                return value;
            }
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame))
                return true;
            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
                return true;
            return false;
#else
            return Input.GetButtonDown("Jump");
#endif
        }
    }

    public static bool DownHeld
    {
        get
        {
            if (UseOverride) return overrideMove.y < -0.5f;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed))
                return true;
            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                if (gamepad.dpad.down.isPressed) return true;
                if (gamepad.leftStick.ReadValue().y < -0.5f) return true;
            }
            return false;
#else
            return Input.GetAxisRaw("Vertical") < -0.5f;
#endif
        }
    }

    /// <summary>本帧是否按下重开键（R）。读取即消耗。</summary>
    public static bool RestartPressed
    {
        get
        {
            if (UseOverride)
            {
                bool value = overrideRestartPressed;
                overrideRestartPressed = false;
                return value;
            }
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }
    }
}

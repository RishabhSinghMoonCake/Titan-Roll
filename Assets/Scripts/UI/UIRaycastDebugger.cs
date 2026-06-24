using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class UIRaycastDebugger : MonoBehaviour
{
    void Update()
    {
        bool isClicking = false;
        Vector2 screenPosition = Vector2.zero;

        // 1. Detect Mouse Click (Editor testing)
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            isClicking = true;
            screenPosition = Mouse.current.position.ReadValue();
        }
        // 2. Detect Touch (Mobile testing)
        else if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0 && Touchscreen.current.touches[0].press.wasPressedThisFrame)
        {
            isClicking = true;
            screenPosition = Touchscreen.current.touches[0].position.ReadValue();
        }

        // 3. Fire the Raycast
        if (isClicking)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            if (results.Count > 0)
            {
                Debug.Log($"<color=cyan>[UI Debugger]</color> You actually clicked on: <b>{results[0].gameObject.name}</b>");
            }
            else
            {
                Debug.Log("<color=red>[UI Debugger]</color> Hit nothing! EventSystem or GraphicRaycaster is dead.");
            }
        }
    }
}
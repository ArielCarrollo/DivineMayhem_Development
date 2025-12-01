using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class GamepadVirtualCursor : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private GraphicRaycaster raycaster;
    [SerializeField] private TextMeshProUGUI playerLabel;

    [Header("Configuración")]
    [SerializeField] private float cursorSpeed = 1000f;

    private RectTransform cursorRect;
    private PlayerInput playerInput;
    private Gamepad assignedGamepad;
    private Mouse assignedMouse;
    private Image cursorImage;

    private EventSystem eventSystem;
    private PointerEventData pointerData;
    private GameObject currentHover;
    private GameObject currentPressed;

    private void Awake()
    {
        cursorRect = GetComponent<RectTransform>();
        cursorImage = GetComponent<Image>();
        eventSystem = EventSystem.current;

        // Auto-detectar canvas raíz si no está asignado
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();

        // Auto-detectar Raycaster
        if (parentCanvas != null && raycaster == null)
            raycaster = parentCanvas.GetComponent<GraphicRaycaster>();

        pointerData = new PointerEventData(eventSystem);
        Cursor.visible = false;
    }

    public void Initialize(PlayerInput pi, Color playerColor, int playerIndex)
    {
        playerInput = pi;
        cursorImage.color = playerColor;
        if (playerLabel != null)
        {
            playerLabel.text = $"P{playerIndex + 1}";
            playerLabel.color = playerColor;
        }

        foreach (var device in playerInput.devices)
        {
            if (device is Gamepad g) assignedGamepad = g;
            else if (device is Mouse m) assignedMouse = m;
        }
    }

    private void Update()
    {
        // --- 1. MOVIMIENTO ---
        if (assignedGamepad != null)
        {
            Vector2 input = assignedGamepad.leftStick.ReadValue();
            if (input.sqrMagnitude > 0.01f) MoveCursorDelta(input);

            if (assignedGamepad.buttonSouth.wasPressedThisFrame) SimulateClickDown();
            if (assignedGamepad.buttonSouth.wasReleasedThisFrame) SimulateClickUp();
        }
        else if (assignedMouse != null)
        {
            Vector2 mousePos = assignedMouse.position.ReadValue();
            Vector2 delta = assignedMouse.delta.ReadValue();

            // Si el mouse se mueve, actualizamos. 
            // IMPORTANTE: En Overlay, mousePos ya es posición de pantalla correcta.
            if (delta.sqrMagnitude > 0.1f)
            {
                // Convertimos de pantalla a local para mover el RectTransform correctamente
                // Esto evita desajustes si el canvas escala
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentCanvas.transform as RectTransform,
                    mousePos,
                    parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
                    out Vector2 localPoint
                );
                cursorRect.anchoredPosition = localPoint;
            }

            if (assignedMouse.leftButton.wasPressedThisFrame) SimulateClickDown();
            if (assignedMouse.leftButton.wasReleasedThisFrame) SimulateClickUp();
        }

        // --- 2. RAYCAST ---
        UpdateHover();
    }

    // --- CORRECCIÓN 1: MANTENER SIEMPRE ENCIMA ---
    private void LateUpdate()
    {
        // Al hacerlo en LateUpdate, nos aseguramos de que si algún script activó un panel
        // en Update(), nosotros nos ponemos encima justo antes de renderizar.
        if (transform.GetSiblingIndex() != transform.parent.childCount - 1)
        {
            transform.SetAsLastSibling();
        }
    }

    private void MoveCursorDelta(Vector2 input)
    {
        if (parentCanvas == null) return;
        Vector2 anchoredPos = cursorRect.anchoredPosition;
        anchoredPos += input * cursorSpeed * Time.unscaledDeltaTime;

        Rect rect = parentCanvas.GetComponent<RectTransform>().rect;
        anchoredPos.x = Mathf.Clamp(anchoredPos.x, rect.xMin, rect.xMax);
        anchoredPos.y = Mathf.Clamp(anchoredPos.y, rect.yMin, rect.yMax);
        cursorRect.anchoredPosition = anchoredPos;
    }

    private void SimulateClickDown()
    {
        if (currentHover == null) return;
        currentPressed = currentHover;

        pointerData.button = PointerEventData.InputButton.Left;
        pointerData.pointerPress = currentPressed;
        pointerData.pressPosition = pointerData.position;
        pointerData.pointerPressRaycast = new RaycastResult { gameObject = currentPressed };

        ExecuteEvents.Execute(currentPressed, pointerData, ExecuteEvents.pointerDownHandler);
    }

    private void SimulateClickUp()
    {
        if (currentPressed == null) return;

        pointerData.button = PointerEventData.InputButton.Left;
        ExecuteEvents.Execute(currentPressed, pointerData, ExecuteEvents.pointerUpHandler);

        if (currentHover == currentPressed)
        {
            ExecuteEvents.Execute(currentPressed, pointerData, ExecuteEvents.pointerClickHandler);
        }

        pointerData.pointerPress = null;
        currentPressed = null;
    }

    private void UpdateHover()
    {
        if (raycaster == null) return;

        // --- CORRECCIÓN 2: COORDENADAS PRECISAS DE PANTALLA ---
        // Convertimos la posición del mundo (WorldSpace) del RectTransform a 
        // coordenadas de pantalla (ScreenSpace) para el Raycaster.
        // Esto soluciona problemas cuando cambias de paneles o resoluciones.
        Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, cursorRect.position);

        pointerData.position = screenPoint;
        pointerData.delta = Vector2.zero; // Importante resetear delta para evitar cálculos erróneos internos

        List<RaycastResult> results = new List<RaycastResult>();
        raycaster.Raycast(pointerData, results);

        GameObject newHover = results.Count > 0 ? results[0].gameObject : null;

        if (newHover != currentHover)
        {
            if (currentHover != null) ExecuteEvents.Execute(currentHover, pointerData, ExecuteEvents.pointerExitHandler);
            currentHover = newHover;
            if (currentHover != null) ExecuteEvents.Execute(currentHover, pointerData, ExecuteEvents.pointerEnterHandler);
        }
    }
}
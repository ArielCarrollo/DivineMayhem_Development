using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MultiplayerSelectorVisual : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private Image frameImage;
    [SerializeField] private TextMeshProUGUI playerLabel;

    [Header("Animación")]
    [SerializeField] private float moveSpeed = 20f;
    [SerializeField] private float sizeSpeed = 20f;
    [SerializeField] private Vector2 padding = new Vector2(20, 20);

    private EventSystem playerEventSystem;
    private RectTransform myRect;
    private GameObject currentSelectedObj;
    private RectTransform targetRect;

    // Array para guardar las 4 esquinas del botón objetivo
    // [0] = Abajo-Izq, [1] = Arriba-Izq, [2] = Arriba-Der, [3] = Abajo-Der
    private Vector3[] worldCorners = new Vector3[4];

    private void Awake()
    {
        myRect = GetComponent<RectTransform>();

        // 1. FORZAR ANCHORS AL CENTRO (Obligatorio para el marco)
        // Esto hace que el marco crezca desde su propio centro hacia afuera.
        if (myRect != null)
        {
            myRect.anchorMin = new Vector2(0.5f, 0.5f);
            myRect.anchorMax = new Vector2(0.5f, 0.5f);
            myRect.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    public void Initialize(EventSystem evtSystem, Color color, int playerIndex)
    {
        playerEventSystem = evtSystem;

        if (frameImage != null)
        {
            frameImage.color = color;
            if (frameImage.type != Image.Type.Sliced)
                Debug.LogWarning("MultiplayerSelectorVisual: ¡Usa una imagen 'Sliced' para el marco!");
        }

        if (playerLabel != null)
        {
            playerLabel.text = $"P{playerIndex + 1}";
            playerLabel.color = color;
        }

        transform.SetAsLastSibling();
    }

    private void Update()
    {
        if (playerEventSystem == null) return;

        GameObject selected = playerEventSystem.currentSelectedGameObject;

        // Detectar cambio de selección
        if (selected != null && selected != currentSelectedObj)
        {
            currentSelectedObj = selected;
            targetRect = selected.GetComponent<RectTransform>();
        }

        // Si tenemos un objetivo activo
        if (targetRect != null && currentSelectedObj != null && currentSelectedObj.activeInHierarchy)
        {
            if (!frameImage.enabled) ToggleVisuals(true);

            // --- CÁLCULO MATEMÁTICO UNIVERSAL ---

            // 1. Obtener las 4 esquinas reales del botón en el mundo
            // Esto ignora pivotes y anchors extraños del objetivo.
            targetRect.GetWorldCorners(worldCorners);

            // 2. Calcular el CENTRO VISUAL
            // El centro es el punto medio entre la esquina Abajo-Izq (0) y Arriba-Der (2)
            Vector3 visualCenter = (worldCorners[0] + worldCorners[2]) / 2f;

            // 3. Calcular el TAMAÑO REAL
            // Distancia física entre las esquinas, corregida por la escala del propio marco
            float width = Vector3.Distance(worldCorners[0], worldCorners[3]); // Ancho
            float height = Vector3.Distance(worldCorners[0], worldCorners[1]); // Alto

            // CORRECCIÓN DE ESCALA: 
            // Si el canvas padre está escalado, debemos dividir el tamaño mundial por la escala local del marco
            // para obtener el sizeDelta correcto.
            float scaleX = myRect.lossyScale.x;
            float scaleY = myRect.lossyScale.y;

            // Evitar división por cero
            if (scaleX == 0) scaleX = 1;
            if (scaleY == 0) scaleY = 1;

            Vector2 targetSize = new Vector2((width / scaleX) + padding.x, (height / scaleY) + padding.y);

            // --- APLICAR MOVIMIENTO ---

            // Mover hacia el centro visual calculado
            myRect.position = Vector3.Lerp(myRect.position, visualCenter, Time.unscaledDeltaTime * moveSpeed);

            // Ajustar tamaño
            myRect.sizeDelta = Vector2.Lerp(myRect.sizeDelta, targetSize, Time.unscaledDeltaTime * sizeSpeed);
        }
        else
        {
            if (frameImage.enabled) ToggleVisuals(false);
        }
    }

    private void ToggleVisuals(bool state)
    {
        if (frameImage) frameImage.enabled = state;
        if (playerLabel) playerLabel.enabled = state;
    }

    private void LateUpdate()
    {
        // Mantener siempre encima
        if (transform.parent != null && transform.GetSiblingIndex() != transform.parent.childCount - 1)
        {
            transform.SetAsLastSibling();
        }
    }
}
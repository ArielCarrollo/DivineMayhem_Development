using UnityEngine;

public class PlayerAppearance : MonoBehaviour
{
    [Header("Referencias a los Contenedores")]
    [Tooltip("Padre que contiene todos los modelos de cuerpos/skins.")]
    [SerializeField] private Transform bodiesParent;

    [Tooltip("Padre que contiene los modelos u objetos de ojos.")]
    [SerializeField] private Transform eyesParent;

    [Tooltip("Padre que contiene los modelos u objetos de guantes.")]
    [SerializeField] private Transform glovesParent;

    // --- Métodos públicos para la UI del Lobby (LocalLobbyManager) ---
    public int GetBodyCount() => bodiesParent != null ? bodiesParent.childCount : 0;
    public int GetEyesCount() => eyesParent != null ? eyesParent.childCount : 0;
    public int GetGlovesCount() => glovesParent != null ? glovesParent.childCount : 0;

    /// <summary>
    /// Método principal llamado por OfflinePlayerSpawner al iniciar la partida.
    /// Activa los GameObjects correspondientes según los índices seleccionados.
    /// </summary>
    /// <param name="bodyIndex">Índice del cuerpo/skin principal</param>
    /// <param name="eyesIndex">Índice de los ojos</param>
    /// <param name="glovesIndex">Índice de los guantes</param>
    public void ApplyOfflineAppearance(int bodyIndex, int eyesIndex, int glovesIndex)
    {
        // Aplicamos la configuración a cada parte
        SetPartActive(bodiesParent, bodyIndex);
        SetPartActive(eyesParent, eyesIndex);
        SetPartActive(glovesParent, glovesIndex);
    }

    /// <summary>
    /// Lógica interna para activar un solo hijo y desactivar el resto.
    /// </summary>
    private void SetPartActive(Transform parent, int index)
    {
        if (parent == null) return;

        int childCount = parent.childCount;
        if (childCount == 0) return; // No hay partes que activar

        // PROTECCIÓN: Aseguramos que el índice sea válido usando el operador módulo (%)
        // Esto evita errores si intentas cargar la skin 5 y solo tienes 3.
        // Ejemplo: Si hay 3 skins y pides la 5 -> 5 % 3 = 2 (Carga la skin 2)
        int safeIndex = index % childCount;
        if (safeIndex < 0) safeIndex += childCount; // Manejo de números negativos

        for (int i = 0; i < childCount; i++)
        {
            // Activamos solo el objeto que coincide con el índice seguro
            parent.GetChild(i).gameObject.SetActive(i == safeIndex);
        }
    }
}
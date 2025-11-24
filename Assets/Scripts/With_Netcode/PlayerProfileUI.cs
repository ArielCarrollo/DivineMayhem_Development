using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Threading.Tasks;
using Unity.Collections;
using System;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_STANDALONE && !UNITY_EDITOR
using SFB;
#endif

/// <summary>
/// Maneja la UI del perfil del jugador: mostrar y editar descripción, fecha de cumpleaños,
/// estado personal e imagen de perfil. Este script asume que existe un panel con
/// campos de entrada (TMP_InputField) y un botón de guardar. Al guardar,
/// actualiza los datos en CloudAuthManager y guarda el progreso en Cloud Save.
/// </summary>
public class PlayerProfileUI : MonoBehaviour
{
    [Header("Campos de perfil")]
    [SerializeField] private TMP_InputField descriptionField;
    [SerializeField] private TMP_InputField birthDateField;
    [SerializeField] private TMP_InputField statusField;
    [SerializeField] private Image profilePreviewImage;
    [SerializeField] private Button chooseImageButton;

    // Nuevo botón para rotar entre imágenes predefinidas manualmente
    [SerializeField] private Button cycleImageButton;

    [Header("Displays (solo lectura)")]
    [SerializeField] private TextMeshProUGUI descriptionDisplay;
    [SerializeField] private TextMeshProUGUI birthDateDisplay;
    [SerializeField] private TextMeshProUGUI statusDisplay;

    [Header("Guardar")]
    [SerializeField] private Button saveButton;
    [SerializeField] private TextMeshProUGUI feedbackText;

    // Aquí podrías definir una lista de sprites disponibles para el perfil
    [SerializeField] private Sprite[] availableProfileImages;

    private int currentImageIndex = 0;

    // Imagen base64 seleccionada por el usuario (si corresponde)
    private string newImageBase64 = null;

    // Si se marca, en Android se desactiva el diálogo de selección de archivos y solo se rota entre imágenes predefinidas.
    [SerializeField] private bool androidMode = false;

    /// <summary>
    /// Convierte una textura a una cadena base64 tras escalarla y comprimirla como JPEG.
    /// Cloud Save tiene un límite de tamaño por elemento (~10 KB), por lo que reducimos
    /// las dimensiones de la imagen para ajustarla a dicho límite. Ajustamos la imagen
    /// para que la dimensión mayor sea 128 píxeles y luego codificamos a JPG con
    /// calidad 80.
    /// </summary>
    private string ConvertTextureToBase64(Texture2D original)
    {
        if (original == null) return null;

        // Intentamos varias resoluciones y calidades para ajustarnos al límite de Cloud Save (~10 KB por item).
        // Empezamos con 128 px de lado mayor y calidad 80; si excede, reducimos a 64 px.
        int[] candidateSizes = new int[] { 128, 64, 32 };
        int[] candidateQualities = new int[] { 80, 60, 50 };
        string bestBase64 = null;

        foreach (int maxDimension in candidateSizes)
        {
            foreach (int quality in candidateQualities)
            {
                int origWidth = original.width;
                int origHeight = original.height;
                int targetWidth = origWidth;
                int targetHeight = origHeight;
                float maxOrigDim = Mathf.Max(origWidth, origHeight);
                if (maxOrigDim > maxDimension)
                {
                    float ratio = (float)maxDimension / maxOrigDim;
                    targetWidth = Mathf.Max(1, Mathf.RoundToInt(origWidth * ratio));
                    targetHeight = Mathf.Max(1, Mathf.RoundToInt(origHeight * ratio));
                }

                // Escalar
                RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
                Graphics.Blit(original, rt);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                Texture2D resized = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
                resized.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                resized.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);

                // Comprimir a JPG
                byte[] jpgBytes;
                try
                {
                    jpgBytes = resized.EncodeToJPG(quality);
                }
                catch
                {
                    jpgBytes = resized.EncodeToPNG();
                }
                string b64 = Convert.ToBase64String(jpgBytes);
                // Si es menor a 8.5KB, lo consideramos adecuado
                if (b64.Length < 8500)
                {
                    return b64;
                }
                // Guardamos el mejor (más pequeño) para fallback
                if (bestBase64 == null || b64.Length < bestBase64.Length)
                {
                    bestBase64 = b64;
                }
            }
        }
        // Si ninguna opción cabe dentro del límite aproximado, devolvemos la más pequeña
        return bestBase64;
    }

    /// <summary>
    /// Callback para el nuevo botón de ciclo de imágenes. Permite al jugador cambiar manualmente
    /// entre las imágenes predefinidas disponibles sin invocar el diálogo de selección de archivos.
    /// </summary>
    private void OnCycleImageClicked()
    {
        if (availableProfileImages == null || availableProfileImages.Length == 0)
            return;
        currentImageIndex = (currentImageIndex + 1) % availableProfileImages.Length;
        if (profilePreviewImage != null)
        {
            profilePreviewImage.sprite = availableProfileImages[currentImageIndex];
        }
    }

    private void Start()
    {
        // Inicializa UI con datos cargados
        LoadCurrentProfileData();

        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveButtonClicked);

        if (chooseImageButton != null)
            chooseImageButton.onClick.AddListener(OnChooseImageClicked);

        if (cycleImageButton != null)
            cycleImageButton.onClick.AddListener(OnCycleImageClicked);
    }

    private async void OnEnable()
    {
        // Cuando el panel se activa, recarga los datos desde Cloud Save antes de mostrarlos.
        if (!gameObject.activeSelf)
            return;

        if (CloudAuthManager.Instance != null)
        {
            try
            {
                await CloudAuthManager.Instance.LoadPlayerProgress();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"No se pudo recargar los datos del jugador antes de mostrar el perfil: {e.Message}");
            }
        }
        LoadCurrentProfileData();
    }

    private void LoadCurrentProfileData()
    {
        if (CloudAuthManager.Instance == null) return;
        var data = CloudAuthManager.Instance.LocalPlayerData;

        // Actualiza campos de entrada
        if (descriptionField != null)
            descriptionField.text = data.Description.ToString();
        if (birthDateField != null)
            birthDateField.text = data.BirthDate.ToString();
        if (statusField != null)
            statusField.text = data.Status.ToString();

        // Actualiza etiquetas de visualización con texto por defecto si están vacías
        if (descriptionDisplay != null)
        {
            string desc = data.Description.ToString();
            descriptionDisplay.text = string.IsNullOrWhiteSpace(desc) ? "Sin descripción" : desc;
        }
        if (birthDateDisplay != null)
        {
            string date = data.BirthDate.ToString();
            birthDateDisplay.text = string.IsNullOrWhiteSpace(date) ? "Sin fecha" : date;
        }
        if (statusDisplay != null)
        {
            string st = data.Status.ToString();
            statusDisplay.text = string.IsNullOrWhiteSpace(st) ? "Sin estado" : st;
        }

        // Cargar imagen de perfil desde base64 si existe
        bool loadedFromBase64 = false;
        if (!string.IsNullOrEmpty(CloudAuthManager.Instance.LocalProfileImageBase64))
        {
            try
            {
                byte[] imgBytes = Convert.FromBase64String(CloudAuthManager.Instance.LocalProfileImageBase64);
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(imgBytes);
                if (profilePreviewImage != null)
                {
                    profilePreviewImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
                loadedFromBase64 = true;
            }
            catch (Exception)
            {
                loadedFromBase64 = false;
            }
        }

        // Si no hay imagen base64 o hubo error, usar banco de imágenes
        if (!loadedFromBase64 && profilePreviewImage != null && availableProfileImages != null && availableProfileImages.Length > 0)
        {
            // Encontrar índice por clave
            int idx = 0;
            if (int.TryParse(data.ProfileImageKey.ToString(), out int parsed))
            {
                idx = Mathf.Clamp(parsed, 0, availableProfileImages.Length - 1);
            }
            currentImageIndex = idx;
            profilePreviewImage.sprite = availableProfileImages[currentImageIndex];
        }
    }

    private async void OnSaveButtonClicked()
    {
        // Obtener los nuevos valores de los campos
        string newDesc = descriptionField != null ? descriptionField.text : null;
        string newDate = birthDateField != null ? birthDateField.text : null;
        string newStatus = statusField != null ? statusField.text : null;
        string newImageKey = currentImageIndex.ToString();

        // Validación de la fecha de cumpleaños (permitir vacío o formato válido). Si es inválida, no se actualiza.
        bool dateIsValid = true;
        if (!string.IsNullOrEmpty(newDate))
        {
            if (!DateTime.TryParse(newDate, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out _))
            {
                dateIsValid = false;
            }
        }

        if (CloudAuthManager.Instance != null)
        {
            feedbackText?.gameObject.SetActive(true);
            feedbackText?.SetText("Guardando...");
            // Preparamos la fecha a guardar: si es inválida, la dejamos nula para mantener la anterior
            string dateToUse = dateIsValid ? newDate : null;
            // Actualiza datos básicos (descripción, fecha, estado, clave de imagen predefinida)
            await CloudAuthManager.Instance.UpdatePlayerProfile(newDesc, dateToUse, newStatus, newImageKey);

            // Gestión de la imagen de perfil: si se seleccionó una imagen externa, guardamos esa cadena base64.
            // En caso contrario, si existe actualmente una base64 guardada y no hemos seleccionado una nueva,
            // limpiamos la base64 para que se use la imagen predefinida elegida.
            if (newImageBase64 != null)
            {
                await CloudAuthManager.Instance.UpdatePlayerProfileImage(newImageBase64);
                newImageBase64 = null;
            }
            else if (!string.IsNullOrEmpty(CloudAuthManager.Instance.LocalProfileImageBase64))
            {
                // Solo limpiar si no se seleccionó nueva imagen externa
                await CloudAuthManager.Instance.UpdatePlayerProfileImage(null);
            }

            // Llamar RPC para actualizar datos en todos los clientes
            try
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.UpdatePlayerProfileDataServerRpc(newDesc, dateIsValid ? newDate : null, newStatus, newImageKey);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error al sincronizar perfil en red: {ex.Message}");
            }

            // Actualizar localmente las etiquetas de visualización y mostrar textos por defecto si están vacíos
            if (descriptionDisplay != null)
            {
                descriptionDisplay.text = string.IsNullOrWhiteSpace(newDesc) ? "Sin descripción" : newDesc;
            }
            if (birthDateDisplay != null)
            {
                birthDateDisplay.text = string.IsNullOrWhiteSpace(newDate) ? "Sin fecha" : newDate;
            }
            if (statusDisplay != null)
            {
                statusDisplay.text = string.IsNullOrWhiteSpace(newStatus) ? "Sin estado" : newStatus;
            }

            // Mostrar mensaje según validez de la fecha
            if (dateIsValid)
            {
                feedbackText?.SetText("Perfil guardado correctamente.");
            }
            else
            {
                feedbackText?.SetText("Perfil guardado. La fecha ingresada es inválida y no se ha modificado.");
            }

            // recargar datos para reflejar posibles ajustes desde Cloud Save
            LoadCurrentProfileData();
        }
    }

    private void OnChooseImageClicked()
    {
        // Si estamos en modo Android, se usa el botón separado para rotar imágenes predefinidas.
        // Por compatibilidad, si androidMode está activo y no hay dialogo nativo, rota manualmente a través del botón.
        if (androidMode)
        {
            // En Android no abrimos el diálogo nativo; rotamos a la siguiente imagen predefinida
            OnCycleImageClicked();
            return;
        }

        // Modo PC: intenta abrir un diálogo nativo para seleccionar una imagen
#if UNITY_STANDALONE && !UNITY_EDITOR
        try
        {
            // Filtrar por archivos de imagen
            var extensions = new[]
            {
                new ExtensionFilter("Image Files", "png", "jpg", "jpeg"),
                new ExtensionFilter("All Files", "*")
            };
            string[] paths = StandaloneFileBrowser.OpenFilePanel("Selecciona una imagen", "", extensions, false);
            if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
            {
            string path = paths[0];
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D originalTex = new Texture2D(2, 2);
            originalTex.LoadImage(bytes);
            // Escalar y comprimir la textura para ajustarse a los límites de Cloud Save (aprox. <10 KB)
            string base64 = ConvertTextureToBase64(originalTex);
            newImageBase64 = base64;
            // Utilizar la textura original para el preview, no la reducida
            if (profilePreviewImage != null)
            {
                profilePreviewImage.sprite = Sprite.Create(originalTex, new Rect(0, 0, originalTex.width, originalTex.height), new Vector2(0.5f, 0.5f));
            }
            return;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"No se pudo seleccionar imagen: {e.Message}");
        }
#endif
        // En el editor (y como fallback), usa OpenFilePanel de EditorUtility
#if UNITY_EDITOR
        string editorPath = EditorUtility.OpenFilePanel("Selecciona una imagen", "", "png,jpg,jpeg");
        if (!string.IsNullOrEmpty(editorPath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(editorPath);
                Texture2D originalTex = new Texture2D(2, 2);
                originalTex.LoadImage(bytes);
                string base64 = ConvertTextureToBase64(originalTex);
                newImageBase64 = base64;
                if (profilePreviewImage != null)
                {
                    profilePreviewImage.sprite = Sprite.Create(originalTex, new Rect(0, 0, originalTex.width, originalTex.height), new Vector2(0.5f, 0.5f));
                }
                return;
            }
            catch (Exception e)
            {
                Debug.LogError($"No se pudo leer la imagen: {e.Message}");
            }
        }
#endif
        // Si no se seleccionó archivo o no se soporta, no cambia la imagen de perfil
        // (la rotación de imágenes se realiza con el botón cycleImageButton).
    }
}
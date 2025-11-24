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

    // Flag para saber si el usuario ha cambiado la imagen predefinida usando el botón de rotación.
    // Si está en true al guardar y no se seleccionó una imagen externa, se debe limpiar la imagen base64 almacenada.
    private bool predefinedImageChanged = false;

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

        int maxDimension = 128;
        int origWidth = original.width;
        int origHeight = original.height;
        int targetWidth = origWidth;
        int targetHeight = origHeight;
        // Calcular escala conservando la relación de aspecto
        float maxOrigDim = Mathf.Max(origWidth, origHeight);
        if (maxOrigDim > maxDimension)
        {
            float ratio = (float)maxDimension / maxOrigDim;
            targetWidth = Mathf.Max(1, Mathf.RoundToInt(origWidth * ratio));
            targetHeight = Mathf.Max(1, Mathf.RoundToInt(origHeight * ratio));
        }

        // Crear RenderTexture para escalar
        RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
        Graphics.Blit(original, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D resized = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
        resized.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        resized.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        // Comprimir a JPG para reducir tamaño
        byte[] jpgBytes;
        try
        {
            jpgBytes = resized.EncodeToJPG(80);
        }
        catch
        {
            // Si no se soporta EncodeToJPG en esta plataforma, usar PNG
            jpgBytes = resized.EncodeToPNG();
        }

        // Convertir a base64
        string base64 = Convert.ToBase64String(jpgBytes);
        return base64;
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

        // Indicamos que el usuario ha cambiado de imagen predefinida
        predefinedImageChanged = true;
    }

    private void Start()
    {
        // Inicializa UI con datos cargados
        // Al iniciar, intentamos recargar datos desde la nube. Si todavía no hay sesión, LoadPlayerProgress
        // no devolverá nada y más adelante se actualizará vía OnSignInSuccess.
        _ = ReloadDataFromCloudAsync();

        // Suscribirnos al evento de inicio de sesión para refrescar la UI cuando el jugador se autentique
        if (CloudAuthManager.Instance != null)
        {
            CloudAuthManager.Instance.OnSignInSuccess -= OnAuthSuccessReload;
            CloudAuthManager.Instance.OnSignInSuccess += OnAuthSuccessReload;
        }

        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveButtonClicked);

        if (chooseImageButton != null)
            chooseImageButton.onClick.AddListener(OnChooseImageClicked);

        if (cycleImageButton != null)
            cycleImageButton.onClick.AddListener(OnCycleImageClicked);
    }

    private void OnDestroy()
    {
        // Nos desuscribimos del evento para evitar referencias colgantes
        if (CloudAuthManager.Instance != null)
        {
            CloudAuthManager.Instance.OnSignInSuccess -= OnAuthSuccessReload;
        }
    }

    private void OnAuthSuccessReload()
    {
        // Al producirse un login correcto, recargamos los datos desde Cloud Save
        _ = ReloadDataFromCloudAsync();
    }

    private void OnEnable()
    {
        // Cuando el panel se activa de nuevo, recarga los datos actuales y solicita datos a la nube.
        if (gameObject.activeSelf)
        {
            _ = ReloadDataFromCloudAsync();
        }
    }

    /// <summary>
    /// Carga datos del jugador desde Cloud Save (si existe información) y después actualiza la interfaz con esos datos. 
    /// Se utiliza un método separado para poder invocar funciones async en Start/OnEnable.
    /// </summary>
    private async Task ReloadDataFromCloudAsync()
    {
        try
        {
            if (CloudAuthManager.Instance != null)
            {
                await CloudAuthManager.Instance.LoadPlayerProgress();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error al cargar datos desde Cloud Save: {ex}");
        }
        // Actualizar la UI con los datos locales recién cargados
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

        if (CloudAuthManager.Instance != null)
        {
            feedbackText?.gameObject.SetActive(true);
            feedbackText?.SetText("Guardando...");

            // Validar la fecha introducida. Si no es válida, conservamos la fecha actual almacenada en la nube
            bool dateValid = true;
            if (!string.IsNullOrWhiteSpace(newDate))
            {
                if (!DateTime.TryParse(newDate, out _))
                {
                    dateValid = false;
                }
            }
            string dateToSave = dateValid ? newDate : CloudAuthManager.Instance.LocalPlayerData.BirthDate.ToString();

            // Actualiza datos básicos (descripción, fecha, estado, clave de imagen predefinida) en Cloud Save
            await CloudAuthManager.Instance.UpdatePlayerProfile(newDesc, dateToSave, newStatus, newImageKey);

            // Determinar la cadena base64 reducida (para la red) a enviar a los demás jugadores
            string imageBase64ToSend;
            if (newImageBase64 != null)
            {
                // Si el jugador ha seleccionado una imagen externa, guardamos la nueva base64 completa. 
                // CloudAuthManager se encargará de generar la versión reducida y asignarla a LocalPlayerData.ProfileImageBase64.
                await CloudAuthManager.Instance.UpdatePlayerProfileImage(newImageBase64);
                // Recuperamos la versión reducida recién generada para enviarla por la red
                imageBase64ToSend = CloudAuthManager.Instance.LocalPlayerData.ProfileImageBase64.ToString();
                newImageBase64 = null;
            }
            else
            {
                // Si no hay una nueva imagen seleccionada, sólo limpiamos la imagen almacenada si el usuario ha rotado las predefinidas
                if (predefinedImageChanged)
                {
                    // Al limpiar la imagen base64, se volverá a usar la imagen predefinida indicada por currentImageIndex
                    await CloudAuthManager.Instance.UpdatePlayerProfileImage(null);
                }
                // Utilizamos la versión reducida actualmente almacenada en LocalPlayerData para enviar por la red
                imageBase64ToSend = CloudAuthManager.Instance.LocalPlayerData.ProfileImageBase64.ToString();
            }

            // Llamar RPC para actualizar datos en todos los clientes, incluido el avatar personalizado en base64 reducido
            try
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.UpdatePlayerProfileDataServerRpc(newDesc, dateToSave, newStatus, newImageKey, imageBase64ToSend);
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
                birthDateDisplay.text = string.IsNullOrWhiteSpace(dateToSave) ? "Sin fecha" : dateToSave;
            }
            if (statusDisplay != null)
            {
                statusDisplay.text = string.IsNullOrWhiteSpace(newStatus) ? "Sin estado" : newStatus;
            }

            // Mostrar mensaje según validez de la fecha. Siempre se añade nota sobre tamaño de imagen
            if (!dateValid)
            {
                feedbackText?.SetText("Datos inválidos: revisa la fecha de cumpleaños. Si la imagen es mayor a 256x256, esta podría no guardarse.");
            }
            else
            {
                feedbackText?.SetText("Perfil guardado correctamente. Si la imagen es mayor a 256x256, esta podría no guardarse.");
            }
            // Recargar datos para reflejar posibles ajustes desde Cloud Save
            LoadCurrentProfileData();

            // Reiniciar el indicador de cambio de imagen predefinida
            predefinedImageChanged = false;
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
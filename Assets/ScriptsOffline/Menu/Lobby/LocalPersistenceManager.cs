using UnityEngine;
using Newtonsoft.Json; // Asegúrate de tener la librería JSON o usa JsonUtility de Unity

/// <summary>
/// Reemplazo local del CloudAuthManager. 
/// Se encarga EXCLUSIVAMENTE de guardar y cargar datos persistentes (Nivel, XP, Skins desbloqueadas)
/// en el disco local del dispositivo usando PlayerPrefs.
/// </summary>
public class LocalPersistenceManager : MonoBehaviour
{
    public static LocalPersistenceManager Instance { get; private set; }

    private const string SAVE_KEY = "LOCAL_PLAYER_SAVE_DATA";

    // Estructura simple para guardar en disco
    [System.Serializable]
    public class PersistentData
    {
        public int Level = 1;
        public int CurrentXP = 0;
        public string Username = "Player";
        // Aquí podrías añadir arrays de cosas desbloqueadas
    }

    public PersistentData CurrentData { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SaveData(int newLevel, int newXp, string newName = null)
    {
        CurrentData.Level = newLevel;
        CurrentData.CurrentXP = newXp;
        if (!string.IsNullOrEmpty(newName)) CurrentData.Username = newName;

        string json = JsonUtility.ToJson(CurrentData);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();

        Debug.Log("Progreso guardado localmente.");
    }

    private void LoadData()
    {
        if (PlayerPrefs.HasKey(SAVE_KEY))
        {
            string json = PlayerPrefs.GetString(SAVE_KEY);
            CurrentData = JsonUtility.FromJson<PersistentData>(json);
        }
        else
        {
            // Datos por defecto si es la primera vez
            CurrentData = new PersistentData();
        }
        Debug.Log($"Datos cargados. Nivel: {CurrentData.Level}");
    }

    // Método de utilidad para borrar progreso (útil para pruebas)
    public void ResetProgress()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        CurrentData = new PersistentData();
    }
}
using UnityEngine;

public class MobileFramerateSetter : MonoBehaviour
{
    void Awake()
    {
        // Importante: si vSync está activo, targetFrameRate se ignora
        QualitySettings.vSyncCount = 0;

#if UNITY_ANDROID || UNITY_IOS
        // En versiones nuevas de Unity:
        // float refresh = (float)Screen.currentResolution.refreshRateRatio.value;
        // En versiones más antiguas:
        int refresh = Screen.currentResolution.refreshRate;

        int target = -1; // -1 = valor por defecto de la plataforma

        if (refresh >= 120)
        {
            target = 120;
        }
        else if (refresh >= 60)
        {
            target = 60;
        }
        else if (refresh > 0)
        {
            // Si la pantalla es de menos de 60 Hz (p.ej. 50), usa ese valor
            target = refresh;
        }

        Application.targetFrameRate = target;
#else
        // En PC/Editor: deja el valor por defecto o pon lo que quieras
        Application.targetFrameRate = -1;  // o 60, 144, etc.
#endif
    }
}
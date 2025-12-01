using UnityEngine;

public class CameraAspectRatio : MonoBehaviour
{
    // Define tu ratio objetivo (16:9 = 1.7777...)
    private float targetAspect = 16.0f / 9.0f;

    void Start()
    {
        // Calculamos el ratio actual de la pantalla del dispositivo
        float windowAspect = (float)Screen.width / (float)Screen.height;

        // Calculamos cuánto tenemos que escalar la altura
        float scaleHeight = windowAspect / targetAspect;

        Camera camera = GetComponent<Camera>();

        // Si el ratio actual es menor al objetivo (pantalla más alta/estrecha, ej: iPad o Móvil vertical)
        // Añadimos barras arriba y abajo (Letterbox)
        if (scaleHeight < 1.0f)
        {
            Rect rect = camera.rect;

            rect.width = 1.0f;
            rect.height = scaleHeight;
            rect.x = 0;
            rect.y = (1.0f - scaleHeight) / 2.0f;

            camera.rect = rect;
        }
        else // Si es mayor (pantalla más ancha, ej: Móviles modernos largos 20:9)
        {
            // Añadimos barras a los lados (Pillarbox)
            float scaleWidth = 1.0f / scaleHeight;

            Rect rect = camera.rect;

            rect.width = scaleWidth;
            rect.height = 1.0f;
            rect.x = (1.0f - scaleWidth) / 2.0f;
            rect.y = 0;

            camera.rect = rect;
        }
    }
}
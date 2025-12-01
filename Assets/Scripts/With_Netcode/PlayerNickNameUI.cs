using UnityEngine;
using TMPro;

public class PlayerNicknameUI : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI nicknameText;

    /// <summary>
    /// Método llamado por OfflinePlayerSpawner para asignar nombre y nivel en local.
    /// </summary>
    public void SetLocalInfo(string username, int level)
    {
        if (nicknameText != null)
        {
            nicknameText.text = $"[Nvl {level}] {username}";
        }
    }
}
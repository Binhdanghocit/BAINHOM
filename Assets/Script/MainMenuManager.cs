using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("--- Scene Configuration ---")]
    [Tooltip("Nhập chính xác tên Scene Triển lãm Tranh Đông Hồ của bạn")]
    public string gallerySceneName = "ExhibitionScene";

    // Gọi khi nhấn nút Play
    public void PlayGame()
    {
        SceneManager.LoadScene(gallerySceneName);
    }

    // Gọi khi nhấn nút Quit
    public void QuitGame()
    {
        Debug.Log("Đã thoát triển lãm!");
        Application.Quit();
    }
}

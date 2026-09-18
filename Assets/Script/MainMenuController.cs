using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    // Hàm này gọi khi nhấn nút Play
    public void PlayGame()
    {
        // Nhập đúng tên Scene màn chơi chính
        SceneManager.LoadScene("Tranh Đông Hồ");
    }

    // Hàm này gọi khi nhấn nút Quit
    public void QuitGame()
    {
        Debug.Log("Đã thoát Game!"); // Hiển thị trong Editor để kiểm tra
        Application.Quit(); // Chỉ hoạt động khi đã Build thành file .exe / .apk
    }
}

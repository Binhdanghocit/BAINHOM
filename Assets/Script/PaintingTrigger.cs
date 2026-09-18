using UnityEngine;
public class PaintingTrigger : MonoBehaviour

{

    private PaintingInfo paintingInfo; // Khai báo để lấy dữ liệu tranh

    private bool isPlayerNearby = false;



    private void Start()

    {

        // Tự động lấy component PaintingInfo nằm trên cùng GameObject bức tranh này

        paintingInfo = GetComponent<PaintingInfo>();



        if (paintingInfo == null)

        {

            Debug.LogError("Chưa gắn script PaintingInfo trên bức tranh này: " + gameObject.name);

        }

    }



    private void OnTriggerEnter(Collider other)

    {

        if (other.CompareTag("Player"))

        {

            isPlayerNearby = true;

        }

    }



    private void OnTriggerExit(Collider other)

    {

        if (other.CompareTag("Player"))

        {

            isPlayerNearby = false;



            // Tự động đóng Popup qua Manager khi đi xa

            if (PaintingUIManager.Instance != null)

            {

                PaintingUIManager.Instance.ClosePopup();

            }

        }

    }



    private void OnMouseDown()

    {

        // Chỉ hoạt động khi người chơi ĐANG ĐỨNG GẦN và có đủ dữ liệu

        if (isPlayerNearby && paintingInfo != null && PaintingUIManager.Instance != null)

        {

            // Bật Popup và TRUYỀN DỮ LIỆU tranh vào UIManager

            PaintingUIManager.Instance.ShowPaintingInfo(paintingInfo);

        }

    }
}
using System.Collections.Generic;
using UnityEngine;

// Một nguồn sự thật duy nhất để nhận diện người chơi, dùng chung cho mọi system
// (InteractableOutline, PaintingTrigger, NPCInteractable, InfoPodiumTrigger...)
// Tránh mỗi script tự viết logic riêng -> lệch pha, thiếu đồng bộ.
public static class PlayerDetector
{
    // Các "gốc" người chơi đã đăng ký (desktop rig, VR rig) bởi ViewModeController
    private static readonly List<Transform> roots = new List<Transform>(4);

    // Đăng ký trước mỗi scene load / khi biết rig. Gọi lại khi có rig mới.
    public static void RegisterRoot(Transform root)
    {
        if (root == null) return;
        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i] == root) return;
        }
        roots.Add(root);
    }

    public static void ClearRoots()
    {
        roots.Clear();
    }

    // Trả về true nếu collider này thuộc về người chơi
    public static bool IsPlayer(Collider other)
    {
        return other != null && IsPlayer(other.transform);
    }

    public static bool IsPlayer(Transform t)
    {
        if (t == null) return false;

        // 1. Ưu tiên Tag "Player" (chuẩn, gán trực tiếp trên player)
        if (t.CompareTag("Player")) return true;

        // 2. Đi lên cha: trùng với rig đã đăng ký (VR hands, desktop child...)
        Transform p = t;
        int depth = 0;
        while (p != null && depth < 10)
        {
            for (int i = 0; i < roots.Count; i++)
            {
                if (p == roots[i]) return true;
            }

            // 3. Fallback desktop: rig có CharacterController / PlayerController
            if (p.GetComponent(typeof(CharacterController)) != null
                || p.GetComponent(typeof(PlayerController)) != null)
            {
                return true;
            }

            p = p.parent;
            depth++;
        }

        return false;
    }
}

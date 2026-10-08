using System;
using UnityEngine;

// All input paths use physical colliders, player distance and the same occlusion rules.
public static class InteractionTargetResolver
{
    public readonly struct Target
    {
        public readonly Component owner;
        public readonly Collider collider;
        public readonly float distance;
        public Target(Component owner, Collider collider, float distance)
        { this.owner = owner; this.collider = collider; this.distance = distance; }

        public void Activate()
        {
            if (owner is NPCInteractable npc) npc.TriggerDialogue();
            else if (owner is DoorMenuTrigger door) door.ToggleDoorMenu();
            else if (owner is PaintingTrigger painting) painting.ToggleInteract();
            else if (owner is PaintingInfo info && PaintingUIManager.Instance != null)
                PaintingUIManager.Instance.ShowPaintingInfo(info);
            else if (owner is MinigameTrigger workshop) workshop.ToggleMinigame();
        }
    }

    private static bool IsSelf(Collider collider, Transform player)
        => PlayerDetector.IsPlayer(collider) || (player != null && collider.transform.IsChildOf(player));

    private static Component Owner(Collider collider)
    {
        var npc = collider.GetComponentInParent<NPCInteractable>();
        if (npc != null)
            return npc.isActiveAndEnabled && npc.IsInteractionCollider(collider)
                && (!collider.isTrigger || npc.interactionCollider == collider) ? npc : null;
        var painting = collider.GetComponentInParent<PaintingTrigger>();
        if (painting != null) return painting.isActiveAndEnabled ? painting : null;
        var info = collider.GetComponentInParent<PaintingInfo>();
        if (info == null) info = collider.GetComponentInChildren<PaintingInfo>();
        if (info != null) return info.isActiveAndEnabled ? info : null;
        var door = collider.GetComponentInParent<DoorMenuTrigger>();
        if (door != null) return door.isActiveAndEnabled ? door : null;
        var workshop = collider.GetComponentInParent<MinigameTrigger>();
        return workshop != null && workshop.isActiveAndEnabled ? workshop : null;
    }

    private static float Allowed(Component owner, float distance)
        => owner is DoorMenuTrigger door ? door.maxInteractDistance : distance;

    public static bool TryRay(Ray ray, Transform player, float distance, out Target target)
    {
        target = default;
        Vector3 origin = player != null ? player.position : ray.origin;
        float range = Vector3.Distance(ray.origin, origin) + DoorMenuTrigger.GetRaycastDistance(distance);
        var hits = Physics.RaycastAll(ray, range, ~0, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (IsSelf(hit.collider, player)) continue;
            Component owner = Owner(hit.collider);
            if (owner != null)
            {
                float actual = Vector3.Distance(origin, hit.point);
                if (actual > Allowed(owner, distance)) return false;
                target = new Target(owner, hit.collider, actual);
                return true;
            }
            // Proximity volumes are transparent, but an unselectable NPC body still blocks.
            if (!hit.collider.isTrigger) return false;
        }
        return false;
    }

    public static bool TryNearby(Transform player, Vector3 forward, float distance, out Target target)
    {
        target = default;
        if (player == null || forward.sqrMagnitude < 0.0001f) return false;
        Vector3 eye = player.position + Vector3.up * 1.2f;
        float closest = float.PositiveInfinity;
        foreach (var collider in Physics.OverlapSphere(player.position,
                     DoorMenuTrigger.GetRaycastDistance(distance), ~0, QueryTriggerInteraction.Collide))
        {
            if (IsSelf(collider, player)) continue;
            Component owner = Owner(collider);
            if (owner == null) continue;
            // Do not select the NPC's large proximity sphere, even in legacy prefabs.
            if (owner is NPCInteractable && collider.isTrigger) continue;
            float actual = Vector3.Distance(player.position, collider.ClosestPoint(player.position));
            if (actual > Allowed(owner, distance) || actual > closest) continue;
            Vector3 aim = collider.bounds.center;
            if (Mathf.Abs(aim.y - eye.y) > 1.5f) continue;
            Vector3 direction = aim - eye;
            if (Vector3.Angle(forward, direction) > 60f) continue;
            if (!HasLineOfSight(eye, aim, collider, player)) continue;
            // Equal distances are stable across PhysX enumeration and target types.
            if (actual == closest && target.owner != null
                && string.CompareOrdinal(HierarchyPath(owner.transform), HierarchyPath(target.owner.transform)) >= 0) continue;
            closest = actual;
            target = new Target(owner, collider, actual);
        }
        return target.owner != null;
    }

    private static bool HasLineOfSight(Vector3 eye, Vector3 aim, Collider candidate, Transform player)
    {
        var hits = Physics.RaycastAll(eye, aim - eye, Vector3.Distance(eye, aim) + 0.01f,
            ~0, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (IsSelf(hit.collider, player)) continue;
            if (hit.collider == candidate) return true;
            if (!hit.collider.isTrigger) return false;
        }
        // The eye may already be inside this collider (raycasts do not return it).
        return candidate.ClosestPoint(eye) == eye;
    }

    private static string HierarchyPath(Transform transform)
    {
        string path = transform.GetSiblingIndex().ToString("D6");
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.GetSiblingIndex().ToString("D6") + "/" + path;
        }
        return transform.gameObject.scene.path + "/" + path;
    }

    public static bool TrySelect(Ray ray, Transform player, float distance, bool xr,
        bool hasControllerRay, out Target target)
    {
        target = default;
        if (xr && !hasControllerRay) return false;
        if (TryRay(ray, player, distance, out target)) return true;
        return !xr && TryNearby(player, ray.direction, distance, out target);
    }
}

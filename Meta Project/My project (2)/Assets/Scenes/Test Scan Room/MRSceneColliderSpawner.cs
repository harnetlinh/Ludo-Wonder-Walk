using Meta.XR.MRUtilityKit;
using UnityEngine;

public class MRSceneColliderSpawner : MonoBehaviour
{
    [SerializeField] private MRUK mruk;
    [SerializeField] private bool removeSceneMeshColliders = true;
    [SerializeField] private float planeThickness = 0.05f;
    [SerializeField] private float minDimension = 0.01f;

    private void Awake()
    {
        if (!mruk)
        {
            mruk = FindObjectOfType<MRUK>();
        }
    }

    private void OnEnable()
    {
        if (mruk != null)
        {
            mruk.SceneLoadedEvent.AddListener(HandleSceneLoaded);
            if (mruk.IsInitialized)
            {
                HandleSceneLoaded();
            }
        }
    }

    private void OnDisable()
    {
        if (mruk != null)
        {
            mruk.SceneLoadedEvent.RemoveListener(HandleSceneLoaded);
        }
    }

    private void HandleSceneLoaded()
    {
        var room = mruk != null ? mruk.GetCurrentRoom() : null;
        if (room == null)
        {
            return;
        }

        if (removeSceneMeshColliders)
        {
            foreach (var meshCollider in room.GetComponentsInChildren<MeshCollider>(true))
            {
                Destroy(meshCollider);
            }
        }

        foreach (var anchor in room.Anchors)
        {
            if (anchor == null)
            {
                continue;
            }

            if (anchor.VolumeBounds.HasValue)
            {
                EnsureVolumeCollider(anchor.gameObject, anchor.VolumeBounds.Value);
            }

            if (anchor.PlaneRect.HasValue)
            {
                EnsurePlaneCollider(anchor, anchor.PlaneRect.Value);
            }
        }
    }

    private void EnsurePlaneCollider(MRUKAnchor anchor, Rect planeRect)
    {
        var go = anchor.gameObject;
        var box = go.GetComponent<BoxCollider>();
        if (!box)
        {
            box = go.AddComponent<BoxCollider>();
        }

        var size = new Vector3(
            Mathf.Max(planeRect.size.x, minDimension),
            Mathf.Max(planeRect.size.y, minDimension),
            Mathf.Max(planeThickness, minDimension));

        var center = new Vector3(planeRect.center.x, planeRect.center.y, 0f);

        box.size = size;
        box.center = center;
    }

    private void EnsureVolumeCollider(GameObject go, Bounds bounds)
    {
        var box = go.GetComponent<BoxCollider>();
        if (!box)
        {
            box = go.AddComponent<BoxCollider>();
        }

        var size = bounds.size;
        size.x = Mathf.Max(size.x, minDimension);
        size.y = Mathf.Max(size.y, minDimension);
        size.z = Mathf.Max(size.z, minDimension);

        box.size = size;
        box.center = bounds.center;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class CoinRushCoinSpawner : MonoBehaviour
{
    public const int CoinValue = 10;
    public const int CoinCount = 15;
    [SerializeField] private BoxCollider floor;
    [SerializeField] private Transform player;
    [SerializeField] private GameObject coinVisualPrefab;
    [SerializeField] private CoinRushGameManager gameManager;
    private readonly List<Vector3> positions = new List<Vector3>();
    private readonly Collider[] overlaps = new Collider[64];
    private int score;
    private float nextRetry;
    [SerializeField] private Transform[] courseSlots;
    private readonly HashSet<int> collectedSlots = new HashSet<int>();
    private readonly HashSet<int> committedSlots = new HashSet<int>();
    private bool IsLinear => courseSlots != null && courseSlots.Length > 0;

    public void ConfigureLinear(Transform character, GameObject prefab, Transform[] slots)
    {
        player = character;
        coinVisualPrefab = prefab;
        courseSlots = slots;
        collectedSlots.Clear();
        committedSlots.Clear();
    }

    public void CommitCheckpoint(int index)
    {
        committedSlots.Clear();
        foreach (int slot in collectedSlots) committedSlots.Add(slot);
    }

    public void RestoreCheckpoint(int index)
    {
        collectedSlots.Clear();
        foreach (int slot in committedSlots) collectedSlots.Add(slot);
        ClearCoins();
        Replenish();
    }

    private void ClearCoins()
    {
        foreach (CoinRushCoin coin in GetComponentsInChildren<CoinRushCoin>(true))
        {
            coin.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(coin.gameObject);
            else DestroyImmediate(coin.gameObject);
        }
    }

    public int Score => gameManager != null ? gameManager.Score : score;
    public bool CanCollect => !Application.isPlaying ||
        (Time.timeScale > 0f && (gameManager == null || gameManager.IsRunning));

    public void SetGameManager(CoinRushGameManager manager) => gameManager = manager;

    public void Configure(BoxCollider ground, Transform character, GameObject visualPrefab)
    {
        floor = ground;
        player = character;
        coinVisualPrefab = visualPrefab;
    }

    private void Start()
    {
        score = 0;
        if (coinVisualPrefab == null)
        {
            Debug.LogError("Coin Rush: execute Coin Rush > Criar Moedas para atribuir o prefab MAR.", this);
            return;
        }
        Populate();
        Debug.Log("Coin Rush: pontuacao = 0", this);
    }

    private void Update()
    {
        if (!CanCollect) return;
        if (Time.time < nextRetry) return;
        nextRetry = Time.time + 1f;
        Replenish();
    }

    public void Collect(CoinRushCoin coin)
    {
        if (!CanCollect || coin == null || coin.transform.parent != transform || !coin.TryConsume()) return;
        if (IsLinear && !collectedSlots.Add(coin.SlotIndex)) return;
        if (gameManager != null) gameManager.AwardCoin();
        else score += CoinValue;
        Debug.Log("Coin Rush: +10 pontos | Pontuacao acumulada: " + Score, this);
        Replenish();
    }

    public void ResetCoins()
    {
        score = 0;
        nextRetry = 0;
        collectedSlots.Clear();
        committedSlots.Clear();
        ClearCoins();
        Populate();
    }

    // Shared by the Editor generator and runtime. Only creates missing coins.
    public void Populate()
    {
        if (!IsLinear) BuildReachablePositions();
        Replenish();
    }

    private bool IsClear(Vector3 position)
    {
        PhysicsScene physics = gameObject.scene.GetPhysicsScene();
        Vector3 center = position + Vector3.up * 0.15f;
        int count = physics.OverlapCapsule(center - Vector3.up * 0.5f, center + Vector3.up * 0.5f,
            0.6f, overlaps, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider hit = overlaps[i];
            if (hit == floor || hit.gameObject.scene != gameObject.scene) continue;
            // Player may occupy the path grid; actual spawn avoids the player separately.
            if (player != null && hit.transform.IsChildOf(player)) continue;
            return false;
        }
        return true;
    }

    private bool HasPath(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.01f) return true;
        // Sweep a player-sized capsule to avoid crossing walls between valid grid points.
        Vector3 center = from + Vector3.up * 0.15f;
        RaycastHit[] hits = Physics.CapsuleCastAll(center - Vector3.up * 0.5f,
            center + Vector3.up * 0.5f, 0.6f, delta / length, length,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == floor || hit.collider.gameObject.scene != gameObject.scene) continue;
            if (player != null && hit.transform.IsChildOf(player)) continue;
            return false;
        }
        return true;
    }

    private void BuildReachablePositions()
    {
        positions.Clear();
        if (floor == null || player == null || !floor.enabled || !floor.gameObject.activeInHierarchy) return;
        Physics.SyncTransforms();
        var candidates = new List<Vector3>();
        Bounds bounds = floor.bounds;
        // Stay at least 1.5 units inside the floor edge; grid contains only floor-level positions.
        for (float x = bounds.min.x + 1.5f; x <= bounds.max.x - 1.5f; x += 2f)
        for (float z = bounds.min.z + 1.5f; z <= bounds.max.z - 1.5f; z += 2f)
        {
            if (!floor.Raycast(new Ray(new Vector3(x, bounds.max.y + 2f, z), Vector3.down),
                    out RaycastHit hit, 3f) || hit.normal.y < 0.95f) continue;
            Vector3 point = hit.point + Vector3.up * 1.05f;
            if (IsClear(point)) candidates.Add(point);
        }
        if (candidates.Count == 0) return;
        Vector3 anchor = new Vector3(player.position.x, bounds.max.y + 1.05f, player.position.z);
        int seed = -1;
        float best = float.PositiveInfinity;
        for (int i = 0; i < candidates.Count; i++)
        {
            float squaredDistance = (candidates[i] - anchor).sqrMagnitude;
            if (squaredDistance >= best || !HasPath(anchor, candidates[i])) continue;
            seed = i;
            best = squaredDistance;
        }
        if (seed < 0) return;
        var visited = new bool[candidates.Count];
        var queue = new Queue<int>();
        queue.Enqueue(seed);
        visited[seed] = true;
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            positions.Add(candidates[current]);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (visited[i] || (candidates[current] - candidates[i]).sqrMagnitude > 4.1f) continue;
                if (!HasPath(candidates[current], candidates[i])) continue;
                visited[i] = true;
                queue.Enqueue(i);
            }
        }
    }

    private void Replenish()
    {
        if (IsLinear)
        {
            if (!CanCollect || coinVisualPrefab == null) return;
            var occupiedSlots = new HashSet<int>();
            foreach (CoinRushCoin existingCoin in GetComponentsInChildren<CoinRushCoin>())
                occupiedSlots.Add(existingCoin.SlotIndex);
            for (int i = 0; i < courseSlots.Length; i++)
                if (!collectedSlots.Contains(i) && !occupiedSlots.Contains(i) && courseSlots[i] != null)
                    CreateCoin(courseSlots[i].position, i);
            return;
        }
        if (!CanCollect || floor == null || coinVisualPrefab == null || positions.Count == 0) return;
        CoinRushCoin[] existing = GetComponentsInChildren<CoinRushCoin>();
        var occupied = new List<Vector3>();
        foreach (CoinRushCoin coin in existing) occupied.Add(coin.transform.position);
        var order = new List<int>();
        for (int i = 0; i < positions.Count; i++) order.Add(i);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            int saved = order[i];
            order[i] = order[swap];
            order[swap] = saved;
        }
        Physics.SyncTransforms();
        for (int i = 0; i < positions.Count && occupied.Count < CoinCount; i++)
        {
            Vector3 point = positions[order[i]];
            if (player != null && (point - player.position).sqrMagnitude < 2.25f) continue;
            bool free = true;
            foreach (Vector3 other in occupied)
                if ((point - other).sqrMagnitude < 2.25f) { free = false; break; }
            if (!free || !IsClear(point)) continue;
            CreateCoin(point);
            occupied.Add(point);
        }
    }

    private void CreateCoin(Vector3 position, int slot = -1)
    {
        var root = new GameObject("Coin");
        root.SetActive(false);
        root.transform.SetParent(transform, false);
        root.transform.position = position;
        SphereCollider trigger = root.AddComponent<SphereCollider>();
        trigger.radius = 0.55f;
        trigger.isTrigger = true;
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        root.AddComponent<CoinRushCoin>().Configure(this, CreateVisual(root.transform), slot);
        root.SetActive(true);
    }

    public void ReplaceCoinVisuals()
    {
        if (coinVisualPrefab == null) return;
        foreach (CoinRushCoin coin in GetComponentsInChildren<CoinRushCoin>(true))
        {
            Transform previous = coin.Visual;
            if (previous != null)
            {
                previous.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(previous.gameObject);
                else DestroyImmediate(previous.gameObject);
            }
            coin.Configure(this, CreateVisual(coin.transform), coin.SlotIndex);
        }
    }

    private Transform CreateVisual(Transform parent)
    {
        Transform pivot = new GameObject("MAR Coin Visual").transform;
        pivot.SetParent(parent, false);
        GameObject model = Instantiate(coinVisualPrefab, pivot, false);
        model.name = coinVisualPrefab.name;
        model.transform.localPosition = Vector3.zero;
        // The model is visual only; collection belongs exclusively to the parent trigger.
        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }
        foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
            if (Application.isPlaying) Destroy(body);
            else DestroyImmediate(body);
        }
        Bounds bounds = VisualBounds(pivot);
        Vector3 size = bounds.size;
        Vector3 thinAxis = size.x <= size.y && size.x <= size.z ? Vector3.right
            : size.y <= size.z ? Vector3.up : Vector3.forward;
        // Point the disc's thin axis forward so the coin stands vertically.
        model.transform.localRotation = Quaternion.FromToRotation(thinAxis, Vector3.forward)
            * model.transform.localRotation;
        bounds = VisualBounds(pivot);
        float diameter = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (diameter > 0.0001f)
        {
            float scale = 0.8f / diameter;
            model.transform.localScale *= scale;
            model.transform.localPosition -= bounds.center * scale;
        }
        return pivot;
    }

    private static Bounds VisualBounds(Transform pivot)
    {
        Bounds result = new Bounds();
        bool first = true;
        foreach (MeshFilter filter in pivot.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds local = filter.sharedMesh.bounds;
            Matrix4x4 matrix = pivot.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = matrix.MultiplyPoint3x4(corner);
                if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                else result.Encapsulate(point);
            }
        }
        return result;
    }
}

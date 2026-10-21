using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public sealed class CoinRushCoin : MonoBehaviour
{
    [SerializeField] private CoinRushCoinSpawner owner;
    [SerializeField] private Transform visual;
    [SerializeField] private Quaternion visualRotation = Quaternion.identity;
    private bool collected;
    private float phase;
    public Transform Visual => visual;
    [SerializeField] private int slotIndex = -1;
    public int SlotIndex => slotIndex;

    public void Configure(CoinRushCoinSpawner spawner, Transform disc, int slot = -1)
    {
        owner = spawner;
        visual = disc;
        visualRotation = disc.localRotation;
        slotIndex = slot;
    }

    private void Awake()
    {
        phase = Random.Range(0f, Mathf.PI * 2f);
        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.55f;
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void Update()
    {
        if (visual == null) return;
        visual.localPosition = Vector3.up * (Mathf.Sin(Time.time * 2f + phase) * 0.1f);
        visual.localRotation = Quaternion.AngleAxis(Time.time * 100f + phase * Mathf.Rad2Deg, Vector3.up)
            * visualRotation;
    }

    private void OnTriggerEnter(Collider other) => TryCollect(other);
    private void OnTriggerStay(Collider other) => TryCollect(other);

    private void TryCollect(Collider other)
    {
        if (collected || owner == null || !owner.CanCollect) return;
        CoinRushPlayerController player = other.GetComponentInParent<CoinRushPlayerController>();
        if (player == null || player.gameObject.scene != gameObject.scene) return;
        owner.Collect(this);
    }

    public bool TryConsume()
    {
        if (collected || !gameObject.activeInHierarchy) return false;
        collected = true;
        GetComponent<SphereCollider>().enabled = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }
}

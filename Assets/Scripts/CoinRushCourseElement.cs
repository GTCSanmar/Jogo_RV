using UnityEngine;

// Authored by the complete-game generator; all references belong to the Coin Rush scene.
public sealed class CoinRushCourseElement : MonoBehaviour
{
    public enum ElementKind { Safezone, Hazard, Finish, DisappearingPlatform }
    public enum MotionKind { None, Lateral, Rotate, Swing, Timed }
    public MotionKind motion;
    public ElementKind kind;
    public CoinRushGameManager manager;
    public int checkpointIndex;
    public float lateralTravel;
    public float period = 3f;
    public float collapseDelay = 0.85f;
    public float swingAngle = 60f;
    [Range(0.05f, 0.8f)] public float activeFraction = 0.3f;
    public float phaseOffset;
    private Vector3 origin;
    private Quaternion initialRotation;
    private Rigidbody body;
    private float elapsed;
    private float collapseTime = -1f;
    private bool initialized;

    private void Initialize()
    {
        if (initialized) return;
        origin = transform.position;
        initialRotation = transform.rotation;
        body = GetComponent<Rigidbody>();
        initialized = true;
    }

    private void Awake() => Initialize();

    private void FixedUpdate()
    {
        if (manager == null || !manager.IsRunning) return;
        elapsed += Time.fixedDeltaTime;
        float cycle = (elapsed + phaseOffset) / Mathf.Max(0.5f, period);
        if (motion == MotionKind.Lateral || (motion == MotionKind.None && lateralTravel > 0))
        {
            Vector3 position = origin + Vector3.right * (Mathf.Sin(cycle * 2f * Mathf.PI) * lateralTravel);
            if (body != null) body.MovePosition(position);
            else transform.position = position;
        }
        else if (motion == MotionKind.Rotate || motion == MotionKind.Swing)
        {
            Quaternion rotation = initialRotation * (motion == MotionKind.Rotate
                ? Quaternion.Euler(0, cycle * 360f, 0)
                : Quaternion.Euler(0, 0, Mathf.Sin(cycle * 2 * Mathf.PI) * swingAngle));
            if (body != null) body.MoveRotation(rotation);
            else transform.rotation = rotation;
        }
        else if (motion == MotionKind.Timed)
            SetVisibleAndSolid(Mathf.Repeat(cycle, 1f) < activeFraction);
    }

    private void Update()
    {
        if (manager == null || !manager.IsRunning || collapseTime < 0) return;
        collapseTime -= Time.deltaTime;
        if (collapseTime > 0) return;
        foreach (Collider item in GetComponentsInChildren<Collider>()) item.enabled = false;
        foreach (Renderer item in GetComponentsInChildren<Renderer>()) item.enabled = false;
        collapseTime = -1;
    }

    public void StepOn()
    {
        if (kind != ElementKind.DisappearingPlatform || manager == null || !manager.IsRunning || collapseTime >= 0) return;
        collapseTime = collapseDelay;
    }

    private bool IsPlayer(Collider other) => other.GetComponentInParent<CoinRushPlayerController>() != null
        && other.gameObject.scene == gameObject.scene;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other) || manager == null || !manager.IsRunning) return;
        if (kind == ElementKind.Safezone) manager.EnterSafezone(checkpointIndex);
        else if (kind == ElementKind.Hazard) manager.Die();
        else if (kind == ElementKind.Finish) manager.CompleteCourse();
    }

    private void OnTriggerStay(Collider other)
    {
        // A timed trap becoming active while touching the player must also be lethal.
        if (kind == ElementKind.Hazard && manager != null && manager.IsRunning && IsPlayer(other))
            manager.Die();
    }

    private void SetVisibleAndSolid(bool value)
    {
        foreach (Collider item in GetComponentsInChildren<Collider>(true)) item.enabled = value;
        foreach (Renderer item in GetComponentsInChildren<Renderer>(true)) item.enabled = value;
    }

    private void OnTriggerExit(Collider other)
    {
        if (kind == ElementKind.Safezone && IsPlayer(other) && manager != null)
            manager.ExitSafezone(checkpointIndex);
    }

    public void ResetElement()
    {
        Initialize();
        elapsed = 0;
        collapseTime = -1;
        transform.position = origin;
        transform.rotation = initialRotation;
        if (body != null) { body.position = origin; body.rotation = initialRotation; }
        if (kind == ElementKind.DisappearingPlatform || motion == MotionKind.Timed)
        {
            SetVisibleAndSolid(true);
        }
    }
}

using UnityEngine;

/// <summary>
/// NPC "Coward" (payaso): huye del jugador con Flee cuando se acerca, y vuelve
/// a Wander cuando la distancia es segura. 
public class CowardNPC : MonoBehaviour
{
    // ============================================================
    // PARÁMETROS DE MOVIMIENTO
    // ============================================================
    [Header("Movimiento")]
    public float maxSpeed = 2.5f;
    public float maxForce = 10f;

    // ============================================================
    // PARÁMETROS DEL WANDER
    // ============================================================
    [Header("Wander")]
    public float wanderRadius = 1.5f;
    public float wanderDistance = 2f;
    public float wanderJitter = 30f;

    // ============================================================
    // DETECCIÓN DEL JUGADOR
    // ============================================================
    [Header("Detección del jugador")]
    [Tooltip("Referencia al transform del jugador.")]
    public Transform player;

    [Tooltip("Distancia a la que empieza a huir (Wander → Flee).")]
    public float fleeRadius = 4f;

    [Tooltip("Distancia a la que deja de huir (Flee → Wander). Mayor que fleeRadius.")]
    public float safeRadius = 7f;

    // ============================================================
    // ESTADO INTERNO
    // ============================================================
    private Vector2 velocity;
    private float wanderAngle;
    private SteeringAvoidance avoidance;

    public enum State { Wander, Flee }
    public State state = State.Wander;

    // ============================================================
    // INICIALIZACIÓN
    // ============================================================
    void Start()
    {
        wanderAngle = Random.Range(0f, 360f);
        velocity = Random.insideUnitCircle.normalized * maxSpeed;
        avoidance = GetComponent<SteeringAvoidance>();

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    // ============================================================
    // BUCLE PRINCIPAL
    // ============================================================
    void Update()
    {
        if (player == null) return;

        UpdateState();
        Vector2 force = RunState();

        if (avoidance != null)
            force += avoidance.GetAvoidanceForce();

        ApplyForce(force);
        Move();

        if (avoidance != null)
        {
            avoidance.SetVelocity(velocity);
            if (avoidance.CheckAndUnstick())
            {
                Vector2 escape = avoidance.GetEscapeDirection();
                velocity = escape * maxSpeed;
                wanderAngle = Mathf.Atan2(escape.y, escape.x) * Mathf.Rad2Deg;
            }
        }
    }

    // ============================================================
    // MÁQUINA DE ESTADOS
    // ============================================================
    /// <summary>
    /// Cambia entre Wander y Flee con histéresis: entra en Flee a fleeRadius
    /// y no vuelve a Wander hasta que la distancia supera safeRadius.
    /// </summary>
    void UpdateState()
    {
        float dist = Vector2.Distance(transform.position, player.position);

        switch (state)
        {
            case State.Wander:
                if (dist <= fleeRadius) state = State.Flee;
                break;

            case State.Flee:
                if (dist >= safeRadius) state = State.Wander;
                break;
        }
    }

    Vector2 RunState()
    {
        return state == State.Flee ? Flee(player.position) : Wander();
    }

    // ============================================================
    // COMPORTAMIENTOS
    // ============================================================
    /// <summary>
    /// Flee: genera una velocidad deseada alejándose del objetivo y devuelve
    /// la diferencia con la velocidad actual.
    /// </summary>
    Vector2 Flee(Vector2 target)
    {
        Vector2 desired = ((Vector2)transform.position - target).normalized * maxSpeed;
        return desired - velocity;
    }

    /// <summary>
    /// Wander
    /// </summary>
    Vector2 Wander()
    {
        Vector2 circleCenter = velocity.normalized * wanderDistance;
        if (velocity.sqrMagnitude < 0.01f)
            circleCenter = (Vector2)transform.right * wanderDistance;

        Vector2 displacement = new Vector2(
            Mathf.Cos(wanderAngle * Mathf.Deg2Rad),
            Mathf.Sin(wanderAngle * Mathf.Deg2Rad)
        ) * wanderRadius;

        wanderAngle += Random.Range(-1f, 1f) * wanderJitter * Time.deltaTime * 60f;

        return circleCenter + displacement;
    }

    // ============================================================
    // FÍSICA Y MOVIMIENTO
    // ============================================================
    void ApplyForce(Vector2 force)
    {
        if (force.magnitude > maxForce)
            force = force.normalized * maxForce;

        velocity += force * Time.deltaTime;

        if (velocity.magnitude > maxSpeed)
            velocity = velocity.normalized * maxSpeed;
    }

    void Move()
    {
        transform.position += (Vector3)(velocity * Time.deltaTime);
        
    }

    // ============================================================
    // DEBUG
    // ============================================================
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;      // Huida = rojo
        Gizmos.DrawWireSphere(transform.position, fleeRadius);

        Gizmos.color = Color.green;    // Zona segura = verde
        Gizmos.DrawWireSphere(transform.position, safeRadius);
    }
}
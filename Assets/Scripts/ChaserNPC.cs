using UnityEngine;

/// <summary>
/// NPC "Chaser" (ninja): persigue al jugador con Seek cuando entra en su radio
/// de detección, hace Arrive al llegar a su posición, y vuelve a Wander si el
/// jugador se aleja lo suficiente.
/// </summary>
public class ChaserNPC : MonoBehaviour
{
    // ============================================================
    // PARÁMETROS DE MOVIMIENTO
    // ============================================================
    [Header("Movimiento")]
    public float maxSpeed = 3f;    // Un poco más rápido que el jugador
    public float maxForce = 12f;   // Reacciona rápido para poder girar

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
    [Tooltip("Referencia al transform del jugador. Si está vacío se busca por tag.")]
    public Transform player;

    [Tooltip("Distancia a la que empieza a perseguir (Wander → Seek).")]
    public float detectionRadius = 4f;

    [Tooltip("Distancia a la que deja de perseguir (Seek → Wander). Mayor que detectionRadius.")]
    public float loseRadius = 6f;

    [Tooltip("Distancia a la que cambia de Seek a Arrive (frenado suave).")]
    public float arriveRadius = 1.2f;

    [Tooltip("Radio dentro del cual empieza a frenar en Arrive.")]
    public float slowingRadius = 1.5f;

    [Tooltip("Distancia mínima: por debajo de esto, se detiene completamente.")]
    public float stopRadius = 0.2f;

    // ============================================================
    // ESTADO INTERNO
    // ============================================================
    private Vector2 velocity;
    private float wanderAngle;
    private SteeringAvoidance avoidance;

    // Estados posibles del NPC. Lo público permite verlo desde el Inspector.
    public enum State { Wander, Seek, Arrive }
    public State state = State.Wander;

    // ============================================================
    // INICIALIZACIÓN
    // ============================================================
    void Start()
    {
        wanderAngle = Random.Range(0f, 360f);
        velocity = Random.insideUnitCircle.normalized * maxSpeed;
        avoidance = GetComponent<SteeringAvoidance>();

        // Si no se asignó el jugador en el Inspector, lo buscamos por tag
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
        if (player == null) return;   // Sin jugador no podemos hacer nada

        // 1) Decidir en qué estado estamos según la distancia al jugador
        UpdateState();

        // 2) Calcular la fuerza según el estado actual
        Vector2 force = RunState();

        // 3) Sumar evasión de obstáculos
        if (avoidance != null)
            force += avoidance.GetAvoidanceForce();

        ApplyForce(force);
        Move();

        // 4) Anti-atasco
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
    /// Cambia entre Wander / Seek / Arrive según la distancia al jugador.
    /// Usa histéresis (umbrales distintos para entrar y salir de cada estado)
    /// para evitar que el NPC parpadee entre estados cuando el jugador está
    /// justo en el límite.
    /// </summary>
    void UpdateState()
    {
        float dist = Vector2.Distance(transform.position, player.position);

        switch (state)
        {
            // --- WANDER ---
            // Si el jugador entra en el radio de detección, perseguimos
            case State.Wander:
                if (dist <= detectionRadius) state = State.Seek;
                break;

            // --- SEEK ---
            case State.Seek:
                // Si se aleja mucho, volvemos a wander
                if (dist > loseRadius) state = State.Wander;
                // Si llegamos cerca, pasamos a Arrive (frenado suave)
                else if (dist <= arriveRadius) state = State.Arrive;
                break;

            // --- ARRIVE ---
            case State.Arrive:
                // Si se aleja mucho, volvemos a wander
                if (dist > loseRadius) state = State.Wander;
                // Si se aleja un poco pero sigue cerca, volvemos a perseguir
                else if (dist > arriveRadius * 1.5f) state = State.Seek;
                break;
        }
    }

    /// <summary>
    /// Ejecuta la fuerza correspondiente al estado actual.
    /// </summary>
    Vector2 RunState()
    {
        switch (state)
        {
            case State.Seek: return Seek(player.position);
            case State.Arrive: return Arrive(player.position);
            default: return Wander();
        }
    }

    // ============================================================
    // COMPORTAMIENTOS
    // ============================================================
    /// <summary>
    /// Seek: genera una velocidad deseada hacia el objetivo a máxima velocidad
    /// y devuelve la diferencia con la velocidad actual (fuerza de corrección).
    /// </summary>
    Vector2 Seek(Vector2 target)
    {
        Vector2 desired = (target - (Vector2)transform.position).normalized * maxSpeed;
        return desired - velocity;
    }

    /// <summary>
    /// Arrive: como Seek pero reduce la velocidad al acercarse al objetivo
    /// para frenar suavemente. Si está muy cerca, se detiene del todo.
    /// </summary>
    Vector2 Arrive(Vector2 target)
    {
        Vector2 toTarget = target - (Vector2)transform.position;
        float distance = toTarget.magnitude;

        // Si está dentro del radio de parada, frenamos por completo
        if (distance < stopRadius)
        {
            velocity *= 0.85f;   // amortiguamos la velocidad
            return -velocity;    // fuerza contraria a la velocidad actual
        }

        // Velocidad deseada depende de la distancia (frenado proporcional)
        float speed = maxSpeed;
        if (distance < slowingRadius)
            speed = maxSpeed * (distance / slowingRadius);

        Vector2 desired = toTarget.normalized * speed;
        return desired - velocity;
    }

    /// <summary>
    /// Wander: igual que en el WandererNPC. Proyecta un círculo delante del NPC
    /// y elige un punto aleatorio en su interior.
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
    // DEBUG EN EL EDITOR
    // ============================================================
    /// <summary>
    /// Dibuja los radios de detección, pérdida y arrive como esferas 
    /// 
    /// 
    /// </summary>
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;   // Detección = amarillo
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;      // Pérdida = rojo
        Gizmos.DrawWireSphere(transform.position, loseRadius);

        Gizmos.color = Color.green;    // Arrive = verde
        Gizmos.DrawWireSphere(transform.position, arriveRadius);
    }
}
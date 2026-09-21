using UnityEngine;

/// <summary>
/// NPC "Wanderer" (mago): deambula por el escenario sin verse afectado
/// ni por el jugador ni por otros NPCs. Solo ejecuta el comportamiento Wander
/// 
/// </summary>
public class WandererNPC : MonoBehaviour
{
    // ============================================================
    // PARÁMETROS DE MOVIMIENTO
    // ============================================================
    [Header("Movimiento")]
    [Tooltip("Velocidad máxima a la que puede moverse el NPC.")]
    public float maxSpeed = 2f;

    [Tooltip("Fuerza máxima (aceleración) que puede aplicar. Más alto = reacciona antes.")]
    public float maxForce = 8f;

    // ============================================================
    // PARÁMETROS DEL WANDER
    // ============================================================
    [Header("Wander")]
    [Tooltip("Radio del círculo interno donde se elige la dirección aleatoria.")]
    public float wanderRadius = 1.5f;

    [Tooltip("Distancia a la que se coloca el círculo delante del NPC.")]
    public float wanderDistance = 2f;

    [Tooltip("Cuánto se perturba el ángulo cada frame. Más alto = más errático.")]
    public float wanderJitter = 30f;

    // ============================================================
    // ESTADO INTERNO
    // ============================================================
    private Vector2 velocity;              // Velocidad actual del NPC
    private float wanderAngle;             // Ángulo actual del círculo de wander
    private SteeringAvoidance avoidance;   // Referencia al componente de evasión

    // ============================================================
    // INICIALIZACIÓN
    // ============================================================
    void Start()
    {
        // Ángulo inicial aleatorio para que no arranque siempre igual
        wanderAngle = Random.Range(0f, 360f);

        // Velocidad inicial aleatoria para que no arranque quieto
        velocity = Random.insideUnitCircle.normalized * maxSpeed;

        // Buscamos el componente de evasión en el mismo GameObject (puede ser null)
        avoidance = GetComponent<SteeringAvoidance>();
    }

    // ============================================================
    // BUCLE PRINCIPAL
    // ============================================================
    void Update()
    {
        // 1) Calculamos la fuerza del Wander
        Vector2 force = Wander();

        // 2) Sumamos la fuerza de evasión de obstáculos (si existe el componente)
        if (avoidance != null)
            force += avoidance.GetAvoidanceForce();

        // 3) Aplicamos la fuerza y movemos al NPC
        ApplyForce(force);
        Move();

        // 4) Anti-atasco: si lleva un rato sin moverse, lo desatascamos
        if (avoidance != null)
        {
            avoidance.SetVelocity(velocity);  // informamos al sistema de nuestra velocidad

            if (avoidance.CheckAndUnstick())
            {
                // Elegimos la dirección más libre y empujamos al NPC hacia allí
                Vector2 escape = avoidance.GetEscapeDirection();
                velocity = escape * maxSpeed;

                // Reorientamos el ángulo de wander hacia esa dirección
                wanderAngle = Mathf.Atan2(escape.y, escape.x) * Mathf.Rad2Deg;
            }
        }
    }

    // ============================================================
    // COMPORTAMIENTO WANDER
    // ============================================================
    /// <summary>
    /// Wander clásico: proyecta un círculo delante del NPC y elige un punto
    /// aleatorio en su interior. Ese punto genera una fuerza suave.
    /// </summary>
    Vector2 Wander()
    {
        // Centro del círculo: delante del NPC, en su dirección de movimiento
        Vector2 circleCenter = velocity.normalized * wanderDistance;

        // Si está quieto, usamos "derecha" como dirección por defecto
        if (velocity.sqrMagnitude < 0.01f)
            circleCenter = (Vector2)transform.right * wanderDistance;

        // Punto dentro del círculo según el ángulo actual
        Vector2 displacement = new Vector2(
            Mathf.Cos(wanderAngle * Mathf.Deg2Rad),
            Mathf.Sin(wanderAngle * Mathf.Deg2Rad)
        ) * wanderRadius;

        // Perturbamos el ángulo aleatoriamente (proporcional al tiempo para ser framerate-independent)
        wanderAngle += Random.Range(-1f, 1f) * wanderJitter * Time.deltaTime * 60f;

        // La fuerza final es el vector desde el NPC hasta ese punto desplazado
        return circleCenter + displacement;
    }

    // ============================================================
    // APLICAR FUERZA (física básica de steering)
    // ============================================================
    /// <summary>
    /// Convierte una fuerza en aceleración, actualiza la velocidad
    /// y la limita a maxSpeed.
    /// </summary>
    void ApplyForce(Vector2 force)
    {
        // Truncamos la fuerza para no superar maxForce
        if (force.magnitude > maxForce)
            force = force.normalized * maxForce;

        // Integramos: v = v + a * dt
        velocity += force * Time.deltaTime;

        // Limitamos la velocidad a maxSpeed
        if (velocity.magnitude > maxSpeed)
            velocity = velocity.normalized * maxSpeed;
    }

    // ============================================================
    // MOVER EL NPC
    // ============================================================
    /// <summary>
    /// Mueve el sprite según la velocidad actual. Sin rotación:
    /// 
    /// </summary>
    void Move()
    {
        transform.position += (Vector3)(velocity * Time.deltaTime);
        
    }
}

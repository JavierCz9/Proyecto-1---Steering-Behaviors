using UnityEngine;

/// <summary>
/// Componente auxiliar de los NPC para:
///  1) Evitar paredes ANTES de chocar (con raycasts hacia adelante).
///  2) Desatascar al NPC si se queda pegado (forzando una nueva dirección).
/// Los NPCs llaman a sus métodos desde su propio Update.
/// </summary>
[DisallowMultipleComponent]
public class SteeringAvoidance : MonoBehaviour
{
    // ============================================================
    // DETECCIÓN DE OBSTÁCULOS
    // ============================================================
    [Header("Detección de obstáculos")]
    [Tooltip("Capa que contiene las paredes/obstáculos del escenario.")]
    public LayerMask obstacleMask;

    [Tooltip("Distancia de los raycasts frontales.")]
    public float avoidDistance = 2f;

    [Tooltip("Fuerza del empuje lateral para esquivar paredes.")]
    public float avoidForce = 25f;

    [Tooltip("Apertura en grados de los raycasts laterales respecto al frontal.")]
    public float sideAngle = 30f;

    // ============================================================
    // ANTI-ATASCO
    // ============================================================
    [Header("Anti-atasco")]
    [Tooltip("Si la velocidad baja de este valor, se considera 'atascado'.")]
    public float stuckSpeedThreshold = 0.3f;

    [Tooltip("Tiempo (segundos) con velocidad baja para declarar atascado.")]
    public float stuckTime = 0.6f;

    [Tooltip("Fuerza del empujón al desatascarse (informativo, el NPC lo aplica).")]
    public float unstickForce = 6f;

    // ============================================================
    // DEBUG
    // ============================================================
    [Header("Debug")]
    [Tooltip("Si está activo, dibuja los raycasts en la vista Scene.")]
    public bool debugRays = true;

    // ============================================================
    // ESTADO INTERNO
    // ============================================================
    private Vector2 velocity;     // La setea el NPC cada frame
    private float stuckTimer = 0f;

    // ============================================================
    // API PÚBLICA
    // ============================================================
    /// <summary>
    /// El NPC nos informa de su velocidad actual cada frame
    /// (para el detector de atascos y para saber hacia dónde mira).
    /// </summary>
    public void SetVelocity(Vector2 v) => velocity = v;

    /// <summary>
    /// Devuelve la fuerza de evasión calculada con tres raycasts
    /// (frontal + dos laterales). Si no hay obstáculos, devuelve Vector2.zero.
    /// </summary>
    public Vector2 GetAvoidanceForce()
    {
        // Dirección hacia la que se mueve el NPC (o "derecha" si está quieto)
        Vector2 heading = velocity.sqrMagnitude > 0.01f
            ? velocity.normalized
            : (Vector2)transform.right;

        Vector2 total = Vector2.zero;

        // Tres direcciones: al frente, un poco a izquierda, un poco a derecha
        Vector2[] dirs =
        {
            heading,
            Quaternion.Euler(0, 0,  sideAngle) * heading,
            Quaternion.Euler(0, 0, -sideAngle) * heading
        };

        // Lanzamos los raycasts y sumamos empujes por cada impacto
        for (int i = 0; i < dirs.Length; i++)
        {
            RaycastHit2D hit = Physics2D.Raycast(transform.position, dirs[i], avoidDistance, obstacleMask);

            // Debug visual: rojo si choca, verde si libre
            if (debugRays)
            {
                Debug.DrawRay(transform.position, dirs[i] * avoidDistance,
                              hit.collider != null ? Color.red : Color.green);
            }

            if (hit.collider != null)
            {
                // Cuanto más cerca esté la pared, más fuerza aplicamos
                float proximity = 1f - (hit.distance / avoidDistance);
                total += hit.normal * proximity * avoidForce;
            }
        }

        return total;
    }

    /// <summary>
    /// Comprueba si el NPC lleva atascado más de stuckTime segundos.
    /// Si es así, resetea el contador y devuelve true (el NPC debe reaccionar).
    /// </summary>
    public bool CheckAndUnstick()
    {
        if (velocity.magnitude < stuckSpeedThreshold)
            stuckTimer += Time.deltaTime;
        else
            stuckTimer = 0f;   // se movió lo suficiente → no está atascado

        if (stuckTimer >= stuckTime)
        {
            stuckTimer = 0f;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Busca entre 8 direcciones la que tenga más espacio libre
    /// (mayor distancia hasta el obstáculo más cercano en esa dirección).
    /// Sirve para elegir hacia dónde escapar cuando el NPC está atascado.
    /// </summary>
    public Vector2 GetEscapeDirection()
    {
        Vector2 bestDir = Random.insideUnitCircle.normalized;
        float bestClearance = -1f;

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                                      Mathf.Sin(angle * Mathf.Deg2Rad));

            RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, avoidDistance * 2f, obstacleMask);
            float clearance = hit.collider != null ? hit.distance : avoidDistance * 2f;

            if (clearance > bestClearance)
            {
                bestClearance = clearance;
                bestDir = dir;
            }
        }

        return bestDir;
    }
}

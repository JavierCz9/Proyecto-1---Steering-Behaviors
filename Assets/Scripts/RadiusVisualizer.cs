using UnityEngine;

/// <summary>
/// Crea automáticamente
/// los círculos de radio como hijos con LineRenderer y los sincroniza con
/// los valores del script del NPC. Funciona en el editor Y en la build.
/// </summary>
[DisallowMultipleComponent]
public class RadiusVisualizer : MonoBehaviour
{
    // ============================================================
    // VISIBILIDAD
    // ============================================================
    [Header("Visibilidad")]
    [Tooltip("Si está marcado, los radios se ven en la build. Desmárcalo si no quieres que el jugador los vea.")]
    public bool visibleInGame = true;

    // ============================================================
    // AJUSTES DE DIBUJO
    // ============================================================
    [Header("Ajustes de dibujo")]
    [Tooltip("Número de segmentos del círculo. Más = más suave.")]
    [Range(20, 200)] public int segments = 64;

    [Tooltip("Grosor de la línea del círculo.")]
    public float lineWidth = 0.06f;

    // ============================================================
    // MATERIAL
    // ============================================================
    [Header("Material")]
    [Tooltip("Arrastra aquí el material LineMaterial (shader Sprites/Default o Sprite-Unlit-Default en URP).")]
    public Material lineMaterial;

    // ============================================================
    // OVERRIDES MANUALES
    // ============================================================
    [Header("Overrides manuales (si el NPC no es Chaser ni Coward)")]
    public RadiusDef[] manualRadii;

    /// <summary>
    /// Definición manual de un radio (para NPCs que no sean Chaser ni Coward).
    /// </summary>
    [System.Serializable]
    public class RadiusDef
    {
        public string name = "Radius";        // Nombre del GameObject hijo
        public string fieldName = "";         // Campo del script del NPC a leer (opcional)
        public float fixedRadius = 4f;        // Radio fijo si no hay fieldName
        public Color color = Color.yellow;    // Color del círculo
    }

    /// <summary>
    /// Instancia interna que representa un círculo dibujado.
    /// </summary>
    private class RadiusInstance
    {
        public LineRenderer line;
        public Transform transform;
        public string fieldName;         // Campo del NPC del que leemos el radio
        public float fixedRadius;        // Radio por defecto si no hay campo
        public float lastRadius = -1f;   // Para evitar redibujar si no cambia
        public Color lastColor;
    }

    private RadiusInstance[] radii;

    // ============================================================
    // INICIALIZACIÓN
    // ============================================================
    void Awake()
    {
        BuildRadii();
    }

    // ============================================================
    // ACTUALIZACIÓN POR FRAME
    // ============================================================
    void Update()
    {
        if (radii == null) return;

        // Tecla R para mostrar/ocultar durante el Play (útil para depurar)
        if (Input.GetKeyDown(KeyCode.R))
            visibleInGame = !visibleInGame;

        foreach (var r in radii)
        {
            if (r.line == null) continue;

            // Sincronizar visibilidad
            if (r.line.enabled != visibleInGame)
                r.line.enabled = visibleInGame;

            if (!visibleInGame) continue;

            // Leer el radio actual: del campo del NPC o del valor fijo
            float currentRadius = r.fixedRadius;
            if (!string.IsNullOrEmpty(r.fieldName))
            {
                float v = GetFloatField(r.fieldName);
                if (v > 0f) currentRadius = v;
            }

            // Solo redibujar si cambió el radio
            if (!Mathf.Approximately(currentRadius, r.lastRadius))
            {
                DrawCircle(r, currentRadius, r.lastColor);
                r.lastRadius = currentRadius;
            }
        }
    }

    // ============================================================
    // CONSTRUCCIÓN DE LOS RADIOS
    // ============================================================
    /// <summary>
    /// Detecta qué tipo de NPC es este GameObject y crea los radios
    /// correspondientes con sus colores por defecto.
    /// </summary>
    void BuildRadii()
    {
        ChaserNPC chaser = GetComponent<ChaserNPC>();
        CowardNPC coward = GetComponent<CowardNPC>();

        if (chaser != null)
        {
            // Chaser: detection (amarillo), lose (rojo), arrive (verde)
            radii = new RadiusInstance[]
            {
                CreateRadius("DetectionRadius", "detectionRadius", 4f, Color.yellow),
                CreateRadius("LoseRadius",      "loseRadius",      6f, Color.red),
                CreateRadius("ArriveRadius",    "arriveRadius",    1.2f, Color.green)
            };
        }
        else if (coward != null)
        {
            // Coward: flee (rojo), safe (verde)
            radii = new RadiusInstance[]
            {
                CreateRadius("FleeRadius", "fleeRadius", 4f, Color.red),
                CreateRadius("SafeRadius", "safeRadius", 7f, Color.green)
            };
        }
        else if (manualRadii != null && manualRadii.Length > 0)
        {
            // Configuración manual desde el Inspector
            radii = new RadiusInstance[manualRadii.Length];
            for (int i = 0; i < manualRadii.Length; i++)
            {
                var def = manualRadii[i];
                radii[i] = CreateRadius(def.name, def.fieldName, def.fixedRadius, def.color);
            }
        }
        else
        {
            Debug.LogWarning($"[RadiusVisualizer] {name}: no detecté ChaserNPC ni CowardNPC.");
            radii = new RadiusInstance[0];
        }
    }

    /// <summary>
    /// Crea (o reutiliza) un GameObject hijo con LineRenderer para dibujar
    /// un círculo. Configura material, sorting y color inicial.
    /// </summary>
    RadiusInstance CreateRadius(string objName, string fieldName, float defaultRadius, Color color)
    {
        // Reutilizar si ya existe (por si ejecutaste el script antes)
        Transform existing = transform.Find(objName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(objName);

        if (existing == null)
            go.transform.SetParent(transform, false);

        // Posición local en el centro del NPC
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        // LineRenderer
        LineRenderer lr = go.GetComponent<LineRenderer>();
        if (lr == null) lr = go.AddComponent<LineRenderer>();

        lr.useWorldSpace = false;              // se mueve con el padre
        lr.loop = true;                        // cierra el círculo
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.textureMode = LineTextureMode.Stretch;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;

        // Material (el asignado desde el Inspector para que se vean los colores)
        if (lineMaterial != null)
            lr.material = lineMaterial;
        else
            Debug.LogWarning($"[RadiusVisualizer] {name}: asigna lineMaterial en el Inspector.");

        // Que se dibuje por encima de los sprites
        lr.sortingLayerName = "Default";
        lr.sortingOrder = 100;

        var instance = new RadiusInstance
        {
            line = lr,
            transform = go.transform,
            fieldName = fieldName,
            fixedRadius = defaultRadius,
            lastColor = color
        };

        // Dibujado inicial
        DrawCircle(instance, defaultRadius, color);
        return instance;
    }

    // ============================================================
    // DIBUJADO
    // ============================================================
    /// <summary>
    /// Dibuja un círculo de radio 'radius' con el color indicado,
    /// colocando 'segments' puntos alrededor del centro.
    /// </summary>
    void DrawCircle(RadiusInstance r, float radius, Color color)
    {
        r.line.positionCount = segments;
        r.line.startColor = color;
        r.line.endColor = color;

        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            r.line.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    // ============================================================
    // UTILIDADES
    // ============================================================
    /// <summary>
    /// Busca un campo float por nombre en cualquier MonoBehaviour del GameObject.
    /// Se usa para leer 'detectionRadius', 'fleeRadius', etc. del script del NPC
    /// sin tener que configurarlos a mano.
    /// </summary>
    float GetFloatField(string fieldName)
    {
        foreach (var mb in GetComponents<MonoBehaviour>())
        {
            if (mb == this) continue;

            var f = mb.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (f != null && f.FieldType == typeof(float))
                return (float)f.GetValue(mb);
        }
        return 0f;
    }
}

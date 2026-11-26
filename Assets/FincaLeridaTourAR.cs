using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class FincaLeridaTourAR : MonoBehaviour
{
    [Header("Cámara y Control 360° / Recorrido")]
    public Camera camaraTour;
    public float sensibilidadGiro = 0.22f;
    public float velocidadMovimiento = 4.5f;
    public float alturaOjos = 1.65f;
    public float distanciaFrenteEntrada = 3.8f;
    public bool modoRealidadAumentada = false;

    [Header("Paisaje 360° Alrededor")]
    [Tooltip("Arrastra aquí la textura Panorama360 para cubrir todo el entorno en 360°")]
    public Texture2D texturaPanorama360;

    [Header("Ubicación Garaje / Bodega con Sacos (A un lado amplio)")]
    public Vector3 posGarajeLateral = new Vector3(11.5f, 0f, 3.5f);

    [Tooltip("Ajusta en Y para bajar o subir el garaje hasta la base de la finca (ej. -0.5)")]
    public float offsetAlturaGaraje = -0.5f;
    [Tooltip("Gira el garaje en Y (0, 90, 180, -90) para que la entrada/puertas queden al frente")]
    public float rotacionYGaraje = 90f;

    [Header("Puntos de Recorrido (Respaldo)")]
    public Vector3 posEntrada = new Vector3(-0.4f, 0f, -13.5f);
    public Vector3 posParcelaIzquierda = new Vector3(-7.2f, 0f, -3.5f);
    public Vector3 posParcelaDerecha = new Vector3(7.2f, 0f, -3.5f);
    public Vector3 posCasaLerida = new Vector3(1.6f, 0f, 3.8f);

    private float yaw = 0f;
    private float pitch = 0f;
    private Vector2 ultimoPointer;
    private bool arrastrando = false;
    private bool camaraEnGaraje = false;

    private int dedogiroId = -1;
    private Vector2 ultimoToqueGiro;
    private int moverAdelante = 0;
    private int moverLados = 0;

    private Transform tableroEntradaTransform;
    private GameObject panelTaza3D;
    private GameObject panelCacao3D;
    private GameObject panelSacos3D;

    private readonly Dictionary<string, (string titulo, string cuerpo)> contenidos =
        new Dictionary<string, (string, string)>
    {
        {
            "Hotspot_Tablero_Bienvenida",
            (
                "Bienvenidos a Finca Lérida",
                "Finca Lérida, en las tierras altas de Boquete, Chiriquí, une tradición cafetalera, bosque nuboso y hospitalidad."
            )
        },
        {
            "Hotspot_Grano_Cafe",
            (
                "Cacao y Grano en Bandeja",
                "Sobre la mesa se aprecian los granos tostados y cacao de la región. Tras la cosecha selectiva, fermentación, secado y tueste, conservan los aromas de las tierras altas."
            )
        },
        {
            "Hotspot_Taza_Cafe",
            (
                "Café en Taza",
                "Una taza de café de especialidad refleja el cuidado en cada etapa: cultivo bajo sombra, maduración de la cereza, secado controlado y tueste preciso."
            )
        },
        {
            "Hotspot_Sacos_Cafe_Garaje",
            (
                "Garaje y Bodega de Sacos de Café",
                "En este espacio amplio tipo garaje y bodega se recorren los estantes con sacos de café beneficiado y seco, listos para selección y despacho."
            )
        }
    };

    private void Start()
    {
        if (camaraTour == null)
        {
            camaraTour = Camera.main;
        }

        ConfigurarPaisaje360();
        PrepararEscena();
        IrAEntrada();
    }

    private void ConfigurarPaisaje360()
    {
        if (texturaPanorama360 == null) return;

        Shader skyShader = Shader.Find("Skybox/Panoramic");
        if (skyShader != null)
        {
            Material skyMat = new Material(skyShader);
            skyMat.SetTexture("_MainTex", texturaPanorama360);
            skyMat.SetFloat("_Exposure", 1.0f);
            skyMat.SetFloat("_Rotation", 0f);
            RenderSettings.skybox = skyMat;
            DynamicGI.UpdateEnvironment();
        }
    }

    private void PrepararEscena()
    {
        GameObject fincaRoot = GameObject.Find("Recorrido_finca");
        Transform raizBusqueda = fincaRoot != null ? fincaRoot.transform : null;

        Transform tazaTransform = null;
        Transform cacaoTransform = null;

        foreach (var clave in contenidos.Keys)
        {
            Transform obj = BuscarObjeto(raizBusqueda, clave);
            if (obj != null)
            {
                if (obj.GetComponent<Collider>() == null)
                {
                    BoxCollider col = obj.gameObject.AddComponent<BoxCollider>();
                    col.size = Vector3.Max(col.size, Vector3.one * 0.16f);
                }

                if (clave == "Hotspot_Tablero_Bienvenida")
                    tableroEntradaTransform = obj;
                else if (clave == "Hotspot_Taza_Cafe")
                    tazaTransform = obj;
                else if (clave == "Hotspot_Grano_Cafe")
                    cacaoTransform = obj;
            }
        }

        // Letrero de bienvenida grande y fijo en el tablero de entrada
        CrearLetreroBienvenidaEnEntrada(tableroEntradaTransform, raizBusqueda);

        // Garaje/bodega amplio a un costado con puertas, ventanas, estantes y sacos de café
        Transform sacosTransform = ConstruirGarajeAmplioLateral(raizBusqueda);

        if (tazaTransform != null)
        {
            var infoTaza = contenidos["Hotspot_Taza_Cafe"];
            Vector3 posMundo = ObtenerPosicionEncima(tazaTransform, 0.65f);
            panelTaza3D = CrearPanelInfo3D("Panel_Info_Taza", posMundo, infoTaza.titulo, infoTaza.cuerpo);
            panelTaza3D.SetActive(false);
        }

        if (cacaoTransform != null)
        {
            var infoCacao = contenidos["Hotspot_Grano_Cafe"];
            Vector3 posMundo = ObtenerPosicionEncima(cacaoTransform, 0.65f);
            panelCacao3D = CrearPanelInfo3D("Panel_Info_Cacao", posMundo, infoCacao.titulo, infoCacao.cuerpo);
            panelCacao3D.SetActive(false);
        }

        if (sacosTransform != null)
        {
            var infoSacos = contenidos["Hotspot_Sacos_Cafe_Garaje"];
            Vector3 posMundo = ObtenerPosicionEncima(sacosTransform, 0.9f);
            panelSacos3D = CrearPanelInfo3D("Panel_Info_Sacos", posMundo, infoSacos.titulo, infoSacos.cuerpo);
            panelSacos3D.SetActive(false);
        }
    }

    private Transform ConstruirGarajeAmplioLateral(Transform raizFinca)
    {
        GameObject anterior = GameObject.Find("Garaje_Bodega_Lateral_Cafe");
        if (anterior != null) Destroy(anterior);

        float baseX = posGarajeLateral.x;
        float baseZ = posGarajeLateral.z;
        float baseY = ObtenerAlturaTerreno(baseX, baseZ);

        GameObject garajeGO = new GameObject("Garaje_Bodega_Lateral_Cafe");
        if (raizFinca != null) garajeGO.transform.SetParent(raizFinca, true);
        garajeGO.transform.position = new Vector3(baseX, baseY + offsetAlturaGaraje, baseZ);
        garajeGO.transform.rotation = Quaternion.Euler(0f, rotacionYGaraje, 0f);

        // Materiales compatibles con Unity 6 (URP / Built-in) para evitar color fucsia
        Material matMuro = CrearMaterialSeguro(new Color(0.82f, 0.76f, 0.66f));
        Material matMadera = CrearMaterialSeguro(new Color(0.40f, 0.24f, 0.13f));
        Material matVidrio = CrearMaterialSeguro(new Color(0.55f, 0.78f, 0.92f));
        Material matTecho = CrearMaterialSeguro(new Color(0.20f, 0.40f, 0.25f));
        Material matSaco = CrearMaterialSeguro(new Color(0.65f, 0.46f, 0.26f));
        Material matEstante = CrearMaterialSeguro(new Color(0.30f, 0.20f, 0.11f));

        // Espacio amplio para recorrer cómodamente
        float ancho = 10.5f;
        float profundo = 9.0f;
        float altoPared = 3.4f;
        float grosor = 0.25f;

        // Piso amplio
        CrearCaja("Piso_Garaje", garajeGO.transform, new Vector3(0f, 0.04f, 0f), new Vector3(ancho, 0.08f, profundo), matMadera, false);

        // Pared trasera con ventana amplia
        CrearCaja("Pared_Trasera_Baja", garajeGO.transform, new Vector3(0f, 0.5f, profundo * 0.5f), new Vector3(ancho, 1.0f, grosor), matMuro, false);
        CrearCaja("Pared_Trasera_Alta", garajeGO.transform, new Vector3(0f, 2.8f, profundo * 0.5f), new Vector3(ancho, 1.2f, grosor), matMuro, false);
        CrearCaja("Pared_Trasera_Izq", garajeGO.transform, new Vector3(-3.6f, 1.6f, profundo * 0.5f), new Vector3(3.3f, 1.4f, grosor), matMuro, false);
        CrearCaja("Pared_Trasera_Der", garajeGO.transform, new Vector3(3.6f, 1.6f, profundo * 0.5f), new Vector3(3.3f, 1.4f, grosor), matMuro, false);
        CrearCaja("Ventana_Trasera", garajeGO.transform, new Vector3(0f, 1.6f, profundo * 0.5f), new Vector3(3.9f, 1.2f, 0.08f), matVidrio, false);

        // Pared izquierda con puerta lateral y ventana
        CrearCaja("Pared_Izq_Tramo1", garajeGO.transform, new Vector3(-ancho * 0.5f, altoPared * 0.5f, 2.7f), new Vector3(grosor, altoPared, 3.6f), matMuro, false);
        CrearCaja("Pared_Izq_Tramo2", garajeGO.transform, new Vector3(-ancho * 0.5f, altoPared * 0.5f, -2.7f), new Vector3(grosor, altoPared, 3.6f), matMuro, false);
        CrearCaja("Pared_Izq_Dintel", garajeGO.transform, new Vector3(-ancho * 0.5f, 2.75f, 0f), new Vector3(grosor, 1.3f, 1.8f), matMuro, false);
        CrearCaja("Puerta_Lateral_Izq", garajeGO.transform, new Vector3(-ancho * 0.5f, 1.05f, 0f), new Vector3(0.14f, 2.1f, 1.5f), matMadera, false);

        // Pared derecha con dos ventanas grandes
        CrearCaja("Pared_Der_Baja", garajeGO.transform, new Vector3(ancho * 0.5f, 0.5f, 0f), new Vector3(grosor, 1.0f, profundo), matMuro, false);
        CrearCaja("Pared_Der_Alta", garajeGO.transform, new Vector3(ancho * 0.5f, 2.8f, 0f), new Vector3(grosor, 1.2f, profundo), matMuro, false);
        CrearCaja("Pared_Der_Centro", garajeGO.transform, new Vector3(ancho * 0.5f, 1.6f, 0f), new Vector3(grosor, 1.4f, 2.0f), matMuro, false);
        CrearCaja("Pared_Der_Ext1", garajeGO.transform, new Vector3(ancho * 0.5f, 1.6f, 3.8f), new Vector3(grosor, 1.4f, 1.4f), matMuro, false);
        CrearCaja("Pared_Der_Ext2", garajeGO.transform, new Vector3(ancho * 0.5f, 1.6f, -3.8f), new Vector3(grosor, 1.4f, 1.4f), matMuro, false);
        CrearCaja("Ventana_Der_1", garajeGO.transform, new Vector3(ancho * 0.5f, 1.6f, 2.05f), new Vector3(0.08f, 1.2f, 2.1f), matVidrio, false);
        CrearCaja("Ventana_Der_2", garajeGO.transform, new Vector3(ancho * 0.5f, 1.6f, -2.05f), new Vector3(0.08f, 1.2f, 2.1f), matVidrio, false);

        // Frente abierto tipo garaje + puerta peatonal
        float frenteZ = -profundo * 0.5f;
        CrearCaja("Frente_Dintel", garajeGO.transform, new Vector3(0f, 2.85f, frenteZ), new Vector3(ancho, 1.1f, grosor), matMuro, false);
        CrearCaja("Frente_Columna_Izq", garajeGO.transform, new Vector3(-4.6f, 1.15f, frenteZ), new Vector3(1.3f, 2.3f, grosor), matMuro, false);
        CrearCaja("Frente_Columna_Centro", garajeGO.transform, new Vector3(1.2f, 1.15f, frenteZ), new Vector3(0.8f, 2.3f, grosor), matMuro, false);
        CrearCaja("Puerta_Peatonal_Frente", garajeGO.transform, new Vector3(2.5f, 1.05f, frenteZ), new Vector3(1.2f, 2.1f, 0.16f), matMadera, false);
        CrearCaja("Frente_Tramo_Der", garajeGO.transform, new Vector3(4.15f, 1.15f, frenteZ), new Vector3(2.1f, 2.3f, grosor), matMuro, false);

        // Techo del garaje/bodega
        CrearCaja("Techo_Garaje", garajeGO.transform, new Vector3(0f, altoPared + 0.15f, 0f), new Vector3(ancho + 0.8f, 0.25f, profundo + 0.8f), matTecho, false);

        // Estantes interiores con sacos de café en zona amplia
        GameObject grupoSacos = new GameObject("Hotspot_Sacos_Cafe_Garaje");
        grupoSacos.transform.SetParent(garajeGO.transform, false);
        grupoSacos.transform.localPosition = new Vector3(-2.4f, 0.08f, 2.2f);

        for (int nivel = 0; nivel < 3; nivel++)
        {
            float yNivel = 0.28f + nivel * 0.9f;
            CrearCaja($"Estante_Nivel_{nivel}", grupoSacos.transform, new Vector3(0f, yNivel, 0f), new Vector3(3.2f, 0.09f, 1.2f), matEstante, false);

            for (int s = 0; s < 4; s++)
            {
                float xSaco = -1.12f + s * 0.75f;
                CrearCaja($"Saco_Cafe_{nivel}_{s}", grupoSacos.transform, new Vector3(xSaco, yNivel + 0.28f, 0f), new Vector3(0.55f, 0.46f, 0.7f), matSaco, false);
            }
        }

        BoxCollider colSacos = grupoSacos.AddComponent<BoxCollider>();
        colSacos.center = new Vector3(0f, 1.3f, 0f);
        colSacos.size = new Vector3(3.3f, 2.4f, 1.3f);

        return grupoSacos.transform;
    }

    private void CrearCaja(string nombre, Transform padre, Vector3 posLocal, Vector3 escala, Material mat, bool conCollider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = posLocal;
        go.transform.localScale = escala;
        go.GetComponent<Renderer>().material = mat;

        if (!conCollider)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }
    }

    private Material CrearMaterialSeguro(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Standard");
        Material mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        return mat;
    }

    private void CrearLetreroBienvenidaEnEntrada(Transform tablero, Transform raizFinca)
    {
        GameObject anterior = GameObject.Find("Letrero_Bienvenida_Texto3D");
        if (anterior != null) Destroy(anterior);

        GameObject letreroGO = new GameObject("Letrero_Bienvenida_Texto3D");
        if (raizFinca != null)
        {
            letreroGO.transform.SetParent(raizFinca, true);
        }

        Vector3 posLetrero;
        if (tablero != null)
        {
            Renderer rend = tablero.GetComponent<Renderer>();
            Vector3 centro = rend != null ? rend.bounds.center : tablero.position;
            float frenteExteriorZ = rend != null ? rend.bounds.max.z + 0.08f : centro.z + 0.12f;
            posLetrero = new Vector3(centro.x, centro.y, frenteExteriorZ);
        }
        else
        {
            float ySuelo = ObtenerAlturaTerreno(-0.5f, -10.5f);
            posLetrero = new Vector3(-0.5f, ySuelo + 2.42f, -10.42f);
        }

        letreroGO.transform.position = posLetrero;
        letreroGO.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        letreroGO.transform.localScale = Vector3.one;

        TextMesh tm = letreroGO.AddComponent<TextMesh>();
        tm.text = "BIENVENIDOS A\nFINCA LÉRIDA";
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 64;
        tm.characterSize = 0.032f;
        tm.color = new Color(1f, 0.96f, 0.85f);
    }

    private Vector3 ObtenerPosicionEncima(Transform objetivo, float alturaExtra)
    {
        Renderer r = objetivo.GetComponentInChildren<Renderer>();
        if (r != null)
        {
            return new Vector3(r.bounds.center.x, r.bounds.max.y + alturaExtra, r.bounds.center.z);
        }
        return objetivo.position + Vector3.up * alturaExtra;
    }

    private GameObject CrearPanelInfo3D(string nombre, Vector3 posicionMundo, string titulo, string cuerpo)
    {
        GameObject panelGO = new GameObject(nombre);
        panelGO.transform.position = posicionMundo;
        panelGO.transform.localScale = Vector3.one;

        GameObject fondo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fondo.name = "Fondo_Panel";
        fondo.transform.SetParent(panelGO.transform, false);
        fondo.transform.localPosition = Vector3.zero;
        fondo.transform.localScale = new Vector3(1.65f, 0.88f, 1f);
        Destroy(fondo.GetComponent<Collider>());

        Material matFondo = CrearMaterialSeguro(new Color(0.10f, 0.08f, 0.06f));
        fondo.GetComponent<Renderer>().material = matFondo;

        GameObject textoGO = new GameObject("Texto_Panel");
        textoGO.transform.SetParent(panelGO.transform, false);
        textoGO.transform.localPosition = new Vector3(0f, 0f, -0.02f);

        TextMesh tm = textoGO.AddComponent<TextMesh>();
        tm.text = $"{titulo}\n\n{AjustarLineas(cuerpo, 32)}";
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 32;
        tm.characterSize = 0.012f;
        tm.color = new Color(1f, 0.96f, 0.88f);

        return panelGO;
    }

    private string AjustarLineas(string texto, int maxCharsPorLinea)
    {
        string[] palabras = texto.Split(' ');
        string resultado = "";
        string linea = "";

        foreach (string p in palabras)
        {
            if ((linea + p).Length > maxCharsPorLinea)
            {
                resultado += linea.TrimEnd() + "\n";
                linea = "";
            }
            linea += p + " ";
        }
        return resultado + linea.TrimEnd();
    }

    private Transform BuscarObjeto(Transform raiz, string nombreBuscado)
    {
        if (raiz != null)
        {
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == nombreBuscado) return t;
            }
        }

        GameObject exacto = GameObject.Find(nombreBuscado);
        if (exacto != null) return exacto.transform;

        string pista = nombreBuscado.Contains("Tablero") ? "Tablero" :
                       nombreBuscado.Contains("Grano") ? "Grano" : "Taza";

        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name.IndexOf(pista, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return t;
            }
        }
        return null;
    }

    private void Update()
    {
        if (camaraTour == null) return;

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            IrAEntrada();
        }

        ManejarGiro360YMovimiento();
        DetectarCursorEncima();
        OrientarPanelesHaciaCamara();
    }

    private void OnGUI()
    {
        if (GUI.Button(new Rect(20f, 20f, 120f, 48f), "Inicio"))
        {
            IrAEntrada();
        }

        float w = Screen.width;
        float h = Screen.height;
        float btn = 64f;
        float baseX = w - (btn * 3f) - 30f;
        float baseY = h - (btn * 2f) - 25f;

        moverAdelante = 0;
        moverLados = 0;

        if (GUI.RepeatButton(new Rect(baseX + btn, baseY, btn, btn), "▲"))
            moverAdelante = 1;
        if (GUI.RepeatButton(new Rect(baseX + btn, baseY + btn, btn, btn), "▼"))
            moverAdelante = -1;
        if (GUI.RepeatButton(new Rect(baseX, baseY + btn, btn, btn), "◀"))
            moverLados = -1;
        if (GUI.RepeatButton(new Rect(baseX + btn * 2f, baseY + btn, btn, btn), "▶"))
            moverLados = 1;
    }

    private void OrientarPanelesHaciaCamara()
    {
        if (panelTaza3D != null && panelTaza3D.activeSelf)
        {
            panelTaza3D.transform.LookAt(camaraTour.transform);
            panelTaza3D.transform.Rotate(0f, 180f, 0f);
        }
        if (panelCacao3D != null && panelCacao3D.activeSelf)
        {
            panelCacao3D.transform.LookAt(camaraTour.transform);
            panelCacao3D.transform.Rotate(0f, 180f, 0f);
        }
        if (panelSacos3D != null && panelSacos3D.activeSelf)
        {
            panelSacos3D.transform.LookAt(camaraTour.transform);
            panelSacos3D.transform.Rotate(0f, 180f, 0f);
        }
    }

    private void ManejarGiro360YMovimiento()
    {
        if (modoRealidadAumentada) return;

        var mouse = Mouse.current;
        var kb = Keyboard.current;
        var touch = Touchscreen.current;

        // Giro con mouse en PC
        Vector2 posActual = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        bool presionadoAhora = mouse != null && mouse.leftButton.wasPressedThisFrame;
        bool sostenido = mouse != null && mouse.leftButton.isPressed;
        bool soltadoAhora = mouse != null && mouse.leftButton.wasReleasedThisFrame;

        if (presionadoAhora)
        {
            arrastrando = true;
            ultimoPointer = posActual;
        }
        else if (soltadoAhora)
        {
            arrastrando = false;
        }

        if (arrastrando && sostenido)
        {
            Vector2 delta = posActual - ultimoPointer;
            ultimoPointer = posActual;

            yaw += delta.x * sensibilidadGiro;
            pitch -= delta.y * sensibilidadGiro;
            pitch = Mathf.Clamp(pitch, -85f, 85f);
        }

        // Giro arrastrando con un dedo en Android / táctil
        if (touch != null)
        {
            foreach (var t in touch.touches)
            {
                int id = t.touchId.ReadValue();
                Vector2 posToque = t.position.ReadValue();

                if (t.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began && dedogiroId == -1)
                {
                    // Ignora la esquina superior izquierda donde está el botón "Inicio"
                    if (posToque.x > 150f || posToque.y < Screen.height - 80f)
                    {
                        dedogiroId = id;
                        ultimoToqueGiro = posToque;
                    }
                }
                else if (id == dedogiroId)
                {
                    if (t.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
                    {
                        Vector2 deltaToque = posToque - ultimoToqueGiro;
                        ultimoToqueGiro = posToque;

                        yaw += deltaToque.x * sensibilidadGiro;
                        pitch -= deltaToque.y * sensibilidadGiro;
                        pitch = Mathf.Clamp(pitch, -85f, 85f);
                    }
                    else if (t.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended ||
                             t.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
                    {
                        dedogiroId = -1;
                    }
                }
            }
        }

        if (kb != null)
        {
            if (kb.qKey.isPressed) yaw -= 90f * Time.deltaTime;
            if (kb.eKey.isPressed) yaw += 90f * Time.deltaTime;
        }

        camaraTour.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Movimiento por teclado (PC) o botones táctiles (Android)
        Vector2 movInput = new Vector2(moverLados, moverAdelante);
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) movInput.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) movInput.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) movInput.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) movInput.x -= 1f;
        }
        movInput = Vector2.ClampMagnitude(movInput, 1f);

        Vector3 forwardPlano = Vector3.ProjectOnPlane(camaraTour.transform.forward, Vector3.up).normalized;
        Vector3 rightPlano = Vector3.ProjectOnPlane(camaraTour.transform.right, Vector3.up).normalized;

        Vector3 mov = (forwardPlano * movInput.y + rightPlano * movInput.x) * velocidadMovimiento * Time.deltaTime;
        camaraTour.transform.position += mov;

        float xAct = camaraTour.transform.position.x;
        float zAct = camaraTour.transform.position.z;
        camaraEnGaraje = EstaDentroDeGaraje(xAct, zAct);

        float ySueloActual = camaraEnGaraje
            ? (ObtenerAlturaTerreno(xAct, zAct) + offsetAlturaGaraje + 0.08f)
            : ObtenerAlturaTerreno(xAct, zAct);

        camaraTour.transform.position = new Vector3(xAct, ySueloActual + alturaOjos, zAct);
    }

    private void DetectarCursorEncima()
    {
        var mouse = Mouse.current;
        Vector2 posCursor = mouse != null ? mouse.position.ReadValue() : Vector2.zero;

        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.isPressed)
        {
            posCursor = touch.primaryTouch.position.ReadValue();
        }

        bool mostrarTaza = false;
        bool mostrarCacao = false;
        bool mostrarSacos = false;

        Ray rayo = camaraTour.ScreenPointToRay(posCursor);
        if (Physics.Raycast(rayo, out RaycastHit hit, 150f))
        {
            string clave = ObtenerClaveObjeto(hit.collider.transform);
            if (clave == "Hotspot_Taza_Cafe")
            {
                mostrarTaza = true;
            }
            else if (clave == "Hotspot_Grano_Cafe")
            {
                mostrarCacao = true;
            }
            else if (clave == "Hotspot_Sacos_Cafe_Garaje")
            {
                mostrarSacos = true;
            }
        }

        if (panelTaza3D != null) panelTaza3D.SetActive(mostrarTaza);
        if (panelCacao3D != null) panelCacao3D.SetActive(mostrarCacao);
        if (panelSacos3D != null) panelSacos3D.SetActive(mostrarSacos);
    }

    private string ObtenerClaveObjeto(Transform t)
    {
        Transform actual = t;
        while (actual != null)
        {
            if (contenidos.ContainsKey(actual.name))
            {
                return actual.name;
            }
            actual = actual.parent;
        }

        if (t.name.IndexOf("Grano", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            t.name.IndexOf("Cacao", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Hotspot_Grano_Cafe";
        }
        if (t.name.IndexOf("Taza", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Hotspot_Taza_Cafe";
        }
        if (t.name.IndexOf("Saco", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            t.name.IndexOf("Estante", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Hotspot_Sacos_Cafe_Garaje";
        }

        return "";
    }

    public void IrAEntrada()
    {
        if (camaraTour == null || modoRealidadAumentada) return;

        if (tableroEntradaTransform != null)
        {
            Renderer rend = tableroEntradaTransform.GetComponent<Renderer>();
            Vector3 objetivoEntrada = rend != null ? rend.bounds.center : tableroEntradaTransform.position;

            float camX = objetivoEntrada.x;
            float camZ = objetivoEntrada.z + distanciaFrenteEntrada;
            float yTerreno = ObtenerAlturaTerreno(camX, camZ);

            camaraTour.transform.position = new Vector3(camX, yTerreno + alturaOjos, camZ);

            Vector3 puntoMira = new Vector3(objetivoEntrada.x, yTerreno + alturaOjos * 0.95f, objetivoEntrada.z - 2.0f);
            Vector3 dir = (puntoMira - camaraTour.transform.position).normalized;
            Quaternion lookRot = Quaternion.LookRotation(dir, Vector3.up);
            Vector3 euler = lookRot.eulerAngles;
            yaw = euler.y;
            pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            camaraTour.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            return;
        }

        MoverAPunto(posEntrada, 180f);
    }

    public void IrAParcelaIzquierda() => MoverAPunto(posParcelaIzquierda, -25f);
    public void IrAParcelaDerecha() => MoverAPunto(posParcelaDerecha, 25f);
    public void IrACasa() => MoverAPunto(posCasaLerida, 0f);

    public void MoverAPunto(Vector3 puntoBase, float nuevoYaw)
    {
        if (camaraTour == null || modoRealidadAumentada) return;

        float yTerreno = ObtenerAlturaTerreno(puntoBase.x, puntoBase.z);
        camaraTour.transform.position = new Vector3(puntoBase.x, yTerreno + alturaOjos, puntoBase.z);
        yaw = nuevoYaw;
        pitch = 0f;
        camaraTour.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

     private bool EstaDentroDeGaraje(float x, float z)
    {
    float mitadAncho = 5.25f;
    float mitadProfundo = 4.5f;
    return Mathf.Abs(x - posGarajeLateral.x) <= mitadAncho &&
           Mathf.Abs(z - posGarajeLateral.z) <= mitadProfundo;
    }

    private float ObtenerAlturaTerreno(float x, float z)
    {
        float yBlender = z;
        float laderaY = Mathf.Max(0f, (yBlender + 8f) * 0.045f);
        float ondulacion = 0.25f * Mathf.Sin(x * 0.22f) * Mathf.Cos(yBlender * 0.18f);
        float distCasa = Mathf.Sqrt(Mathf.Pow((x - 1.5f) * 0.25f, 2f) + Mathf.Pow((yBlender - 8.5f) * 0.35f, 2f));
        float factorPlano = Mathf.Clamp01((distCasa - 0.5f) / 1.5f);
        return (laderaY + ondulacion) * factorPlano;
    }
}
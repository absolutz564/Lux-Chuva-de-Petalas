using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[Serializable] public class LandmarkPoint { public float x, y, z; }
[Serializable] public class HandData      { public List<LandmarkPoint> landmarks; }
[Serializable] public class HandsPayload  { public List<HandData> hands; }

public class HandTracker : MonoBehaviour
{
    public static HandTracker Instance { get; private set; }

    [Header("Network")]
    [SerializeField] private int listenPort = 5005;

    [Header("Glove")]
    [SerializeField] private Color gloveColorOpen   = new Color(0f,   0f,   0f,   0.65f);
    [SerializeField] private Color gloveColorClosed = new Color(0.3f, 0f,   0.5f, 0.80f);
    [SerializeField] private float fingerHalfWidth  = 0.15f;
    [SerializeField] private int   capSegments      = 8;

    // cada dedo: base → pip → dip → tip
    private static readonly int[][] FingerChains =
    {
        new[] {  1,  2,  3,  4 },   // polegar
        new[] {  5,  6,  7,  8 },   // indicador
        new[] {  9, 10, 11, 12 },   // médio
        new[] { 13, 14, 15, 16 },   // anelar
        new[] { 17, 18, 19, 20 },   // mindinho
    };

    // contorno da palma: pulso + base de cada dedo
    private static readonly int[] PalmRing = { 0, 1, 5, 9, 13, 17 };

    // ── Dados públicos ────────────────────────────────────────────────────────

    public int HandCount => _handsWorld.Count;

    public Vector3 GetPalmCenter(int handIndex)
    {
        if (handIndex >= _handsWorld.Count) return Vector3.zero;
        var h = _handsWorld[handIndex];
        return (h[0] + h[5] + h[9] + h[13] + h[17]) / 5f;
    }

    public bool IsHandClosed(int handIndex)
    {
        if (handIndex >= _rawLandmarks.Count) return false;
        var lm = _rawLandmarks[handIndex];
        if (lm.Count < 21) return false;

        float pdx = lm[9].x - lm[0].x;
        float pdy = lm[9].y - lm[0].y;
        float plen = Mathf.Sqrt(pdx * pdx + pdy * pdy);
        if (plen < 0.001f) return false;
        pdx /= plen;
        pdy /= plen;

        int[] tips = { 8, 12, 16, 20 };
        int[] pips = { 6, 10, 14, 18 };

        int closedFingers = 0;
        for (int i = 0; i < 4; i++)
        {
            float vx = lm[tips[i]].x - lm[pips[i]].x;
            float vy = lm[tips[i]].y - lm[pips[i]].y;
            if (vx * pdx + vy * pdy < 0f)
                closedFingers++;
        }
        return closedFingers >= 3;
    }

    public bool IsHandJustClosed(int handIndex)
    {
        if (handIndex >= _handJustClosedTimer.Length) return false;
        return _handJustClosedTimer[handIndex] > 0f;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private struct HandGlove
    {
        public MeshRenderer renderer;
        public Mesh         mesh;
    }

    private HandGlove[]                        _gloves;
    private readonly List<List<Vector3>>       _handsWorld   = new List<List<Vector3>>();
    private readonly List<List<LandmarkPoint>> _rawLandmarks = new List<List<LandmarkPoint>>();

    private UdpClient    _udp;
    private Thread       _thread;
    private string       _pendingJson;
    private readonly object _lock = new object();

    private float _lastHandTime = -99f;
    private const float HandTimeout = 0.5f;

    private bool[]  _prevHandClosed      = new bool[2];
    private float[] _handJustClosedTimer = new float[2];
    private const float GrabGracePeriod  = 0.20f;

    private int   _packetsReceived;
    private float _nextLogTime;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        CreateGloves(2);
        StartUdp();
    }

    // ── UDP ───────────────────────────────────────────────────────────────────

    private void StartUdp()
    {
        try
        {
            _udp = new UdpClient(listenPort);
            _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "HandTrackerUDP" };
            _thread.Start();
            Debug.Log($"[HandTracker] Listening on UDP port {listenPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[HandTracker] UDP init failed: {e.Message}");
        }
    }

    private void ReceiveLoop()
    {
        var ep = new IPEndPoint(IPAddress.Any, listenPort);
        while (true)
        {
            try
            {
                byte[] bytes = _udp.Receive(ref ep);
                lock (_lock) { _pendingJson = Encoding.UTF8.GetString(bytes); }
            }
            catch { break; }
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        string json = null;
        lock (_lock)
        {
            if (_pendingJson != null) { json = _pendingJson; _pendingJson = null; }
        }

        if (json != null) { _packetsReceived++; ParseHands(json); }

        for (int h = 0; h < _prevHandClosed.Length; h++)
        {
            bool closed = IsHandClosed(h);
            if (closed && !_prevHandClosed[h])
                _handJustClosedTimer[h] = GrabGracePeriod;
            else if (_handJustClosedTimer[h] > 0f)
                _handJustClosedTimer[h] -= Time.deltaTime;
            _prevHandClosed[h] = closed;
        }

        if (Time.time >= _nextLogTime)
        {
            _nextLogTime = Time.time + 3f;
            string info = "";
            for (int h = 0; h < HandCount; h++)
                info += $" Mão{h}:{(IsHandClosed(h) ? "PUNHO" : "aberta")}";
            Debug.Log($"[HandTracker] Pacotes: {_packetsReceived} | Mãos: {HandCount}{info}");
        }

        bool handsVisible = HandCount > 0 && Time.time - _lastHandTime < HandTimeout;
        RenderGloves(handsVisible);
    }

    // ── Parsing ───────────────────────────────────────────────────────────────

    private void ParseHands(string json)
    {
        try
        {
            var payload = JsonUtility.FromJson<HandsPayload>(json);
            _handsWorld.Clear();
            _rawLandmarks.Clear();

            if (payload?.hands == null || payload.hands.Count == 0) return;

            _lastHandTime = Time.time;

            foreach (var hand in payload.hands)
            {
                if (hand.landmarks == null || hand.landmarks.Count != 21) continue;

                _rawLandmarks.Add(hand.landmarks);

                var pts = new List<Vector3>(21);
                foreach (var lm in hand.landmarks)
                {
                    float sx = lm.x * Screen.width;
                    float sy = (1f - lm.y) * Screen.height;
                    Vector3 world = Camera.main.ScreenToWorldPoint(new Vector3(sx, sy, 10f));
                    world.z = 0f;
                    pts.Add(world);
                }
                _handsWorld.Add(pts);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[HandTracker] Parse error: {e.Message}");
        }
    }

    // ── Glove rendering ───────────────────────────────────────────────────────

    private void CreateGloves(int maxHands)
    {
        _gloves = new HandGlove[maxHands];
        for (int h = 0; h < maxHands; h++)
        {
            var go = new GameObject($"Glove_H{h}");
            go.transform.SetParent(transform);

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.material       = new Material(Shader.Find("Sprites/Default"));
            mr.material.color = gloveColorOpen;
            mr.sortingOrder   = 10;
            mr.enabled        = false;

            var mesh = new Mesh { name = $"GloveMesh_H{h}" };
            mf.mesh = mesh;

            _gloves[h] = new HandGlove { renderer = mr, mesh = mesh };
        }
    }

    private void RenderGloves(bool handsVisible)
    {
        for (int h = 0; h < _gloves.Length; h++)
        {
            bool show = handsVisible && h < _handsWorld.Count;
            _gloves[h].renderer.enabled = show;

            if (!show) { _gloves[h].mesh.Clear(); continue; }

            _gloves[h].renderer.material.color = IsHandClosed(h) ? gloveColorClosed : gloveColorOpen;
            BuildGloveMesh(_gloves[h].mesh, _handsWorld[h]);
        }
    }

    private void BuildGloveMesh(Mesh mesh, List<Vector3> pts)
    {
        var verts = new List<Vector3>();
        var tris  = new List<int>();

        FillPalm(pts, verts, tris);

        foreach (var chain in FingerChains)
            FillFinger(pts, chain, verts, tris);

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
    }

    // Palma preenchida: fan do centroide aos landmarks expandidos para fora
    private void FillPalm(List<Vector3> pts, List<Vector3> verts, List<int> tris)
    {
        Vector3 centroid = Vector3.zero;
        foreach (int idx in PalmRing)
            centroid += pts[idx];
        centroid /= PalmRing.Length;

        int center = verts.Count;
        verts.Add(new Vector3(centroid.x, centroid.y, 0f));

        foreach (int idx in PalmRing)
        {
            Vector3 p   = pts[idx];
            Vector3 dir = p - centroid;
            float   mag = dir.magnitude;
            if (mag > 0.001f)
                dir = (dir / mag) * (mag + fingerHalfWidth);
            verts.Add(new Vector3(centroid.x + dir.x, centroid.y + dir.y, 0f));
        }

        for (int i = 0; i < PalmRing.Length; i++)
        {
            tris.Add(center);
            tris.Add(center + 1 + i);
            tris.Add(center + 1 + (i + 1) % PalmRing.Length);
        }
    }

    // Dedo preenchido: faixa de quads + semicírculo na ponta
    private void FillFinger(List<Vector3> pts, int[] chain, List<Vector3> verts, List<int> tris)
    {
        var leftEdge  = new Vector3[chain.Length];
        var rightEdge = new Vector3[chain.Length];

        for (int i = 0; i < chain.Length; i++)
        {
            Vector3 pt  = pts[chain[i]];
            Vector3 dir = Vector3.zero;

            if (i > 0)              dir += (pt - pts[chain[i - 1]]).normalized;
            if (i < chain.Length-1) dir += (pts[chain[i + 1]] - pt).normalized;
            if (dir.magnitude < 0.001f)
                dir = i > 0 ? (pt - pts[chain[i - 1]]).normalized : Vector3.up;
            dir.Normalize();

            Vector3 perp = new Vector3(-dir.y, dir.x, 0f) * fingerHalfWidth;
            leftEdge[i]  = new Vector3(pt.x - perp.x, pt.y - perp.y, 0f);
            rightEdge[i] = new Vector3(pt.x + perp.x, pt.y + perp.y, 0f);
        }

        // Faixa de quads ao longo do dedo
        for (int i = 0; i < chain.Length - 1; i++)
        {
            int b = verts.Count;
            verts.Add(leftEdge[i]);
            verts.Add(rightEdge[i]);
            verts.Add(rightEdge[i + 1]);
            verts.Add(leftEdge[i + 1]);

            tris.Add(b);   tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b);   tris.Add(b + 2); tris.Add(b + 3);
        }

        // Semicírculo na ponta: +perp → +dir → -perp
        int     last     = chain.Length - 1;
        Vector3 tip      = pts[chain[last]];
        Vector3 tipDir   = (tip - pts[chain[last - 1]]).normalized;
        Vector3 tipPerp  = new Vector3(-tipDir.y, tipDir.x, 0f);

        int capCenter = verts.Count;
        verts.Add(new Vector3(tip.x, tip.y, 0f));

        for (int s = 0; s <= capSegments; s++)
        {
            float   a      = Mathf.PI * s / capSegments;
            Vector3 offset = (tipPerp * Mathf.Cos(a) + tipDir * Mathf.Sin(a)) * fingerHalfWidth;
            verts.Add(new Vector3(tip.x + offset.x, tip.y + offset.y, 0f));
        }

        for (int s = 0; s < capSegments; s++)
        {
            tris.Add(capCenter);
            tris.Add(capCenter + 1 + s);
            tris.Add(capCenter + 2 + s);
        }
    }

    private void OnDestroy()
    {
        _thread?.Abort();
        _udp?.Close();
        if (_gloves != null)
            foreach (var g in _gloves)
                if (g.mesh != null) Destroy(g.mesh);
    }
}

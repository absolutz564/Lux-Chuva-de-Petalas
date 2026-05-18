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

    [Header("Skeleton")]
    [SerializeField] private Color skeletonColorOpen   = Color.cyan;
    [SerializeField] private Color skeletonColorClosed = Color.green;
    [SerializeField] private float lineWidth = 0.04f;

    // MediaPipe connections entre os 21 landmarks
    private static readonly int[][] Connections =
    {
        new[]{0,1},  new[]{1,2},  new[]{2,3},  new[]{3,4},
        new[]{0,5},  new[]{5,6},  new[]{6,7},  new[]{7,8},
        new[]{0,9},  new[]{9,10}, new[]{10,11},new[]{11,12},
        new[]{0,13}, new[]{13,14},new[]{14,15},new[]{15,16},
        new[]{0,17}, new[]{17,18},new[]{18,19},new[]{19,20},
        new[]{5,9},  new[]{9,13}, new[]{13,17}
    };

    // ── Dados públicos ────────────────────────────────────────────────────────

    public int HandCount => _handsWorld.Count;

    /// Retorna o centro da palma em world-space para o índice de mão indicado.
    public Vector3 GetPalmCenter(int handIndex)
    {
        if (handIndex >= _handsWorld.Count) return Vector3.zero;
        var h = _handsWorld[handIndex];
        return (h[0] + h[5] + h[9] + h[13] + h[17]) / 5f;
    }

    /// Retorna true quando a mão está fechada em punho.
    /// Detecta com produto escalar em relação à direção da palma,
    /// funcionando independentemente da inclinação da mão.
    public bool IsHandClosed(int handIndex)
    {
        if (handIndex >= _rawLandmarks.Count) return false;
        var lm = _rawLandmarks[handIndex];
        if (lm.Count < 21) return false;

        // Direção da palma: pulso (0) → articulação do dedo médio (9)
        float pdx = lm[9].x - lm[0].x;
        float pdy = lm[9].y - lm[0].y;
        float plen = Mathf.Sqrt(pdx * pdx + pdy * pdy);
        if (plen < 0.001f) return false;
        pdx /= plen;
        pdy /= plen;

        // Dedo fechado: ponta (tip) está "atrás" da segunda articulação (pip)
        // em relação à direção da palma → produto escalar (tip-pip)·palmDir < 0
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

    /// Retorna true nos 0.2 s seguintes ao momento em que a mão fechou.
    /// Use este método para exigir o gesto de fechar, não o estado constante.
    public bool IsHandJustClosed(int handIndex)
    {
        if (handIndex >= _handJustClosedTimer.Length) return false;
        return _handJustClosedTimer[handIndex] > 0f;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private readonly List<LineRenderer[]>        _skeletons    = new List<LineRenderer[]>();
    private readonly List<List<Vector3>>         _handsWorld   = new List<List<Vector3>>();
    private readonly List<List<LandmarkPoint>>   _rawLandmarks = new List<List<LandmarkPoint>>();

    private UdpClient _udp;
    private Thread    _thread;
    private string    _pendingJson;
    private readonly object _lock = new object();

    private float _lastHandTime  = -99f;
    private const float HandTimeout = 0.5f;

    private bool[]  _prevHandClosed       = new bool[2];
    private float[] _handJustClosedTimer  = new float[2];
    private const float GrabGracePeriod   = 0.20f;

    private int   _packetsReceived;
    private float _nextLogTime;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        CreateSkeletons(2);
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
            string fistInfo = "";
            for (int h = 0; h < HandCount; h++)
                fistInfo += $" Mão{h}:{(IsHandClosed(h) ? "PUNHO" : "aberta")}";
            Debug.Log($"[HandTracker] Pacotes: {_packetsReceived} | Mãos: {HandCount}{fistInfo}");
        }

        bool handsVisible = HandCount > 0 && Time.time - _lastHandTime < HandTimeout;
        RenderSkeletons(handsVisible);
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
                    // Flip Y: MediaPipe y=0 é topo da imagem, Unity y=0 é base da tela.
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

    // ── Skeleton rendering ────────────────────────────────────────────────────

    private void CreateSkeletons(int maxHands)
    {
        for (int h = 0; h < maxHands; h++)
        {
            var lines = new LineRenderer[Connections.Length];
            for (int i = 0; i < Connections.Length; i++)
            {
                var go = new GameObject($"Skel_H{h}_L{i}");
                go.transform.SetParent(transform);

                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth    = lineWidth;
                lr.endWidth      = lineWidth;
                lr.useWorldSpace = true;
                lr.material      = new Material(Shader.Find("Sprites/Default"));
                lr.startColor    = skeletonColorOpen;
                lr.endColor      = skeletonColorOpen;
                lr.sortingOrder  = 10;
                lr.enabled       = false;
                lines[i] = lr;
            }
            _skeletons.Add(lines);
        }
    }

    private void RenderSkeletons(bool handsVisible)
    {
        for (int h = 0; h < _skeletons.Count; h++)
        {
            bool show  = handsVisible && h < _handsWorld.Count;
            var  lines = _skeletons[h];

            Color c = (show && IsHandClosed(h)) ? skeletonColorClosed : skeletonColorOpen;

            for (int i = 0; i < lines.Length; i++)
            {
                lines[i].enabled = show;
                if (!show) continue;

                var hand = _handsWorld[h];
                int a = Connections[i][0], b = Connections[i][1];
                if (a < hand.Count && b < hand.Count)
                {
                    lines[i].SetPosition(0, hand[a]);
                    lines[i].SetPosition(1, hand[b]);
                    lines[i].startColor = c;
                    lines[i].endColor   = c;
                }
            }
        }
    }

    private void OnDestroy()
    {
        _thread?.Abort();
        _udp?.Close();
    }
}

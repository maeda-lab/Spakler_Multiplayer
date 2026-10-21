using System.Collections.Generic;
using QuestLAN.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace QuestLAN.Integration
{
    /// <summary>Head-gaze dwell controls usable without controller or hand input bindings.</summary>
    public sealed class VRSessionPanel : MonoBehaviour
    {
        private sealed class Control { public RectTransform Rect; public Text Text; public System.Action Action; }
        [SerializeField] private Camera headCamera;
        [SerializeField] private Transform rigRoot;
        private readonly List<Control> controls = new List<Control>();
        private LanSessionController session;
        private NetworkManager manager;
        private Transform panel;
        private Text status;
        private Image pointer;
        private string address = "127.0.0.1", port = "7777";
        private bool editPort;
        private Control focused;
        private double focusedSince;
        private bool fired;
        public void Configure(Camera camera, Transform rig) { headCamera = camera; rigRoot = rig; }

        private void Start()
        {
            session = GetComponent<LanSessionController>(); manager = GetComponent<NetworkManager>();
            var canvas = new GameObject("LAN gaze panel", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = headCamera;
            panel = canvas.transform;
            panel.SetParent(rigRoot, false);
            panel.localPosition = new Vector3(0, 1.4f, 2);
            panel.localScale = Vector3.one * 0.001f;
            ((RectTransform)panel).sizeDelta = new Vector2(1600, 2000);
            status = Label("Status", panel, new Vector2(0, 370), new Vector2(1600, 240), 36);
            Button("Host", -0.5f, 0.1f, () => StartSession(true));
            Button("Client", 0, 0.1f, () => StartSession(false));
            Button("Stop", 0.5f, 0.1f, () => session.Disconnect());
            Button("Color", -0.5f, -0.1f, () => LocalSignals()?.RequestColorChange());
            Button("IP / Port", 0, -0.1f, () => editPort = !editPort);
            Button("Clear", 0.5f, -0.1f, () => SetEntry(""));
            var keys = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", ".", "0", "<" };
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                Button(key, -0.5f + (i % 3) * 0.5f, -0.3f - (i / 3) * 0.18f, () =>
                {
                    string entry = editPort ? port : address;
                    if (key == "<") SetEntry(entry.Length > 0 ? entry.Substring(0, entry.Length - 1) : entry);
                    else if (entry.Length < (editPort ? 5 : 15) && (!editPort || key != ".")) SetEntry(entry + key);
                });
            }
            pointer = new GameObject("Head pointer", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            pointer.transform.SetParent(panel, false);
            pointer.rectTransform.anchorMin = pointer.rectTransform.anchorMax = Vector2.one * 0.5f;
            pointer.rectTransform.sizeDelta = new Vector2(14, 14);
            pointer.color = Color.white; pointer.raycastTarget = false;
        }

        private void SetEntry(string value) { if (editPort) port = value; else address = value; }
        private void StartSession(bool host)
        {
            if (!ushort.TryParse(port, out ushort parsed) || parsed == 0) return;
            if (host) session.StartHost(address, parsed); else session.StartClient(address, parsed);
        }
        private NetworkPlayerSignals LocalSignals() => manager.LocalClient?.PlayerObject
            ? manager.LocalClient.PlayerObject.GetComponent<NetworkPlayerSignals>() : null;

        private void Update()
        {
            if (!panel || !headCamera) return;
            var signals = manager.IsConnectedClient ? LocalSignals() : null;
            string latency = signals && !manager.IsHost ? signals.Rtt.TimedOut ? "RTT timeout" :
                signals.Rtt.HasSample ? $"RTT {signals.Rtt.CurrentMilliseconds:F1} / avg {signals.Rtt.SmoothedMilliseconds:F1} ms" : "RTT waiting" : "";
            string color = signals ? $"Your cube (right): {signals.ColorIndex.Value}" : "";
            status.text = $"{session.Status} | {address}:{port}\nMove head: hold dot on button 1.2s | Edit {(editPort ? "PORT" : "IP")}\n{latency}\n{color}";
            Control hit = null;
            pointer.gameObject.SetActive(false);
            var ray = new Ray(headCamera.transform.position, headCamera.transform.forward);
            var plane = new Plane(panel.forward, panel.position);
            if (plane.Raycast(ray, out float distance) && distance < 5)
            {
                var world = ray.GetPoint(distance);
                var canvasPoint = panel.InverseTransformPoint(world);
                pointer.rectTransform.anchoredPosition = new Vector2(canvasPoint.x, canvasPoint.y);
                pointer.gameObject.SetActive(((RectTransform)panel).rect.Contains(new Vector2(canvasPoint.x, canvasPoint.y)));
                foreach (var control in controls)
                {
                    var local = control.Rect.InverseTransformPoint(world);
                    if (control.Rect.rect.Contains(new Vector2(local.x, local.y))) { hit = control; break; }
                }
            }
            pointer.color = hit == null ? Color.white : Color.yellow;
            if (hit != focused)
            {
                if (focused != null) focused.Text.color = Color.white;
                focused = hit; focusedSince = Time.realtimeSinceStartupAsDouble; fired = false;
            }
            if (focused == null) return;
            focused.Text.color = fired ? Color.green : Color.yellow;
            pointer.color = fired ? Color.green : Color.yellow;
            if (!fired && Time.realtimeSinceStartupAsDouble - focusedSince >= 1.2)
            {
                fired = true;
                session.Log("Gaze activated: " + focused.Text.text);
                focused.Action();
            }
        }

        private Text Label(string label, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var text = new GameObject(label, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = Vector2.one * 0.5f;
            text.rectTransform.pivot = Vector2.one * 0.5f;
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize; text.text = label;
            text.color = Color.white; text.raycastTarget = false;
            return text;
        }
        private void Button(string label, float x, float y, System.Action action)
        {
            var background = new GameObject(label, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            background.transform.SetParent(panel, false);
            background.rectTransform.anchorMin = background.rectTransform.anchorMax = Vector2.one * 0.5f;
            background.rectTransform.pivot = Vector2.one * 0.5f;
            background.rectTransform.anchoredPosition = new Vector2(x * 1000, y * 1000);
            background.rectTransform.sizeDelta = new Vector2(440, 140);
            background.color = new Color(0.025f, 0.05f, 0.1f, 0.95f);
            background.raycastTarget = false;
            controls.Add(new Control { Rect = background.rectTransform,
                Text = Label(label, background.transform, Vector2.zero, new Vector2(440, 140), 56), Action = action });
        }
        private void OnDestroy()
        {
            if (panel) Destroy(panel.gameObject);
        }
    }
}

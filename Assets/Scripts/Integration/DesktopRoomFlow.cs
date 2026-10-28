using System.Collections;
using QuestLAN.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace QuestLAN.Integration
{
    /// <summary>Desktop lobby first; initialize XR only after the selected LAN session connects.</summary>
    public sealed class DesktopRoomFlow : MonoBehaviour
    {
        public static DesktopRoomFlow Instance { get; private set; }
        public bool RoomEntered { get; private set; }
        public bool IsBusy { get; private set; }
        public bool IsLobbyVisible => menu && menu.activeSelf;
        public InputField AddressInput { get; private set; }
        public InputField PortInput { get; private set; }
        public Button HostButton { get; private set; }
        public Button JoinButton { get; private set; }
        public string StatusText => status ? status.text : "";
        private GameObject menu, toolbar;
        private Camera desktopCamera;
        private Text status, roomStatus, localAddressLabel;
        public string DetectedLocalIPv4 { get; private set; }
        private Button disconnect, retry;
        private LanSessionController session;
        private NetworkManager manager;
        private XRManagerSettings xr;
        private bool hadConnection, ownsXR, ownsInstance, cancelRequested;
        private bool HeadlessValidation => Application.isBatchMode && HasArgument("--lobby-no-xr");

        private static bool HasArgument(string value) => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), value) >= 0;
        private void Awake()
        {
            // Existing isolated MainScene tests/probes retain their explicit no-loader path.
            if (Application.isBatchMode && !HasArgument("--lobby-probe")) { enabled = false; return; }
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this; ownsInstance = true;
            DontDestroyOnLoad(gameObject);
            BuildUI();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            status.text = "Choose Host or enter a Host IPv4 and select Join. VR starts after connection.";
        }

        private void Update()
        {
            if (!ownsInstance) return;
            bool connected = manager && manager.IsConnectedClient;
            if (connected) hadConnection = true;
            if (hadConnection && !connected && !IsBusy) { StartCoroutine(ReturnToLobby()); return; }
            HostButton.interactable = JoinButton.interactable = !IsBusy && !connected;
            AddressInput.interactable = PortInput.interactable = !IsBusy && !connected;
            disconnect.interactable = session && (session.CanDisconnect || connected);
            retry.interactable = connected && !IsBusy && !RoomEntered;
            if (RoomEntered) roomStatus.text = $"{session.Role} | Players: {session.PlayerCount}/2 | Left stick: move toward head direction";
        }

        public void Connect(bool host)
        {
            if (IsBusy || (manager && manager.IsConnectedClient)) return;
            if (!ushort.TryParse(PortInput.text, out ushort port) || port == 0)
            { status.text = "Enter a UDP port from 1 to 65535."; return; }
            if (!host && (!System.Net.IPAddress.TryParse(AddressInput.text, out var ip) ||
                ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork))
            { status.text = "Enter a valid Host IPv4 address."; return; }
            StartCoroutine(ConnectRoutine(host, AddressInput.text, port));
        }

        private IEnumerator ConnectRoutine(bool host, string address, ushort port)
        {
            IsBusy = true; cancelRequested = false; status.text = "Loading room...";
            if (!SceneManager.GetSceneByName("MainScene").isLoaded)
                yield return SceneManager.LoadSceneAsync("MainScene");
            session = FindFirstObjectByType<XRNetworkBinding>().GetComponent<LanSessionController>();
            manager = session.GetComponent<NetworkManager>();
            session.DesktopControlsVisible = false;
            session.HostAddress = address; session.PortText = port.ToString();
            bool started = host ? session.StartHost("127.0.0.1", port) : session.StartClient(address, port);
            if (!started) { status.text = session.Status; IsBusy = false; yield break; }
            status.text = host ? "Starting Host..." : "Connecting to Host...";
            double deadline = Time.realtimeSinceStartupAsDouble + 12;
            while (!manager.IsConnectedClient && !session.CanStart && !cancelRequested && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            if (!manager.IsConnectedClient || cancelRequested)
            {
                status.text = cancelRequested ? "Connection canceled." : session.Status;
                session.Disconnect();
                while (manager && manager.ShutdownInProgress) yield return null;
                IsBusy = false; yield break;
            }
            hadConnection = true;
            yield return StartVR();
            IsBusy = false;
        }

        public void RetryVR()
        {
            if (!IsBusy && manager && manager.IsConnectedClient) StartCoroutine(RetryRoutine());
        }
        private IEnumerator RetryRoutine() { IsBusy = true; yield return StartVR(); IsBusy = false; }

        private IEnumerator StartVR()
        {
            status.text = "Connected. Starting Meta Link / OpenXR...";
            if (!HeadlessValidation)
            {
                xr = XRGeneralSettings.Instance ? XRGeneralSettings.Instance.Manager : null;
                if (!xr) { status.text = "XR settings unavailable. Disconnect and check the project configuration."; yield break; }
                if (!xr.isInitializationComplete) yield return xr.InitializeLoader();
                if (xr.activeLoader)
                {
                    ownsXR = true; xr.StartSubsystems();
                    var display = xr.activeLoader.GetLoadedSubsystem<XRDisplaySubsystem>();
                    double readyDeadline = Time.realtimeSinceStartupAsDouble + 10;
                    while ((display == null || !display.running) && manager && manager.IsConnectedClient &&
                        !cancelRequested && Time.realtimeSinceStartupAsDouble < readyDeadline)
                    {
                        yield return null;
                        display = xr.activeLoader ? xr.activeLoader.GetLoadedSubsystem<XRDisplaySubsystem>() : null;
                    }
                    if (display == null || !display.running)
                    { StopXR(); status.text = "VR unavailable. Enter Meta Link, then click Retry VR or Disconnect."; yield break; }
                }
                else
                {
                    if (xr.isInitializationComplete) xr.DeinitializeLoader();
                    status.text = "VR unavailable. Enter Meta Link, then click Retry VR or Disconnect."; yield break;
                }
            }
            if (cancelRequested || !manager || !manager.IsConnectedClient) { StopXR(); yield break; }
            RoomEntered = true;
            menu.SetActive(false); toolbar.SetActive(true); desktopCamera.enabled = false;
        }

        public void Disconnect()
        {
            if (IsBusy) { cancelRequested = true; if (session) session.Disconnect(); return; }
            StartCoroutine(ReturnToLobby());
        }
        private IEnumerator ReturnToLobby()
        {
            IsBusy = true; RoomEntered = false; hadConnection = false;
            menu.SetActive(true); toolbar.SetActive(false); desktopCamera.enabled = true;
            status.text = "Leaving room...";
            if (session) session.Disconnect();
            while (manager && manager.ShutdownInProgress) yield return null;
            StopXR();
            // NGO keeps the NetworkManager root across scenes. Destroy the stopped room
            // before unloading its Rig, otherwise a second connection reuses stale refs.
            if (manager) { Destroy(manager.gameObject); yield return null; }
            session = null; manager = null;
            if (SceneManager.GetActiveScene().name != "DesktopLobby")
                yield return SceneManager.LoadSceneAsync("DesktopLobby");
            session = null; manager = null;
            status.text = "Offline | Players: 0/2. Choose Host or Join.";
            IsBusy = false;
        }

        private void StopXR()
        {
            if (ownsXR && xr && xr.isInitializationComplete) xr.DeinitializeLoader();
            ownsXR = false;
        }
        private void OnDestroy()
        {
            if (!ownsInstance) return;
            StopXR();
            if (Instance == this) Instance = null;
        }

        private void BuildUI()
        {
            var cameraObject = new GameObject("Desktop lobby camera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            desktopCamera = cameraObject.GetComponent<Camera>();
            desktopCamera.clearFlags = CameraClearFlags.SolidColor; desktopCamera.backgroundColor = new Color(0.025f, 0.04f, 0.07f);
            desktopCamera.cullingMask = 0; desktopCamera.depth = 100;
            desktopCamera.stereoTargetEye = StereoTargetEyeMask.None;
            var canvas = new GameObject("Desktop lobby UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 800);
            if (!FindFirstObjectByType<EventSystem>())
            {
                var events = new GameObject("Desktop input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                var input = events.GetComponent<InputSystemUIInputModule>();
                if (!input.actionsAsset) input.AssignDefaultActions();
            }
            menu = new GameObject("Lobby menu", typeof(RectTransform), typeof(Image));
            menu.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)menu.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            menu.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.07f, 1);
            Label(menu.transform, "QuestLAN - Desktop Lobby", new Vector2(0, 285), new Vector2(1000, 60), 36);
            Label(menu.transform, "Host IP (IPv4)", new Vector2(-220, 190), new Vector2(460, 40), 24);
            Label(menu.transform, "UDP Port", new Vector2(280, 190), new Vector2(220, 40), 24);
            DetectedLocalIPv4 = LocalIPv4Detector.Detect();
            localAddressLabel = Label(menu.transform, "Local IPv4: " + DetectedLocalIPv4, new Vector2(0, 235), new Vector2(1100, 30), 20);
            AddressInput = Field("Host IP", new Vector2(-220, 130), new Vector2(460, 64), DetectedLocalIPv4, 15);
            AddressInput.onValidateInput = (_, _, ch) => ch >= '0' && ch <= '9' || ch == '.' ? ch : '\0';
            PortInput = Field("UDP Port", new Vector2(280, 130), new Vector2(220, 64), "7777", 5);
            PortInput.onValidateInput = (_, _, ch) => ch >= '0' && ch <= '9' ? ch : '\0';
            HostButton = Control(menu.transform, "Create Host", new Vector2(-240, 20), () => Connect(true));
            JoinButton = Control(menu.transform, "Join", new Vector2(80, 20), () => Connect(false));
            Control(menu.transform, "Refresh IP", new Vector2(400, 20), () =>
            {
                if (IsBusy || (manager && manager.IsConnectedClient)) return;
                DetectedLocalIPv4 = LocalIPv4Detector.Detect(); AddressInput.text = DetectedLocalIPv4;
                localAddressLabel.text = "Local IPv4: " + DetectedLocalIPv4;
            });
            disconnect = Control(menu.transform, "Disconnect", new Vector2(-240, -80), Disconnect);
            retry = Control(menu.transform, "Retry VR", new Vector2(80, -80), RetryVR);
            Control(menu.transform, "Show Status", new Vector2(400, -80), () =>
            { if (session && !IsBusy) status.text = $"{session.Status} | {session.Role} | Players: {session.PlayerCount}/2"; });
            status = Label(menu.transform, "", new Vector2(0, -210), new Vector2(1120, 150), 24);
            toolbar = new GameObject("Room toolbar", typeof(RectTransform)); toolbar.transform.SetParent(canvas.transform, false);
            var bar = (RectTransform)toolbar.transform; bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1); bar.anchoredPosition = new Vector2(0, -45);
            roomStatus = Label(toolbar.transform, "", new Vector2(-120, 0), new Vector2(900, 60), 22);
            Control(toolbar.transform, "Disconnect", new Vector2(480, 0), Disconnect);
            toolbar.SetActive(false);
        }
        private InputField Field(string name, Vector2 position, Vector2 size, string value, int limit)
        {
            var field = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            field.transform.SetParent(menu.transform, false); Place((RectTransform)field.transform, position, size);
            field.GetComponent<Image>().color = new Color(0.9f, 0.94f, 1);
            var text = Label(field.transform, "", Vector2.zero, size - new Vector2(24, 8), 30); text.color = Color.black; text.alignment = TextAnchor.MiddleLeft;
            var input = field.GetComponent<InputField>(); input.textComponent = text; input.characterLimit = limit;
            input.text = value; input.lineType = InputField.LineType.SingleLine;
            return input;
        }
        private static Button Control(Transform parent, string name, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); item.transform.SetParent(parent, false);
            Place((RectTransform)item.transform, position, new Vector2(280, 66));
            item.GetComponent<Image>().color = new Color(0.08f, 0.3f, 0.55f);
            var button = item.GetComponent<Button>(); button.targetGraphic = item.GetComponent<Image>(); button.onClick.AddListener(action);
            Label(item.transform, name, Vector2.zero, new Vector2(270, 60), 26);
            return button;
        }
        private static Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var item = new GameObject("Label", typeof(RectTransform), typeof(Text)); item.transform.SetParent(parent, false);
            Place((RectTransform)item.transform, position, size);
            var text = item.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.fontSize = fontSize; text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            return text;
        }
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f; rect.anchoredPosition = position; rect.sizeDelta = size; }
    }
}

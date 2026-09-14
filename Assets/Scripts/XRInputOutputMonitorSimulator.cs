using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

public class XRInputOutputMonitorSimulator : MonoBehaviour
{
    [Header("Output visual")]
    [Tooltip("Teks pada Canvas world-space untuk menampilkan data input.")]
    public TextMeshProUGUI statusText;

    [Tooltip("Objek yang berubah warna & skala sebagai umpan balik visual.")]
    public Transform feedbackObject;

    [Header("Output audio")]
    [Tooltip("AudioSource dengan Spatial Blend = 1 (audio 3D).")]
    public AudioSource beepSource;

    [Header("Output haptik")]
    [Range(0f, 1f)] public float hapticAmplitude = 0.7f;
    [Range(0f, 1f)] public float hapticDuration = 0.15f;

    [Header("Head Input")]
    public InputActionReference headPosition;
    public InputActionReference headRotation;


    [Header("Controllers")]
    public ActionBasedController leftController;
    public InputActionReference leftMoveAction;
    public ActionBasedController rightController;
    public InputActionReference rightMoveAction;

    private readonly StringBuilder _sb = new StringBuilder(1024);
    private Renderer _feedbackRenderer;
    private bool _prevTriggerL, _prevTriggerR;
    private string _lastEvent = "-";

    private void Start()
    {
        if (feedbackObject != null)
        {
            _feedbackRenderer =
                feedbackObject.GetComponent<Renderer>();
        }
    }

    void Update()
    {
        _sb.Clear();
        _sb.AppendLine("== INPUT MONITOR (Pertemuan 2) ==");

        // ------------------------------------------------------------------
        // 1) INPUT: pose kepala. Inilah sumber 6DOF pada sistem VR.
        // ------------------------------------------------------------------
        if (headPosition.action.enabled && headRotation.action.enabled)
        {
            Vector3 hp = headPosition.action.ReadValue<Vector3>();
            Quaternion hr = headRotation.action.ReadValue<Quaternion>();
            Vector3 euler = hr.eulerAngles;

            _sb.AppendFormat("HEAD  pos : {0:F2} / {1:F2} / {2:F2}\n", hp.x, hp.y, hp.z);
            _sb.AppendFormat("HEAD  rot : yaw {0:F1}  pitch {1:F1}  roll {2:F1}\n",
                             euler.y, euler.x, euler.z);
        }
        else
        {
            _sb.AppendLine("HEAD      : belum terdeteksi (jalankan XR Device Simulator)");
        }

        // // ------------------------------------------------------------------
        // // 2) INPUT: controller kiri & kanan (pose + nilai tombol analog)
        // // ------------------------------------------------------------------
        ActionBasedController[] controllers = new[] { leftController, rightController };
        for (int i = 0; i < controllers.Length; i++)
        {
            string label = i == 0 ? "Left" : "Right";
            ActionBasedController controller = controllers[i];

            if (controller == null)
            {
                _sb.AppendFormat("{0}     : tidak terhubung\n", label);
                continue;
            }

            ref bool prevPressed = ref (i == 0 ? ref _prevTriggerL : ref _prevTriggerR);
            InputActionReference moveAction = i == 0 ? leftMoveAction : rightMoveAction;

            Vector3 pos = controller.positionAction.action.ReadValue<Vector3>();
            float trigger = controller.activateActionValue.action.ReadValue<float>();  // analog 0..1
            bool pressed = controller.activateAction.action.IsPressed();
            float grip = controller.selectActionValue.action.ReadValue<float>();
            Vector2 stick = moveAction.action.ReadValue<Vector2>();

            _sb.AppendFormat("{0} pos : {1:F2} / {2:F2} / {3:F2}\n", label, pos.x, pos.y, pos.z);
            _sb.AppendFormat("{0} in  : trig {1:F2}  grip {2:F2}  stick {3:F2},{4:F2}\n",
                             label, trigger, grip, stick.x, stick.y);

            // Deteksi transisi tidak-ditekan -> ditekan (edge detection)
            if (pressed && !prevPressed) FireOutputs(controller, label.Trim());
            prevPressed = pressed;
        }

        // // ------------------------------------------------------------------
        // // 3) INPUT: percepatan perangkat (IMU) - relevan untuk AR mobile
        // // ------------------------------------------------------------------
        // if (_head.isValid &&
        //     _head.TryGetFeatureValue(XRCommonUsages.deviceAcceleration, out Vector3 acc))
        // {
        //     _sb.AppendFormat("IMU   acc : {0:F2} / {1:F2} / {2:F2} m/s2\n", acc.x, acc.y, acc.z);
        // }

        _sb.AppendFormat("\nOUTPUT    : {0:F0} fps  |  {1}", 1f / Mathf.Max(Time.deltaTime, 0.0001f), _lastEvent);

        // ------------------------------------------------------------------
        // 4) OUTPUT VISUAL: tulis ke UI world-space + relaksasi skala objek
        // ------------------------------------------------------------------
        if (statusText != null) statusText.text = _sb.ToString();

        if (feedbackObject != null)
        {
            feedbackObject.localScale = Vector3.Lerp(
                feedbackObject.localScale, Vector3.one, Time.deltaTime * 6f);
            feedbackObject.Rotate(Vector3.up, 30f * Time.deltaTime, Space.Self);
        }
    }

    /// <summary>
    /// Satu aksi INPUT dibalas dengan tiga kanal OUTPUT sekaligus:
    /// visual, audio spasial, dan haptik.
    /// </summary>
    void FireOutputs(ActionBasedController controller, string source)
    {
        // (a) OUTPUT VISUAL
        if (_feedbackRenderer != null)
            _feedbackRenderer.material.color = Random.ColorHSV(0f, 1f, 0.5f, 0.9f, 0.8f, 1f);
        if (feedbackObject != null)
            feedbackObject.localScale = Vector3.one * 1.4f;

        // (b) OUTPUT AUDIO 3D - AudioSource harus Spatial Blend = 1
        if (beepSource != null)
        {
            beepSource.pitch = Random.Range(0.8f, 1.3f);
            beepSource.Play();
        }

        //  --- XR Device Simulator tidak bisa mensimulasikan haptic ---
        // (c) OUTPUT HAPTIK
        controller.SendHapticImpulse(hapticAmplitude, hapticDuration);
        string hapticInfo = $"impulse {hapticAmplitude:F1} / {hapticDuration:F2}s";

        _lastEvent = $"select dari {source} -> {hapticInfo}";
    }
}

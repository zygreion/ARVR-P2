// =============================================================================
//  XRInputOutputMonitor.cs
//  Mata Kuliah : Realitas Virtual dan Realitas Augmentasi (SE.1) - INF124312
//  Pertemuan 2 : Input dan Output  |  Pengayaan Unity
//  Prodi Informatika - Fakultas Ilmu Komputer - UPN Veteran Jakarta
//
//  Fungsi:
//    INPUT  -> membaca pose kepala (6DOF), pose & tombol controller, dan
//              percepatan perangkat melalui API UnityEngine.XR.
//    OUTPUT -> menuliskan data tersebut ke UI world-space (visual),
//              memutar audio 3D, dan mengirim getaran (haptic impulse).
//
//  Kebutuhan:
//    - Unity 2022.3 LTS atau lebih baru
//    - XR Plugin Management + OpenXR (atau Oculus/Mock HMD)
//    - TextMeshPro (Window > TextMeshPro > Import TMP Essential Resources)
//
//  Cara pakai:
//    1. Buat GameObject kosong bernama "IOMonitor", pasang script ini.
//    2. Isi field statusText dengan komponen TextMeshProUGUI pada Canvas
//       ber-Render Mode = World Space.
//    3. Isi feedbackObject dengan sebuah Cube, dan beepSource dengan
//       AudioSource (Spatial Blend = 1 agar benar-benar audio 3D).
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using TMPro;

public class XRInputOutputMonitor : MonoBehaviour
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

    // Daftar perangkat XR yang aktif
    private readonly List<InputDevice> _devices = new List<InputDevice>();
    private InputDevice _head, _left, _right;

    private readonly StringBuilder _sb = new StringBuilder(512);
    private Renderer _feedbackRenderer;
    private bool _prevTriggerL, _prevTriggerR;
    private string _lastEvent = "-";

    void Start()
    {
        if (feedbackObject != null)
            _feedbackRenderer = feedbackObject.GetComponent<Renderer>();

        RefreshDevices();

        // Perangkat bisa menyala/mati kapan saja -> selalu daftar ulang.
        InputDevices.deviceConnected += _ => RefreshDevices();
        InputDevices.deviceDisconnected += _ => RefreshDevices();
    }

    void RefreshDevices()
    {
        _devices.Clear();
        InputDevices.GetDevices(_devices);

        _head = GetDevice(InputDeviceCharacteristics.HeadMounted);
        _left = GetDevice(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left);
        _right = GetDevice(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right);
    }

    InputDevice GetDevice(InputDeviceCharacteristics ch)
    {
        var list = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(ch, list);
        return list.Count > 0 ? list[0] : default;
    }

    void Update()
    {
        _sb.Clear();
        _sb.AppendLine("== INPUT MONITOR (Pertemuan 2) ==");

        // ------------------------------------------------------------------
        // 1) INPUT: pose kepala. Inilah sumber 6DOF pada sistem VR.
        // ------------------------------------------------------------------
        if (_head.isValid)
        {
            _head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 hp);
            _head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion hr);
            Vector3 euler = hr.eulerAngles;

            _sb.AppendFormat("HEAD  pos : {0:F2} / {1:F2} / {2:F2}\n", hp.x, hp.y, hp.z);
            _sb.AppendFormat("HEAD  rot : yaw {0:F1}  pitch {1:F1}  roll {2:F1}\n",
                             euler.y, euler.x, euler.z);
        }
        else
        {
            _sb.AppendLine("HEAD      : belum terdeteksi (jalankan XR Device Simulator)");
        }

        // ------------------------------------------------------------------
        // 2) INPUT: controller kiri & kanan (pose + nilai tombol analog)
        // ------------------------------------------------------------------
        ReadController(_left, "LEFT ", ref _prevTriggerL);
        ReadController(_right, "RIGHT", ref _prevTriggerR);

        // ------------------------------------------------------------------
        // 3) INPUT: percepatan perangkat (IMU) - relevan untuk AR mobile
        // ------------------------------------------------------------------
        if (_head.isValid &&
            _head.TryGetFeatureValue(CommonUsages.deviceAcceleration, out Vector3 acc))
        {
            _sb.AppendFormat("IMU   acc : {0:F2} / {1:F2} / {2:F2} m/s2\n", acc.x, acc.y, acc.z);
        }

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

    void ReadController(InputDevice device, string label, ref bool prevPressed)
    {
        if (!device.isValid)
        {
            _sb.AppendFormat("{0}     : tidak terhubung\n", label);
            return;
        }

        device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos);
        device.TryGetFeatureValue(CommonUsages.trigger, out float trigger);   // analog 0..1
        device.TryGetFeatureValue(CommonUsages.grip, out float grip);
        device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick);
        device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed);

        _sb.AppendFormat("{0} pos : {1:F2} / {2:F2} / {3:F2}\n", label, pos.x, pos.y, pos.z);
        _sb.AppendFormat("{0} in  : trig {1:F2}  grip {2:F2}  stick {3:F2},{4:F2}\n",
                         label, trigger, grip, stick.x, stick.y);

        // Deteksi transisi tidak-ditekan -> ditekan (edge detection)
        if (pressed && !prevPressed) FireOutputs(device, label.Trim());
        prevPressed = pressed;
    }

    /// <summary>
    /// Satu aksi INPUT dibalas dengan tiga kanal OUTPUT sekaligus:
    /// visual, audio spasial, dan haptik.
    /// </summary>
    void FireOutputs(InputDevice device, string source)
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

        // (c) OUTPUT HAPTIK
        string hapticInfo = "haptik tidak didukung";
        if (device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
        {
            device.SendHapticImpulse(0u, hapticAmplitude, hapticDuration);
            hapticInfo = $"impulse {hapticAmplitude:F1} / {hapticDuration:F2}s";
        }

        _lastEvent = $"select dari {source} -> {hapticInfo}";
    }
}

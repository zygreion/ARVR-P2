using System.Collections.Generic;
using System.Text;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Management;

using TMPro;

// Alias supaya tidak bentrok dengan
// UnityEngine.InputSystem.InputDevice
using XRInputDevice = UnityEngine.XR.InputDevice;
using UnityEngine.XR;

public class XRInputOutputMonitor2 : MonoBehaviour
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

    [Header("Left Controller Input Actions")]
    public InputActionReference leftSelectAction;
    public InputActionReference leftActivateAction;
    public InputActionReference leftMoveAction;

    [Header("Right Controller Input Actions")]
    public InputActionReference rightSelectAction;
    public InputActionReference rightActivateAction;
    public InputActionReference rightMoveAction;

    // ============================================================
    // XR DEVICES
    // ============================================================

    private XRInputDevice _head;
    private XRInputDevice _leftController;
    private XRInputDevice _rightController;

    private readonly List<XRInputDevice> _devices =
        new List<XRInputDevice>();

    // ============================================================
    // INTERNAL
    // ============================================================

    private readonly StringBuilder _sb =
        new StringBuilder(1024);

    private Renderer _feedbackRenderer;

    private bool _previousLeftSelect;
    private bool _previousRightSelect;

    private string _lastEvent = "-";

    // ============================================================
    // START
    // ============================================================

    private void Start()
    {
        if (feedbackObject != null)
        {
            _feedbackRenderer =
                feedbackObject.GetComponent<Renderer>();
        }

        RefreshXRDevices();

        InputDevices.deviceConnected +=
            OnDeviceConnected;

        InputDevices.deviceDisconnected +=
            OnDeviceDisconnected;

        EnableInputActions();

        PrintXRStatus();
    }

    // ============================================================
    // CLEANUP
    // ============================================================

    private void OnDestroy()
    {
        InputDevices.deviceConnected -=
            OnDeviceConnected;

        InputDevices.deviceDisconnected -=
                    OnDeviceDisconnected;

        DisableInputActions();
    }

    // ============================================================
    // INPUT ACTIONS
    // ============================================================

    private void EnableInputActions()
    {
        EnableAction(leftSelectAction);
        EnableAction(leftActivateAction);
        EnableAction(leftMoveAction);

        EnableAction(rightSelectAction);
        EnableAction(rightActivateAction);
        EnableAction(rightMoveAction);
    }

    private void DisableInputActions()
    {
        DisableAction(leftSelectAction);
        DisableAction(leftActivateAction);
        DisableAction(leftMoveAction);

        DisableAction(rightSelectAction);
        DisableAction(rightActivateAction);
        DisableAction(rightMoveAction);
    }

    private void EnableAction(InputActionReference actionReference)
    {
        if (actionReference != null &&
            actionReference.action != null)
        {
            actionReference.action.Enable();
        }
    }

    private void DisableAction(InputActionReference actionReference)
    {
        if (actionReference != null &&
            actionReference.action != null)
        {
            actionReference.action.Disable();
        }
    }

    // ============================================================
    // XR DEVICES
    // ============================================================

    private void OnDeviceConnected(XRInputDevice device)
    {
        RefreshXRDevices();
    }

    private void OnDeviceDisconnected(XRInputDevice device)
    {
        RefreshXRDevices();
    }

    private void RefreshXRDevices()
    {
        _devices.Clear();

        InputDevices.GetDevices(_devices);

        _head = GetDevice(
            InputDeviceCharacteristics.HeadMounted
        );

        _leftController = GetDevice(
            InputDeviceCharacteristics.Controller |
            InputDeviceCharacteristics.Left
        );

        _rightController = GetDevice(
            InputDeviceCharacteristics.Controller |
            InputDeviceCharacteristics.Right
        );
    }

    private XRInputDevice GetDevice(
        InputDeviceCharacteristics characteristics)
    {
        List<XRInputDevice> devices =
            new List<XRInputDevice>();

        InputDevices
            .GetDevicesWithCharacteristics(
                characteristics,
                devices
            );

        if (devices.Count > 0)
        {
            return devices[0];
        }

        return default;
    }

    // ============================================================
    // XR STATUS
    // ============================================================

    private void PrintXRStatus()
    {
        XRManagerSettings manager =
            XRGeneralSettings.Instance != null
                ? XRGeneralSettings.Instance.Manager
                : null;

        if (manager != null &&
            manager.activeLoader != null)
        {
            Debug.Log(
                "XR aktif: " +
                manager.activeLoader.name
            );
        }
        else
        {
            Debug.Log("XR loader tidak aktif.");
        }

        Debug.Log(
            "Input controller menggunakan Unity Input System."
        );
    }

    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        _sb.Clear();

        _sb.AppendLine(
            "== INPUT MONITOR - XR DEVICE SIMULATOR =="
        );

        _sb.AppendLine();

        ReadHead();
        ReadLeftController();
        ReadRightController();

        float fps =
            1f / Mathf.Max(Time.deltaTime, 0.0001f);

        _sb.AppendLine();

        _sb.AppendFormat(
            "OUTPUT : {0:F0} FPS\n",
            fps
        );

        _sb.AppendFormat(
            "EVENT  : {0}",
            _lastEvent
        );

        if (statusText != null)
        {
            statusText.text = _sb.ToString();
        }

        if (feedbackObject != null)
        {
            feedbackObject.localScale =
                Vector3.Lerp(
                    feedbackObject.localScale,
                    Vector3.one,
                    Time.deltaTime * 6f
                );

            feedbackObject.Rotate(
                Vector3.up,
                30f * Time.deltaTime,
                Space.Self
            );
        }
    }

    // ============================================================
    // HEAD
    // ============================================================

    private void ReadHead()
    {
        _sb.AppendLine("-- HEAD / HMD --");

        if (!_head.isValid)
        {
            _sb.AppendLine(
                "HEAD : InputDevice tidak terdeteksi"
            );

            _sb.AppendLine();
            return;
        }

        bool hasPosition =
            _head.TryGetFeatureValue(
                UnityEngine.XR.CommonUsages.devicePosition,
                out Vector3 position
            );

        bool hasRotation =
            _head.TryGetFeatureValue(
                UnityEngine.XR.CommonUsages.deviceRotation,
                out Quaternion rotation
            );

        if (hasPosition)
        {
            _sb.AppendFormat(
                "HEAD pos : {0:F2} / {1:F2} / {2:F2}\n",
                position.x,
                position.y,
                position.z
            );
        }
        else
        {
            position = headPosition.action.ReadValue<Vector3>();
            _sb.AppendFormat(
                "HEAD pos : {0:F2} / {1:F2} / {2:F2}\n",
                position.x,
                position.y,
                position.z
            );
        }

        if (hasRotation)
        {
            Vector3 euler =
                rotation.eulerAngles;

            _sb.AppendFormat(
                "HEAD rot : yaw {0:F1}  pitch {1:F1}  roll {2:F1}\n",
                euler.y,
                euler.x,
                euler.z
            );
        }
        else
        {
            Vector3 euler =
                headRotation.action.ReadValue<Quaternion>().eulerAngles;

            _sb.AppendFormat(
                "HEAD rot : yaw {0:F1}  pitch {1:F1}  roll {2:F1}\n",
                euler.y,
                euler.x,
                euler.z
            );
        }

        if (_head.TryGetFeatureValue(
            UnityEngine.XR.CommonUsages.deviceAcceleration,
            out Vector3 acceleration))
        {
            _sb.AppendFormat(
                "IMU acc  : {0:F2} / {1:F2} / {2:F2} m/s2\n",
                acceleration.x,
                acceleration.y,
                acceleration.z
            );
        }

        // Debug.Log("Head position: " + headPosition.action.ReadValue<Vector3>());
        // Debug.Log("Head rotation: " + headRotation.action.ReadValue<Quaternion>());

        _sb.AppendLine();
    }

    // ============================================================
    // LEFT CONTROLLER
    // ============================================================

    private void ReadLeftController()
    {
        _sb.AppendLine("-- LEFT CONTROLLER --");

        Vector3 position;

        bool hasPosition =
            TryGetControllerPosition(
                _leftController,
                out position
            );

        if (hasPosition)
        {
            _sb.AppendFormat(
                "LEFT pos : {0:F2} / {1:F2} / {2:F2}\n",
                position.x,
                position.y,
                position.z
            );
        }
        else
        {
            _sb.AppendLine(
                "LEFT pos : tidak tersedia"
            );
        }

        float trigger =
            ReadFloat(leftActivateAction);

        float grip =
            ReadFloat(leftSelectAction);

        Vector2 stick =
            ReadVector2(leftMoveAction);

        bool select =
            ReadButton(leftSelectAction);

        _sb.AppendFormat(
            "LEFT in  : trig {0:F2}  grip {1:F2}  stick {2:F2},{3:F2}\n",
            trigger,
            grip,
            stick.x,
            stick.y
        );

        _sb.AppendFormat(
            "LEFT sel : {0}\n",
            select ? "PRESSED" : "-"
        );

        if (select && !_previousLeftSelect)
        {
            FireOutputs(
                _leftController,
                "LEFT"
            );
        }

        _previousLeftSelect = select;

        _sb.AppendLine();
    }

    // ============================================================
    // RIGHT CONTROLLER
    // ============================================================

    private void ReadRightController()
    {
        _sb.AppendLine("-- RIGHT CONTROLLER --");

        Vector3 position;

        bool hasPosition =
            TryGetControllerPosition(
                _rightController,
                out position
            );

        if (hasPosition)
        {
            _sb.AppendFormat(
                "RIGHT pos : {0:F2} / {1:F2} / {2:F2}\n",
                position.x,
                position.y,
                position.z
            );
        }
        else
        {
            _sb.AppendLine(
                "RIGHT pos : tidak tersedia"
            );
        }

        float trigger =
            ReadFloat(rightSelectAction);

        float grip =
            ReadFloat(rightActivateAction);

        Vector2 stick =
            ReadVector2(rightMoveAction);

        bool select =
            ReadButton(rightSelectAction);

        _sb.AppendFormat(
            "RIGHT in  : trig {0:F2}  grip {1:F2}  stick {2:F2},{3:F2}\n",
            trigger,
            grip,
            stick.x,
            stick.y
        );

        _sb.AppendFormat(
            "RIGHT sel : {0}\n",
            select ? "PRESSED" : "-"
        );

        if (select && !_previousRightSelect)
        {
            FireOutputs(
                _rightController,
                "RIGHT"
            );
        }

        _previousRightSelect = select;

        _sb.AppendLine();
    }

    // ============================================================
    // INPUT HELPERS
    // ============================================================

    private float ReadFloat(
        InputActionReference actionReference)
    {
        if (actionReference == null ||
            actionReference.action == null)
        {
            return 0f;
        }

        return actionReference.action.ReadValue<float>();
    }

    private Vector2 ReadVector2(
        InputActionReference actionReference)
    {
        if (actionReference == null ||
            actionReference.action == null)
        {
            return Vector2.zero;
        }

        return actionReference.action.ReadValue<Vector2>();
    }

    private bool ReadButton(
        InputActionReference actionReference)
    {
        if (actionReference == null ||
            actionReference.action == null)
        {
            return false;
        }

        return actionReference.action.IsPressed();
    }

    // ============================================================
    // CONTROLLER POSITION
    // ============================================================

    private bool TryGetControllerPosition(
        XRInputDevice device,
        out Vector3 position)
    {
        position = Vector3.zero;

        if (!device.isValid)
        {
            return false;
        }

        return device.TryGetFeatureValue(
            UnityEngine.XR.CommonUsages.devicePosition,
            out position
        );
    }

    // ============================================================
    // OUTPUT
    // ============================================================

    private void FireOutputs(
        XRInputDevice device,
        string source)
    {
        // --------------------------------------------------------
        // VISUAL
        // --------------------------------------------------------

        if (_feedbackRenderer != null)
        {
            _feedbackRenderer.material.color =
                Random.ColorHSV(
                    0f,
                    1f,
                    0.5f,
                    0.9f,
                    0.8f,
                    1f
                );
        }

        if (feedbackObject != null)
        {
            feedbackObject.localScale =
                Vector3.one * 1.4f;
        }

        // --------------------------------------------------------
        // AUDIO
        // --------------------------------------------------------

        if (beepSource != null)
        {
            beepSource.pitch =
                Random.Range(0.8f, 1.3f);

            beepSource.Play();
        }

        // --------------------------------------------------------
        // HAPTIC
        // --------------------------------------------------------

        string hapticInfo =
            "simulator / haptic tidak tersedia";

        if (device.isValid)
        {
            if (device.TryGetHapticCapabilities(
                out HapticCapabilities capabilities))
            {
                if (capabilities.supportsImpulse)
                {
                    device.SendHapticImpulse(
                        0u,
                        hapticAmplitude,
                        hapticDuration
                    );

                    hapticInfo =
                        $"impulse {hapticAmplitude:F1} / " +
                        $"{hapticDuration:F2}s";
                }
            }
        }

        _lastEvent =
            $"select dari {source} -> {hapticInfo}";
    }
}

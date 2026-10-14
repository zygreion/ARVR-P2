// =============================================================================
//  HapticFeedbackLab.cs
//  Mata Kuliah : Realitas Virtual dan Realitas Augmentasi (SE.1) - INF124312
//  Pertemuan 4 : Force Feedback Simulation dan Haptic Device  |  Pengayaan Unity
//  Prodi Informatika - Fakultas Ilmu Komputer - UPN Veteran Jakarta
//
//  Script ini memisahkan dua lapis yang dibahas di kelas:
//
//    LAPIS SIMULASI  -> HitungGaya()   : model pegas F = k x d (perangkat lunak)
//    LAPIS PERANGKAT -> KirimHaptik()  : XR controller / gamepad / ponsel (perangkat keras)
//
//  Pemisahan ini disengaja agar mahasiswa melihat bahwa satu simulasi yang sama
//  dapat dikirim ke perangkat yang berbeda-beda, dengan sensasi yang berbeda pula.
//
//  Kebutuhan:
//    - Unity 2022.3 LTS atau lebih baru
//    - Input System (untuk getaran gamepad). Tanpa itu, jalur gamepad dinonaktifkan
//      dengan menghapus definisi PAKAI_INPUT_SYSTEM di bawah.
//
//  Cara pakai:
//    1. Buat Cube bernama "Probe" (alat maya yang digerakkan pengguna).
//    2. Buat beberapa objek material uji, beri tag dan komponen MaterialHaptik.
//    3. Pasang script ini pada Probe, lalu tekan Play.
// =============================================================================

#define PAKAI_INPUT_SYSTEM

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

#if PAKAI_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Input System dan XR sama-sama memiliki kelas bernama InputDevice, sehingga
// nama tersebut menjadi ambigu bila keduanya diimpor. Alias di bawah ini
// menegaskan bahwa yang dimaksud adalah InputDevice milik XR.
using XRInputDevice = UnityEngine.XR.InputDevice;

public class HapticFeedbackLab : MonoBehaviour
{
    public enum JalurPerangkat { Otomatis, ControllerXR, Gamepad, Ponsel, TanpaPerangkat }

    [Header("=== LAPIS SIMULASI (perangkat lunak) ===")]
    [Tooltip("Kekakuan bahan dalam newton per meter. Nilai besar = terasa keras.")]
    [Range(10f, 2000f)] public float kekakuan = 400f;

    [Tooltip("Redaman, melawan kecepatan penetrasi. Mencegah osilasi.")]
    [Range(0f, 50f)] public float redaman = 5f;

    [Tooltip("Gaya maksimum yang boleh dihasilkan, dalam newton.")]
    [Range(1f, 50f)] public float gayaMaksimum = 12f;

    [Header("=== LAPIS PERANGKAT (perangkat keras) ===")]
    public JalurPerangkat jalur = JalurPerangkat.Otomatis;

    [Tooltip("Amplitudo minimum agar getaran masih terasa (0..1).")]
    [Range(0f, 0.5f)] public float amplitudoMinimum = 0.05f;

    [Header("=== PSEUDO-HAPTICS ===")]
    [Tooltip("Memperlambat gerak probe sebanding dengan gaya. Menimbulkan sensasi berat tanpa perangkat.")]
    public bool pseudoHaptics = true;

    [Tooltip("Seberapa kuat perlambatan visual. 0 = tidak ada, 1 = probe nyaris berhenti saat gaya maksimum.")]
    [Range(0f, 1f)] public float kekuatanPseudo = 0.7f;

    [Header("Gerak probe")]
    public float kecepatanGerak = 1.2f;

    // --- keadaan internal ---
    private float _penetrasi, _penetrasiSebelumnya;
    private float _gaya;
    private float _amplitudo;
    private string _namaBahan = "-";
    private Collider _kontakAktif;
    private XRInputDevice _deviceXR;
    private Vector3 _posisiTarget;

    void Start()
    {
        _posisiTarget = transform.position;
        CariDeviceXR();
    }

    void CariDeviceXR()
    {
        var list = new List<XRInputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, list);
        if (list.Count > 0) _deviceXR = list[0];
    }

    void Update()
    {
        GerakkanProbe();

        // ---------- LAPIS SIMULASI ----------
        _gaya = HitungGaya();

        // Petakan gaya (newton) menjadi amplitudo perangkat (0..1)
        _amplitudo = Mathf.Clamp01(_gaya / gayaMaksimum);
        if (_amplitudo > 0f) _amplitudo = Mathf.Max(_amplitudo, amplitudoMinimum);

        // ---------- LAPIS PERANGKAT ----------
        KirimHaptik(_amplitudo);
    }

    /// <summary>
    /// Menggerakkan probe dengan keyboard. Bila pseudo-haptics aktif, gerakan
    /// diperlambat sebanding dengan gaya sehingga objek terasa berat.
    /// </summary>
    void GerakkanProbe()
    {
        Vector3 arah = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) arah += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) arah += Vector3.back;
        if (Input.GetKey(KeyCode.A)) arah += Vector3.left;
        if (Input.GetKey(KeyCode.D)) arah += Vector3.right;
        if (Input.GetKey(KeyCode.E)) arah += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) arah += Vector3.down;

        _posisiTarget += arah.normalized * kecepatanGerak * Time.deltaTime;

        if (pseudoHaptics)
        {
            // Inilah trik pseudo-haptics: posisi yang DITAMPILKAN tertinggal di
            // belakang posisi sebenarnya, sebanding dengan besar gaya.
            float hambatan = 1f - (Mathf.Clamp01(_gaya / gayaMaksimum) * kekuatanPseudo);
            transform.position = Vector3.Lerp(transform.position, _posisiTarget,
                                              Mathf.Max(hambatan, 0.02f));
        }
        else
        {
            transform.position = _posisiTarget;
        }
    }

    /// <summary>
    /// LAPIS SIMULASI — model pegas teredam.
    /// F = k x d + b x (dd/dt), dibatasi oleh gaya maksimum perangkat.
    /// </summary>
    float HitungGaya()
    {
        if (_kontakAktif == null) { _penetrasi = 0f; _penetrasiSebelumnya = 0f; return 0f; }

        // Kedalaman penetrasi: seberapa jauh probe menembus permukaan objek
        Vector3 titikTerdekat = _kontakAktif.ClosestPoint(transform.position);
        _penetrasi = Vector3.Distance(transform.position, titikTerdekat);

        // Bila ClosestPoint mengembalikan titik yang sama, probe berada di dalam objek
        if (_penetrasi < 0.0001f)
            _penetrasi = Vector3.Distance(transform.position, _kontakAktif.bounds.ClosestPoint(transform.position)) + 0.01f;

        float kecepatanPenetrasi = (_penetrasi - _penetrasiSebelumnya) / Mathf.Max(Time.deltaTime, 0.0001f);
        _penetrasiSebelumnya = _penetrasi;

        float f = kekakuan * _penetrasi + redaman * Mathf.Max(kecepatanPenetrasi, 0f);
        return Mathf.Clamp(f, 0f, gayaMaksimum);
    }

    /// <summary>
    /// LAPIS PERANGKAT — mengirim amplitudo ke perangkat keras yang tersedia.
    /// </summary>
    void KirimHaptik(float amplitudo)
    {
        JalurPerangkat j = jalur;

        if (j == JalurPerangkat.Otomatis)
        {
            if (_deviceXR.isValid) j = JalurPerangkat.ControllerXR;
#if PAKAI_INPUT_SYSTEM
            else if (Gamepad.current != null) j = JalurPerangkat.Gamepad;
#endif
            else if (Application.isMobilePlatform) j = JalurPerangkat.Ponsel;
            else j = JalurPerangkat.TanpaPerangkat;
        }

        switch (j)
        {
            case JalurPerangkat.ControllerXR:
                if (_deviceXR.isValid && amplitudo > 0f)
                {
                    // Impulse pendek dikirim berulang setiap frame, membentuk getaran kontinu
                    _deviceXR.SendHapticImpulse(0u, amplitudo, Time.deltaTime * 2f);
                }
                break;

#if PAKAI_INPUT_SYSTEM
            case JalurPerangkat.Gamepad:
                if (Gamepad.current != null)
                {
                    // Motor kiri berfrekuensi rendah, motor kanan berfrekuensi tinggi
                    Gamepad.current.SetMotorSpeeds(amplitudo * 0.6f, amplitudo);
                }
                break;
#endif

            case JalurPerangkat.Ponsel:
                // Handheld.Vibrate tidak mengenal amplitudo — hanya bisa nyala atau mati.
                // Keterbatasan ini sengaja diperlihatkan sebagai bahan diskusi.
                if (amplitudo > 0.5f && Time.frameCount % 20 == 0) Handheld.Vibrate();
                break;

            case JalurPerangkat.TanpaPerangkat:
                // Tidak ada aktuator. Hanya pseudo-haptics dan tampilan angka yang bekerja.
                break;
        }
    }

    void OnDisable()
    {
#if PAKAI_INPUT_SYSTEM
        if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f);
#endif
    }

    // ---------- Deteksi kontak ----------
    void OnTriggerEnter(Collider other)
    {
        _kontakAktif = other;
        var bahan = other.GetComponent<MaterialHaptik>();
        if (bahan != null)
        {
            kekakuan = bahan.kekakuan;
            redaman = bahan.redaman;
            _namaBahan = bahan.namaBahan;
        }
        else _namaBahan = other.name;
    }

    void OnTriggerExit(Collider other)
    {
        if (_kontakAktif == other)
        {
            _kontakAktif = null;
            _namaBahan = "-";
        }
    }

    // ---------- Tampilan data di layar ----------
    void OnGUI()
    {
        GUI.color = Color.white;
        var gaya = new GUIStyle(GUI.skin.label) { fontSize = 16 };

        GUI.Label(new Rect(14, 10, 900, 24), "WASD/QE = gerakkan probe   |   Tekan objek untuk merasakan gaya", gaya);
        GUI.Label(new Rect(14, 38, 900, 24), $"Bahan        : {_namaBahan}   (k = {kekakuan:F0} N/m)", gaya);
        GUI.Label(new Rect(14, 62, 900, 24), $"Penetrasi    : {_penetrasi * 100f:F2} cm", gaya);
        GUI.Label(new Rect(14, 86, 900, 24), $"Gaya simulasi: {_gaya:F2} N   (maks {gayaMaksimum:F0} N)", gaya);
        GUI.Label(new Rect(14, 110, 900, 24), $"Amplitudo    : {_amplitudo:F2}   |   Jalur: {jalur}", gaya);
        GUI.Label(new Rect(14, 134, 900, 24), $"Pseudo-haptics: {(pseudoHaptics ? "aktif" : "nonaktif")}", gaya);

        // Bar gaya
        GUI.Box(new Rect(14, 164, 300, 18), "");
        GUI.color = Color.Lerp(Color.green, Color.red, _amplitudo);
        GUI.Box(new Rect(14, 164, 300 * _amplitudo, 18), "");
    }
}

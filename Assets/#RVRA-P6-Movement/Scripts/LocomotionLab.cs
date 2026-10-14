// =============================================================================
//  LocomotionLab.cs
//  Mata Kuliah : Realitas Virtual dan Realitas Augmentasi (SE.1) - INF124312
//  Pertemuan 6 : Poses dan Movements  |  Pengayaan Unity Bagian B
//  Prodi Informatika - Fakultas Ilmu Komputer - UPN Veteran Jakarta
//
//  Membandingkan teknik locomotion beserta pengaturan kenyamanannya:
//
//    Mode gerak   : Smooth (meluncur)  vs  Teleport (berpindah seketika)
//    Mode putar   : Smooth turn        vs  Snap turn
//    Kenyamanan   : vignette, akselerasi, kecepatan
//
//  Pasang pada GameObject "Rig" yang menjadi induk dari Main Camera.
//
//  Kontrol:
//    W A S D        gerak maju/mundur/samping
//    Mouse kanan    menoleh bebas (meniru gerakan kepala, bukan locomotion)
//    Q / E          memutar badan (smooth atau snap, sesuai mode)
//    Tahan Spasi    membidik tujuan teleport; lepas untuk berpindah
//    1 / 2          ganti mode gerak  (Smooth / Teleport)
//    3 / 4          ganti mode putar  (Smooth / Snap)
//    V              nyalakan atau matikan vignette
// =============================================================================

using UnityEngine;

public class LocomotionLab : MonoBehaviour
{
    public enum ModeGerak { Smooth, Teleport }
    public enum ModePutar { SmoothTurn, SnapTurn }

    [Header("Mode")]
    public ModeGerak modeGerak = ModeGerak.Smooth;
    public ModePutar modePutar = ModePutar.SmoothTurn;

    [Header("Kamera")]
    [Tooltip("Kamera anak dari rig ini. Dipakai untuk arah gerak dan arah bidik.")]
    public Transform kamera;

    [Header("Smooth locomotion")]
    [Range(0.5f, 6f)] public float kecepatan = 2.0f;
    [Tooltip("Waktu untuk mencapai kecepatan penuh, dalam detik. Nol berarti seketika.")]
    [Range(0f, 2f)] public float waktuAkselerasi = 0.25f;

    [Header("Rotasi")]
    [Range(20f, 180f)] public float kecepatanSmoothTurn = 60f;
    [Range(15f, 90f)] public float sudutSnapTurn = 45f;
    [Range(0.05f, 1f)] public float jedaSnapTurn = 0.25f;

    [Header("Teleport")]
    [Range(1f, 40f)] public float jarakMaksimumTeleport = 25f;
    public LayerMask lapisanLantai = ~0;

    [Header("Kenyamanan")]
    public bool vignetteAktif = true;
    [Tooltip("Seberapa besar tepi layar digelapkan saat bergerak, 0 sampai 1.")]
    [Range(0f, 0.45f)] public float kekuatanVignette = 0.22f;

    // --- keadaan internal ---
    Vector3 _kecepatanSekarang;
    float _waktuSnapBerikut;
    bool _membidik;
    Vector3 _titikTeleport;
    bool _titikValid;
    float _vignetteSekarang;
    float _jarakTempuh;
    int _jumlahTeleport;
    float _yaw, _pitch;
    Texture2D _texHitam;
    Transform _penanda;          // penanda tujuan teleport, terlihat di Game view
    Renderer _penandaRenderer;

    void Start()
    {
        if (kamera == null && Camera.main != null) kamera = Camera.main.transform;
        _texHitam = new Texture2D(1, 1);
        _texHitam.SetPixel(0, 0, Color.black);
        _texHitam.Apply();
        _yaw = transform.eulerAngles.y;
        BuatPenanda();
    }

    /// <summary>Membuat cakram penanda tujuan teleport saat runtime.</summary>
    void BuatPenanda()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "PenandaTeleport";
        go.transform.localScale = new Vector3(0.7f, 0.02f, 0.7f);
        Destroy(go.GetComponent<Collider>());   // jangan sampai menghalangi raycast
        _penandaRenderer = go.GetComponent<Renderer>();
        _penandaRenderer.material.color = new Color(0f, 0.82f, 0.70f);
        _penanda = go.transform;
        _penanda.gameObject.SetActive(false);
    }

    void Update()
    {
        GantiMode();
        Menoleh();

        if (modeGerak == ModeGerak.Smooth)
        {
            GerakSmooth();
            if (_penanda != null && _penanda.gameObject.activeSelf) _penanda.gameObject.SetActive(false);
        }
        else GerakTeleport();

        Memutar();
    }

    void GantiMode()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) modeGerak = ModeGerak.Smooth;
        if (Input.GetKeyDown(KeyCode.Alpha2)) modeGerak = ModeGerak.Teleport;
        if (Input.GetKeyDown(KeyCode.Alpha3)) modePutar = ModePutar.SmoothTurn;
        if (Input.GetKeyDown(KeyCode.Alpha4)) modePutar = ModePutar.SnapTurn;
        if (Input.GetKeyDown(KeyCode.V)) vignetteAktif = !vignetteAktif;
    }

    /// <summary>Menoleh dengan mouse — meniru gerakan kepala, bukan locomotion.</summary>
    void Menoleh()
    {
        if (kamera == null || !Input.GetMouseButton(1)) return;
        _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * 2.5f, -80f, 80f);
        kamera.localRotation = Quaternion.Euler(_pitch, kamera.localEulerAngles.y, 0f);
    }

    void GerakSmooth()
    {
        Vector3 arah = Vector3.zero;
        Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 r = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

        if (Input.GetKey(KeyCode.W)) arah += f;
        if (Input.GetKey(KeyCode.S)) arah -= f;
        if (Input.GetKey(KeyCode.D)) arah += r;
        if (Input.GetKey(KeyCode.A)) arah -= r;

        Vector3 target = arah.normalized * kecepatan;

        // Akselerasi: inilah yang paling memicu mual bila diubah terlalu cepat
        _kecepatanSekarang = waktuAkselerasi <= 0.01f
            ? target
            : Vector3.MoveTowards(_kecepatanSekarang, target,
                                  (kecepatan / waktuAkselerasi) * Time.deltaTime);

        Vector3 langkah = _kecepatanSekarang * Time.deltaTime;
        transform.position += langkah;
        _jarakTempuh += langkah.magnitude;
    }

    void GerakTeleport()
    {
        _kecepatanSekarang = Vector3.zero;
        _membidik = Input.GetKey(KeyCode.Space);

        if (_membidik && kamera != null)
        {
            Ray ray = new Ray(kamera.position, kamera.forward);
            _titikValid = Physics.Raycast(ray, out RaycastHit hit, jarakMaksimumTeleport, lapisanLantai)
                          && Vector3.Angle(hit.normal, Vector3.up) < 35f;
            if (_titikValid) _titikTeleport = hit.point;
        }

        // Tampilkan penanda tujuan di Game view
        if (_penanda != null)
        {
            bool tampil = _membidik && _titikValid;
            _penanda.gameObject.SetActive(tampil);
            if (tampil) _penanda.position = _titikTeleport + Vector3.up * 0.02f;
        }

        if (Input.GetKeyUp(KeyCode.Space) && _titikValid)
        {
            Debug.Log("MASUK");
            float tinggiRig = transform.position.y;
            transform.position = new Vector3(_titikTeleport.x, tinggiRig, _titikTeleport.z);
            _jumlahTeleport++;
            _titikValid = false;
            if (_penanda != null) _penanda.gameObject.SetActive(false);
        }
    }

    void Memutar()
    {
        float input = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
        if (Mathf.Approximately(input, 0f)) return;

        if (modePutar == ModePutar.SmoothTurn)
        {
            transform.Rotate(Vector3.up, input * kecepatanSmoothTurn * Time.deltaTime, Space.World);
        }
        else
        {
            if (Time.time < _waktuSnapBerikut) return;
            _waktuSnapBerikut = Time.time + jedaSnapTurn;
            transform.Rotate(Vector3.up, input * sudutSnapTurn, Space.World);
        }
    }

    void OnDrawGizmos()
    {
        if (modeGerak == ModeGerak.Teleport && _membidik && _titikValid)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_titikTeleport, 0.3f);
            if (kamera != null) Gizmos.DrawLine(kamera.position, _titikTeleport);
        }
    }

    void OnGUI()
    {
        // ---- Vignette: tepi layar digelapkan sebanding kecepatan gerak ----
        float target = 0f;
        if (vignetteAktif)
        {
            float laju = _kecepatanSekarang.magnitude / Mathf.Max(kecepatan, 0.01f);
            bool sedangMemutar = Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E);
            target = Mathf.Clamp01(laju + (sedangMemutar && modePutar == ModePutar.SmoothTurn ? 0.6f : 0f))
                     * kekuatanVignette;
        }
        _vignetteSekarang = Mathf.Lerp(_vignetteSekarang, target, Time.deltaTime * 6f);

        if (_vignetteSekarang > 0.001f)
        {
            float tebalX = Screen.width * _vignetteSekarang;
            float tebalY = Screen.height * _vignetteSekarang;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, tebalX, Screen.height), _texHitam);
            GUI.DrawTexture(new Rect(Screen.width - tebalX, 0, tebalX, Screen.height), _texHitam);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, tebalY), _texHitam);
            GUI.DrawTexture(new Rect(0, Screen.height - tebalY, Screen.width, tebalY), _texHitam);
            GUI.color = Color.white;
        }

        // ---- Panel data ----
        var st = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true };
        string bidik = modeGerak == ModeGerak.Teleport
            ? (_membidik ? (_titikValid ? "<color=#00d1b2>tujuan valid</color>" : "<color=#ff6b6b>tujuan tidak valid</color>") : "tahan Spasi untuk membidik")
            : "-";

        string teks =
            $"<b>LOCOMOTION LAB</b>\n" +
            $"Mode gerak : <color=#f0a821>{modeGerak}</color>   (1 = Smooth, 2 = Teleport)\n" +
            $"Mode putar : <color=#f0a821>{modePutar}</color>   (3 = Smooth, 4 = Snap)\n" +
            $"Vignette   : {(vignetteAktif ? "aktif" : "mati")}   (V)   kekuatan {_vignetteSekarang:F2}\n" +
            $"Kecepatan  : {_kecepatanSekarang.magnitude:F2} m/s   |   akselerasi {waktuAkselerasi:F2} s\n" +
            $"Teleport   : {bidik}\n" +
            $"Jarak tempuh {_jarakTempuh:F1} m   |   jumlah teleport {_jumlahTeleport}";

        GUI.Box(new Rect(10, 10, 540, 160), teks, st);

        var kecil = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        GUI.Label(new Rect(10, 178, 760, 20),
            "WASD gerak  |  Q/E putar badan  |  klik-tahan mouse KANAN untuk MENUNDUK  |  lalu tahan Spasi untuk membidik lantai", kecil);
    }
}

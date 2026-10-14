// =============================================================================
//  PenggerakSederhana.cs
//  Mata Kuliah : Realitas Virtual dan Realitas Augmentasi (SE.1) - INF124312
//  Pertemuan 5 : Viewer dan Object Tracking  |  Pengayaan Unity
//
//  Fungsi:
//    Menggerakkan pose "sebenarnya" — kepala atau benda nyata — agar tracker
//    punya sesuatu untuk dilacak. Tombol dapat diatur per objek sehingga
//    kepala dan benda dapat dikendalikan secara terpisah.
//
//  Preset yang disarankan:
//    Kepala : W A S D + Q E, menoleh dengan klik-tahan mouse kanan
//    Benda  : I J K L + U O, atau aktifkan Gerak Melingkar
// =============================================================================

using UnityEngine;

public class PenggerakSederhana : MonoBehaviour
{
    [Header("Tombol gerak")]
    public KeyCode maju = KeyCode.W;
    public KeyCode mundur = KeyCode.S;
    public KeyCode kiri = KeyCode.A;
    public KeyCode kanan = KeyCode.D;
    public KeyCode naik = KeyCode.E;
    public KeyCode turun = KeyCode.Q;

    [Header("Perilaku")]
    public float kecepatan = 1.5f;
    [Tooltip("Aktifkan untuk kepala: arah gerak mengikuti arah hadap.")]
    public bool relatifArahHadap = false;
    [Tooltip("Aktifkan untuk kepala: klik-tahan mouse kanan untuk menoleh.")]
    public bool menolehDenganMouse = false;
    public float sensitivitasMouse = 2.5f;

    [Header("Gerak otomatis (untuk mengamati latensi)")]
    [Tooltip("Benda bergerak melingkar sendiri, sehingga latensi terlihat jelas tanpa menekan tombol.")]
    public bool gerakMelingkar = false;
    public float jariJari = 0.6f;
    public float putaranPerDetik = 0.25f;
    [Tooltip("Tombol untuk menyalakan/mematikan gerak melingkar saat Play.")]
    public KeyCode toggleMelingkar = KeyCode.Space;

    private Vector3 _pusat;
    private float _sudut, _yaw, _pitch;

    void Start()
    {
        _pusat = transform.position;
        Vector3 e = transform.eulerAngles;
        _yaw = e.y; _pitch = e.x;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleMelingkar)) gerakMelingkar = !gerakMelingkar;

        if (gerakMelingkar)
        {
            _sudut += putaranPerDetik * 360f * Time.deltaTime;
            float rad = _sudut * Mathf.Deg2Rad;
            transform.position = _pusat + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * jariJari;
            return;
        }

        if (menolehDenganMouse && Input.GetMouseButton(1))
        {
            _yaw += Input.GetAxis("Mouse X") * sensitivitasMouse;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * sensitivitasMouse, -80f, 80f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        Vector3 f = relatifArahHadap ? Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 r = relatifArahHadap ? Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized : Vector3.right;

        Vector3 d = Vector3.zero;
        if (Input.GetKey(maju)) d += f;
        if (Input.GetKey(mundur)) d -= f;
        if (Input.GetKey(kanan)) d += r;
        if (Input.GetKey(kiri)) d -= r;
        if (Input.GetKey(naik)) d += Vector3.up;
        if (Input.GetKey(turun)) d -= Vector3.up;

        transform.position += d.normalized * kecepatan * Time.deltaTime;
        _pusat = transform.position;   // pusat lingkaran ikut posisi terakhir
    }
}

// =============================================================================
//  StereoRigController.cs
//  Mata Kuliah : Realitas Virtual dan Realitas Augmentasi (SE.1) - INF124312
//  Pertemuan 3 : Stereoscopic View  |  Modul 1 (Unity)
//
//  Fungsi: menggerakkan StereoRig saat Play berjalan, agar mahasiswa dapat
//  mendekat dan menjauh dari objek sambil mengamati perubahan disparitas.
//
//  Kontrol:
//    Klik-tahan tombol KANAN mouse  -> menoleh (mouse look)
//    W / A / S / D                  -> maju, kiri, mundur, kanan
//    Q / E                          -> turun / naik
//    Shift                          -> bergerak lebih cepat
//    R                              -> kembali ke posisi awal
//
//  Cara pakai: pasang script ini pada GameObject "StereoRig" yang sama,
//  berdampingan dengan komponen Stereo Camera Rig.
//
//  Catatan: memakai Input Manager lama. Bila Game view tidak merespons,
//  buka Project Settings > Player > Active Input Handling lalu pilih "Both".
// =============================================================================

using UnityEngine;

public class StereoRigController : MonoBehaviour
{
    [Header("Kecepatan")]
    [Tooltip("Kecepatan gerak dalam meter per detik.")]
    public float moveSpeed = 2.5f;

    [Tooltip("Pengali kecepatan saat tombol Shift ditahan.")]
    public float sprintMultiplier = 3f;

    [Tooltip("Sensitivitas mouse saat menoleh.")]
    public float lookSensitivity = 2.5f;

    [Header("Batas")]
    [Tooltip("Batas sudut menunduk dan mendongak, dalam derajat.")]
    public float maxPitch = 80f;

    [Tooltip("Kunci ketinggian mata agar tetap setinggi berdiri.")]
    public bool kunciKetinggian = true;

    private float _yaw, _pitch;
    private Vector3 _startPos;
    private Quaternion _startRot;

    void Start()
    {
        _startPos = transform.position;
        _startRot = transform.rotation;

        Vector3 e = transform.eulerAngles;
        _yaw = e.y;
        _pitch = e.x;
    }

    void Update()
    {
        // ---- Menoleh: hanya aktif saat tombol kanan mouse ditahan ----
        if (Input.GetMouseButton(1))
        {
            _yaw += Input.GetAxis("Mouse X") * lookSensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch, -maxPitch, maxPitch);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        // ---- Berpindah tempat ----
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);

        Vector3 dir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) dir += transform.forward;
        if (Input.GetKey(KeyCode.S)) dir -= transform.forward;
        if (Input.GetKey(KeyCode.D)) dir += transform.right;
        if (Input.GetKey(KeyCode.A)) dir -= transform.right;
        if (Input.GetKey(KeyCode.E)) dir += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) dir -= Vector3.up;

        if (kunciKetinggian && !Input.GetKey(KeyCode.E) && !Input.GetKey(KeyCode.Q))
            dir.y = 0f;   // gerak mendatar saja, seperti orang berjalan

        transform.position += dir.normalized * speed * Time.deltaTime;

        // ---- Kembali ke posisi awal ----
        if (Input.GetKeyDown(KeyCode.R))
        {
            transform.position = _startPos;
            transform.rotation = _startRot;
            Vector3 e = _startRot.eulerAngles;
            _yaw = e.y;
            _pitch = e.x;
        }
    }

    void OnGUI()
    {
        // Petunjuk singkat di layar, berguna saat demo di depan kelas
        GUI.color = Color.white;
        GUI.Label(new Rect(10, 10, 700, 22),
            "Klik-tahan MOUSE KANAN = menoleh   |   WASD = jalan   |   Q/E = turun-naik   |   Shift = cepat   |   R = reset");
    }
}

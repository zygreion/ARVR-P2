// =============================================================================
//  StereoCameraRig.cs
//  Mata Kuliah : Realitas Virtual dan Realitas Augmentasi (SE.1) - INF124312
//  Pertemuan 3 : Stereoscopic View  |  Modul 1 (Unity)
//  Prodi Informatika - Fakultas Ilmu Komputer - UPN Veteran Jakarta
//
//  Fungsi:
//    Membangun rig stereo dua kamera secara manual, sehingga mahasiswa dapat
//    MELIHAT kedua citra sekaligus dan mengubah parameternya secara langsung.
//
//    Mode OffAxis : frustum asimetris  -> cara yang BENAR
//    Mode ToeIn   : kamera diputar     -> cara yang KELIRU, untuk pembanding
//
//  Cara pakai:
//    1. Buat GameObject kosong bernama "StereoRig", pasang script ini.
//    2. Tekan tombol "Bangun Rig" pada Inspector, atau cukup tekan Play —
//       kedua kamera anak akan dibuat otomatis.
//    3. Ubah nilai ipdMeters dan convergenceDistance saat Play berjalan dan
//       amati perubahannya pada Game view yang terbagi dua.
//
//  Catatan: script ini sengaja TIDAK memakai XR SDK agar dapat dijalankan
//  pada komputer mana pun tanpa headset.
// =============================================================================

using UnityEngine;

[ExecuteAlways]
public class StereoCameraRig : MonoBehaviour
{
    public enum StereoMethod { OffAxis, ToeIn }

    [Header("Metode penyusunan kamera")]
    [Tooltip("OffAxis = frustum asimetris (benar). ToeIn = kamera diputar (keliru, untuk pembanding).")]
    public StereoMethod method = StereoMethod.OffAxis;

    [Header("Parameter stereo")]
    [Tooltip("Jarak antar pupil dalam meter. Rata-rata dewasa 0.063 m.")]
    [Range(0f, 0.25f)] public float ipdMeters = 0.063f;

    [Tooltip("Jarak bidang parallax nol dari pengamat, dalam meter.")]
    [Range(0.3f, 20f)] public float convergenceDistance = 3f;

    [Header("Parameter kamera")]
    [Range(30f, 110f)] public float fieldOfView = 60f;
    public float nearClip = 0.05f;
    public float farClip = 200f;

    [Header("Tampilan")]
    [Tooltip("Menukar citra kiri dan kanan untuk mendemonstrasikan efek pseudoscopic.")]
    public bool swapEyes = false;

    private Camera _left, _right;

    void OnEnable() { BuildRig(); }
    void Update() { UpdateRig(); }
    void OnValidate() { if (isActiveAndEnabled) UpdateRig(); }

    /// <summary>Membuat dua kamera anak bila belum ada.</summary>
    public void BuildRig()
    {
        _left = FindOrCreate("Camera_Left");
        _right = FindOrCreate("Camera_Right");
        UpdateRig();
    }

    Camera FindOrCreate(string childName)
    {
        Transform t = transform.Find(childName);
        if (t == null)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            t = go.transform;
        }
        var cam = t.GetComponent<Camera>();
        if (cam == null) cam = t.gameObject.AddComponent<Camera>();

        // Hanya satu AudioListener yang boleh aktif di scene
        var al = t.GetComponent<AudioListener>();
        if (al != null) DestroyImmediate(al);

        return cam;
    }

    void UpdateRig()
    {
        if (_left == null || _right == null) return;

        float half = ipdMeters * 0.5f;

        // Kedua kamera selalu berada di kiri dan kanan titik pusat rig
        _left.transform.localPosition = new Vector3(-half, 0f, 0f);
        _right.transform.localPosition = new Vector3(+half, 0f, 0f);

        // Layar terbagi dua: kiri dan kanan (side-by-side)
        _left.rect = new Rect(swapEyes ? 0.5f : 0.0f, 0f, 0.5f, 1f);
        _right.rect = new Rect(swapEyes ? 0.0f : 0.5f, 0f, 0.5f, 1f);

        foreach (var cam in new[] { _left, _right })
        {
            cam.nearClipPlane = nearClip;
            cam.farClipPlane = farClip;
            cam.fieldOfView = fieldOfView;
        }

        if (method == StereoMethod.ToeIn)
        {
            // ---------------------------------------------------------------
            // CARA KELIRU: kamera diputar hingga sumbunya bertemu di titik
            // konvergensi. Hasilnya muncul parallax vertikal di tepi citra.
            // ---------------------------------------------------------------
            _left.ResetProjectionMatrix();
            _right.ResetProjectionMatrix();

            Vector3 target = transform.position + transform.forward * convergenceDistance;
            _left.transform.LookAt(target, transform.up);
            _right.transform.LookAt(target, transform.up);
        }
        else
        {
            // ---------------------------------------------------------------
            // CARA BENAR: kamera tetap sejajar, frustum digeser secara
            // horizontal sehingga bidang proyeksi kedua mata berhimpit.
            // ---------------------------------------------------------------
            _left.transform.localRotation = Quaternion.identity;
            _right.transform.localRotation = Quaternion.identity;

            // Aspect tiap kamera = setengah lebar layar
            float aspect = (Screen.width * 0.5f) / Mathf.Max(Screen.height, 1);

            _left.projectionMatrix = OffAxisProjection(+half, aspect);   // mata kiri: frustum digeser ke kanan
            _right.projectionMatrix = OffAxisProjection(-half, aspect);  // mata kanan: frustum digeser ke kiri
        }
    }

    /// <summary>
    /// Membangun matriks proyeksi dengan frustum asimetris.
    /// offset bertanda positif menggeser frustum ke arah kanan.
    /// </summary>
    Matrix4x4 OffAxisProjection(float halfIpdSigned, float aspect)
    {
        float top = nearClip * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
        float bottom = -top;

        // Inilah rumus yang dibahas pada slide teori
        float shift = halfIpdSigned * (nearClip / convergenceDistance);

        float halfWidth = top * aspect;
        float left = -halfWidth + shift;
        float right = +halfWidth + shift;

        return Frustum(left, right, bottom, top, nearClip, farClip);
    }

    /// <summary>Matriks proyeksi perspektif umum (setara glFrustum).</summary>
    static Matrix4x4 Frustum(float l, float r, float b, float t, float n, float f)
    {
        Matrix4x4 m = new Matrix4x4();
        m[0, 0] = 2f * n / (r - l);
        m[0, 2] = (r + l) / (r - l);
        m[1, 1] = 2f * n / (t - b);
        m[1, 2] = (t + b) / (t - b);
        m[2, 2] = -(f + n) / (f - n);
        m[2, 3] = -(2f * f * n) / (f - n);
        m[3, 2] = -1f;
        return m;
    }

    /// <summary>Menggambar bidang konvergensi di Scene view sebagai alat bantu ajar.</summary>
    void OnDrawGizmos()
    {
        Vector3 center = transform.position + transform.forward * convergenceDistance;
        Gizmos.color = new Color(0f, 0.82f, 0.70f, 0.9f);
        Gizmos.DrawWireCube(center, new Vector3(4f, 2.5f, 0.001f));
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, center);
    }
}

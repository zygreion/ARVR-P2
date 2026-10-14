// =============================================================================
//  PoseGestureLab.cs
//  Mata Kuliah : Realitas Virtual dan Realitas Augmentasi (SE.1) - INF124312
//  Pertemuan 6 : Poses dan Movements  |  Pengayaan Unity Bagian A
//  Prodi Informatika - Fakultas Ilmu Komputer - UPN Veteran Jakarta
//
//  Script ini memperlihatkan tiga hal sekaligus:
//
//    1. POSE      -> membaca posisi dan orientasi, dalam Euler maupun quaternion
//    2. MOVEMENT  -> merekam rangkaian pose lalu memutarnya kembali
//    3. GESTUR    -> mengenali tiga gestur berbasis aturan, dengan ambang,
//                    histeresis, dan cooldown yang dapat disetel
//
//  Seluruh perhitungan gestur dilakukan pada KERANGKA KEPALA, bukan kerangka
//  dunia. Mahasiswa dapat mematikannya lewat centang untuk melihat akibatnya.
//
//  Cara pakai:
//    Pasang pada GameObject kosong bernama "GestureSystem", lalu isi field
//    kepala dan tangan dari Hierarchy.
//
//  Tombol:
//    F1 = mulai/berhenti merekam     F2 = putar ulang rekaman
//    F3 = hapus rekaman              F4 = reset penghitung salah deteksi
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

public class PoseGestureLab : MonoBehaviour
{
    [Header("Objek yang diamati")]
    public Transform kepala;
    public Transform tangan;

    [Header("Kerangka acuan")]
    [Tooltip("Dicentang = gestur diukur relatif terhadap kepala (benar). " +
             "Tidak dicentang = relatif terhadap dunia (keliru, untuk pembanding).")]
    public bool relatifTerhadapKepala = true;

    [Header("Ambang gestur")]
    [Tooltip("Kecepatan minimum agar gerakan dianggap gestur DORONG, dalam m/detik.")]
    [Range(0.1f, 5f)] public float ambangDorong = 1.2f;

    [Tooltip("Ketinggian tangan di atas kepala agar dianggap gestur ANGKAT, dalam meter.")]
    [Range(0f, 0.6f)] public float ambangAngkat = 0.15f;

    [Tooltip("Jumlah perubahan arah kiri-kanan agar dianggap gestur LAMBAI.")]
    [Range(2, 8)] public int ambangLambai = 3;

    [Header("Histeresis dan cooldown")]
    [Tooltip("Ambang keluar dibuat lebih rendah agar deteksi tidak berkedip. " +
             "0,7 berarti gestur baru dianggap selesai di bawah 70% ambang masuk.")]
    [Range(0.3f, 1f)] public float faktorHisteresis = 0.7f;

    [Tooltip("Jeda wajib setelah satu gestur terdeteksi, dalam detik.")]
    [Range(0f, 2f)] public float cooldownDetik = 0.6f;

    [Header("Perekaman movement")]
    [Tooltip("Laju perekaman pose, dalam sampel per detik.")]
    [Range(10f, 120f)] public float lajuRekamHz = 60f;

    // --- keadaan internal ---
    struct Sampel { public Vector3 p; public Quaternion r; }
    readonly List<Sampel> _rekaman = new List<Sampel>();
    bool _merekam, _memutar;
    int _indexPutar;
    float _waktuSampelBerikut;

    Vector3 _posLokalSebelum;
    Vector3 _kecepatan;
    bool _aktifDorong, _aktifAngkat;
    float _waktuCooldown;
    string _gesturTerakhir = "-";
    float _waktuGesturTerakhir = -10f;
    int _jumlahDeteksi, _jumlahSalah;

    // deteksi lambai: hitung pergantian arah horizontal
    float _arahSebelum;
    int _pergantianArah;
    float _waktuMulaiLambai;

    void Start()
    {
        if (tangan != null && kepala != null)
            _posLokalSebelum = PosisiLokal();
    }

    Vector3 PosisiLokal()
    {
        if (tangan == null) return Vector3.zero;
        if (relatifTerhadapKepala && kepala != null)
            return kepala.InverseTransformPoint(tangan.position);
        return tangan.position;
    }

    void Update()
    {
        if (tangan == null || kepala == null) return;

        TanganiTombol();

        if (_memutar) { PutarRekaman(); return; }
        if (_merekam) Rekam();

        // --- MOVEMENT: kecepatan diturunkan dari perubahan pose ---
        Vector3 posLokal = PosisiLokal();
        _kecepatan = (posLokal - _posLokalSebelum) / Mathf.Max(Time.deltaTime, 0.0001f);
        _posLokalSebelum = posLokal;

        if (_waktuCooldown > 0f) _waktuCooldown -= Time.deltaTime;

        DeteksiDorong(posLokal);
        DeteksiAngkat(posLokal);
        DeteksiLambai(posLokal);
    }

    void TanganiTombol()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            _merekam = !_merekam;
            if (_merekam) { _rekaman.Clear(); _waktuSampelBerikut = 0f; }
        }
        if (Input.GetKeyDown(KeyCode.F2) && _rekaman.Count > 0)
        {
            _memutar = true; _indexPutar = 0; _merekam = false; _waktuSampelBerikut = 0f;
        }
        if (Input.GetKeyDown(KeyCode.F3)) { _rekaman.Clear(); _memutar = false; _merekam = false; }
        if (Input.GetKeyDown(KeyCode.F4)) { _jumlahDeteksi = 0; _jumlahSalah = 0; }

        // Mahasiswa menandai sendiri bila deteksi barusan tidak diinginkan
        if (Input.GetKeyDown(KeyCode.X)) _jumlahSalah++;
    }

    void Rekam()
    {
        if (Time.time < _waktuSampelBerikut) return;
        _waktuSampelBerikut = Time.time + 1f / lajuRekamHz;
        _rekaman.Add(new Sampel { p = tangan.position, r = tangan.rotation });
    }

    void PutarRekaman()
    {
        if (Time.time < _waktuSampelBerikut) return;
        _waktuSampelBerikut = Time.time + 1f / lajuRekamHz;

        if (_indexPutar >= _rekaman.Count) { _memutar = false; return; }
        tangan.SetPositionAndRotation(_rekaman[_indexPutar].p, _rekaman[_indexPutar].r);
        _indexPutar++;
    }

    // ---------- GESTUR 1: DORONG ----------
    void DeteksiDorong(Vector3 posLokal)
    {
        // Pada kerangka kepala, sumbu Z positif berarti ke arah depan pengguna
        float majuMundur = relatifTerhadapKepala ? _kecepatan.z
                                                 : Vector3.Dot(_kecepatan, kepala.forward);

        if (!_aktifDorong && majuMundur > ambangDorong)
        {
            _aktifDorong = true;
            Picu("DORONG");
        }
        // Histeresis: gestur dianggap selesai pada ambang yang lebih rendah
        else if (_aktifDorong && majuMundur < ambangDorong * faktorHisteresis)
        {
            _aktifDorong = false;
        }
    }

    // ---------- GESTUR 2: ANGKAT TANGAN ----------
    void DeteksiAngkat(Vector3 posLokal)
    {
        float tinggiRelatif = relatifTerhadapKepala
            ? posLokal.y
            : tangan.position.y - kepala.position.y;

        if (!_aktifAngkat && tinggiRelatif > ambangAngkat)
        {
            _aktifAngkat = true;
            Picu("ANGKAT TANGAN");
        }
        else if (_aktifAngkat && tinggiRelatif < ambangAngkat * faktorHisteresis)
        {
            _aktifAngkat = false;
        }
    }

    // ---------- GESTUR 3: LAMBAI ----------
    void DeteksiLambai(Vector3 posLokal)
    {
        float kecepatanSamping = relatifTerhadapKepala ? _kecepatan.x
                                                       : Vector3.Dot(_kecepatan, kepala.right);

        float arah = Mathf.Abs(kecepatanSamping) < 0.25f ? 0f : Mathf.Sign(kecepatanSamping);

        if (arah != 0f && _arahSebelum != 0f && arah != _arahSebelum)
        {
            if (Time.time - _waktuMulaiLambai > 1.5f) _pergantianArah = 0;
            if (_pergantianArah == 0) _waktuMulaiLambai = Time.time;
            _pergantianArah++;

            if (_pergantianArah >= ambangLambai)
            {
                Picu("LAMBAI");
                _pergantianArah = 0;
            }
        }
        if (arah != 0f) _arahSebelum = arah;
    }

    void Picu(string nama)
    {
        if (_waktuCooldown > 0f) return;      // masih dalam cooldown
        _waktuCooldown = cooldownDetik;
        _gesturTerakhir = nama;
        _waktuGesturTerakhir = Time.time;
        _jumlahDeteksi++;
    }

    void OnGUI()
    {
        if (tangan == null || kepala == null) return;

        Vector3 pl = PosisiLokal();
        Quaternion q = tangan.rotation;
        Vector3 e = tangan.eulerAngles;

        var st = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true };

        string status = _merekam ? "<color=#ff6b6b>MEREKAM</color>"
                      : _memutar ? "<color=#f0a821>MEMUTAR ULANG</color>"
                      : "<color=#00d1b2>siap</color>";

        string teks =
            $"<b>POSE</b>   [{status}]   sampel tersimpan: {_rekaman.Count}\n" +
            $"Euler      : ({e.x:F1}, {e.y:F1}, {e.z:F1})\n" +
            $"Quaternion : ({q.x:F2}, {q.y:F2}, {q.z:F2}, {q.w:F2})\n" +
            $"Posisi {(relatifTerhadapKepala ? "relatif kepala" : "relatif DUNIA")} : ({pl.x:F2}, {pl.y:F2}, {pl.z:F2})\n" +
            $"\n<b>MOVEMENT</b>\n" +
            $"Kecepatan  : maju {_kecepatan.z:F2}  samping {_kecepatan.x:F2}  naik {_kecepatan.y:F2} m/s\n" +
            $"\n<b>GESTUR</b>\n" +
            $"Terakhir   : {_gesturTerakhir}   ({Time.time - _waktuGesturTerakhir:F1} detik lalu)\n" +
            $"Cooldown   : {Mathf.Max(_waktuCooldown, 0f):F2} detik\n" +
            $"Terdeteksi : {_jumlahDeteksi}   |   ditandai salah (X): {_jumlahSalah}";

        GUI.Box(new Rect(10, 10, 560, 230), teks, st);

        // Sorotan besar saat gestur baru saja terdeteksi
        if (Time.time - _waktuGesturTerakhir < 0.8f)
        {
            var big = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            GUI.color = new Color(0f, 0.82f, 0.70f, 1f - (Time.time - _waktuGesturTerakhir) / 0.8f);
            GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 60), _gesturTerakhir, big);
            GUI.color = Color.white;
        }

        var kecil = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        GUI.Label(new Rect(10, 248, 700, 20),
            "F1 rekam  |  F2 putar ulang  |  F3 hapus  |  F4 reset hitungan  |  X tandai salah deteksi", kecil);
    }
}

// =============================================================================
//  PerformanceLab.cs
//  Mata Kuliah : Realitas Virtual dan Realitas Augmentasi (SE.1) - INF124312
//  Pertemuan 7 : Dasar Accelerator  |  Pengayaan Unity
//  Prodi Informatika - Fakultas Ilmu Komputer - UPN Veteran Jakarta
//
//  Fungsi:
//    Membangkitkan beban render yang dapat diatur, lalu menampilkan angka
//    kinerja secara langsung di layar. Mahasiswa menaikkan beban sampai
//    melewati anggaran frame, lalu menurunkannya kembali dengan teknik
//    akselerasi — TANPA mengurangi jumlah objek.
//
//  Tombol:
//    Panah Atas / Bawah   tambah / kurangi 100 objek
//    I                    GPU instancing (hidup / mati)
//    B                    bayangan real-time (hidup / mati)
//    L                    level of detail sederhana (hidup / mati)
//    R                    skala resolusi render (100% / 70% / 50%)
//    C                    kembalikan semua ke kondisi awal
//    SPASI                tandai titik ukur (dicetak ke Console)
//
//  Pasang pada GameObject kosong bernama "PerformanceLab".
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

public class PerformanceLab : MonoBehaviour
{
    [Header("Pembangkit beban")]
    [Tooltip("Jumlah objek yang dibangkitkan saat Play dimulai.")]
    public int jumlahObjekAwal = 300;

    [Tooltip("Jarak sebar objek dari titik pusat, dalam meter.")]
    public float radiusSebar = 22f;

    [Tooltip("Prefab opsional. Bila kosong, kubus dibuat secara otomatis.")]
    public GameObject prefabObjek;

    [Header("Anggaran frame")]
    [Tooltip("Target frame rate yang dijadikan acuan anggaran waktu.")]
    [Range(30f, 120f)] public float targetFps = 90f;

    [Header("Teknik akselerasi")]
    public bool gpuInstancing = false;
    public bool bayanganRealtime = true;
    public bool levelOfDetail = false;

    [Tooltip("Jarak mulai objek diganti versi sederhana, dalam meter.")]
    [Range(5f, 40f)] public float jarakLod = 14f;

    [Header("Skala resolusi")]
    [Range(0.3f, 1f)] public float skalaResolusi = 1f;

    // --- keadaan internal ---
    readonly List<GameObject> _objek = new List<GameObject>();
    readonly List<Renderer> _renderer = new List<Renderer>();
    readonly List<MeshFilter> _meshFilter = new List<MeshFilter>();

    Material _matInstancing, _matBiasa;
    Mesh _meshDetail, _meshSederhana;
    Light _lampuUtama;
    Transform _kamera;

    float _frameTimeHalus;
    float _akumulasi;
    int _hitunganFrame;
    float _frameTimeTercatat;
    string _catatanTerakhir = "-";

    void Start()
    {
        _kamera = Camera.main != null ? Camera.main.transform : null;
        _lampuUtama = FindObjectOfType<Light>();

        SiapkanAsetBersama();
        AturJumlahObjek(jumlahObjekAwal);
        TerapkanPengaturan();

        // Matikan VSync agar frame time benar-benar mencerminkan beban,
        // bukan dibatasi oleh laju penyegaran monitor.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
    }

    void SiapkanAsetBersama()
    {
        // Dua mesh: satu berdetail tinggi, satu sederhana — untuk latihan LOD
        var bolaSementara = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _meshDetail = bolaSementara.GetComponent<MeshFilter>().sharedMesh;
        Destroy(bolaSementara);

        var kubusSementara = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _meshSederhana = kubusSementara.GetComponent<MeshFilter>().sharedMesh;
        Destroy(kubusSementara);

        Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");

        _matBiasa = new Material(shader) { color = new Color(0.55f, 0.6f, 0.85f) };
        _matBiasa.enableInstancing = false;

        _matInstancing = new Material(shader) { color = new Color(0f, 0.66f, 0.61f) };
        _matInstancing.enableInstancing = true;
    }

    void AturJumlahObjek(int jumlah)
    {
        jumlah = Mathf.Max(0, jumlah);

        while (_objek.Count > jumlah)
        {
            int i = _objek.Count - 1;
            Destroy(_objek[i]);
            _objek.RemoveAt(i);
            _renderer.RemoveAt(i);
            _meshFilter.RemoveAt(i);
        }

        while (_objek.Count < jumlah)
        {
            GameObject go;
            if (prefabObjek != null)
            {
                go = Instantiate(prefabObjek, transform);
            }
            else
            {
                go = new GameObject("Beban_" + _objek.Count);
                go.transform.SetParent(transform);
                go.AddComponent<MeshFilter>().sharedMesh = _meshDetail;
                go.AddComponent<MeshRenderer>();
            }

            Vector2 lingkaran = Random.insideUnitCircle * radiusSebar;
            go.transform.position = new Vector3(lingkaran.x, Random.Range(0.3f, 3.5f), lingkaran.y);
            go.transform.localScale = Vector3.one * Random.Range(0.4f, 0.9f);
            go.transform.rotation = Random.rotation;

            _objek.Add(go);
            _renderer.Add(go.GetComponent<Renderer>());
            _meshFilter.Add(go.GetComponent<MeshFilter>());
        }
    }

    void TerapkanPengaturan()
    {
        // --- GPU instancing: banyak objek bermaterial sama digambar sekaligus ---
        Material m = gpuInstancing ? _matInstancing : _matBiasa;
        foreach (var r in _renderer)
        {
            if (r == null) continue;
            r.sharedMaterial = m;
            r.shadowCastingMode = bayanganRealtime
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = bayanganRealtime;
        }

        if (_lampuUtama != null)
            _lampuUtama.shadows = bayanganRealtime ? LightShadows.Soft : LightShadows.None;
    }

    void Update()
    {
        TanganiTombol();
        PerbaruiLod();
        UkurFrameTime();
    }

    void TanganiTombol()
    {
        bool berubah = false;

        if (Input.GetKeyDown(KeyCode.UpArrow)) { AturJumlahObjek(_objek.Count + 100); berubah = true; }
        if (Input.GetKeyDown(KeyCode.DownArrow)) { AturJumlahObjek(_objek.Count - 100); berubah = true; }
        if (Input.GetKeyDown(KeyCode.I)) { gpuInstancing = !gpuInstancing; berubah = true; }
        if (Input.GetKeyDown(KeyCode.B)) { bayanganRealtime = !bayanganRealtime; berubah = true; }
        if (Input.GetKeyDown(KeyCode.L)) levelOfDetail = !levelOfDetail;

        if (Input.GetKeyDown(KeyCode.R))
        {
            skalaResolusi = skalaResolusi > 0.95f ? 0.7f : (skalaResolusi > 0.6f ? 0.5f : 1f);
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            gpuInstancing = false; bayanganRealtime = true; levelOfDetail = false;
            skalaResolusi = 1f;
            AturJumlahObjek(jumlahObjekAwal);
            berubah = true;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            _catatanTerakhir = $"{_objek.Count} objek · {_frameTimeHalus:F2} ms · " +
                               $"instancing {(gpuInstancing ? "ON" : "off")} · " +
                               $"bayangan {(bayanganRealtime ? "ON" : "off")} · " +
                               $"LOD {(levelOfDetail ? "ON" : "off")} · " +
                               $"resolusi {skalaResolusi * 100f:F0}%";
            Debug.Log("[TITIK UKUR] " + _catatanTerakhir);
        }

        ScalableBufferManager.ResizeBuffers(skalaResolusi, skalaResolusi);

        if (berubah) TerapkanPengaturan();
    }

    /// <summary>LOD sederhana: objek jauh diganti mesh berpoligon rendah.</summary>
    void PerbaruiLod()
    {
        if (_kamera == null) return;

        for (int i = 0; i < _meshFilter.Count; i++)
        {
            if (_meshFilter[i] == null) continue;

            Mesh target = _meshDetail;
            if (levelOfDetail)
            {
                float jarak = Vector3.Distance(_kamera.position, _objek[i].transform.position);
                target = jarak > jarakLod ? _meshSederhana : _meshDetail;
            }

            if (_meshFilter[i].sharedMesh != target) _meshFilter[i].sharedMesh = target;
        }
    }

    void UkurFrameTime()
    {
        _akumulasi += Time.unscaledDeltaTime;
        _hitunganFrame++;

        if (_akumulasi >= 0.25f)
        {
            _frameTimeTercatat = (_akumulasi / _hitunganFrame) * 1000f;
            _akumulasi = 0f; _hitunganFrame = 0;
        }
        _frameTimeHalus = Mathf.Lerp(_frameTimeHalus, _frameTimeTercatat, Time.unscaledDeltaTime * 5f);
    }

    void OnGUI()
    {
        float anggaran = 1000f / targetFps;
        bool lewat = _frameTimeHalus > anggaran;

        var st = new GUIStyle(GUI.skin.box)
        { alignment = TextAnchor.UpperLeft, fontSize = 15, richText = true };

        string warna = lewat ? "#ff6b6b" : "#00d1b2";
        string vonis = lewat ? "MELEWATI ANGGARAN" : "di dalam anggaran";

        string teks =
            $"<b>PERFORMANCE LAB</b>\n" +
            $"Frame time : <color={warna}><b>{_frameTimeHalus:F2} ms</b></color>   " +
            $"(anggaran {anggaran:F2} ms pada {targetFps:F0} Hz)\n" +
            $"Status     : <color={warna}>{vonis}</color>   ·   setara {1000f / Mathf.Max(_frameTimeHalus, 0.01f):F0} fps\n" +
            $"\n" +
            $"Jumlah objek      : <b>{_objek.Count}</b>   (panah atas / bawah)\n" +
            $"GPU instancing    : {(gpuInstancing ? "<color=#00d1b2>AKTIF</color>" : "mati")}   (I)\n" +
            $"Bayangan realtime : {(bayanganRealtime ? "<color=#f0a821>AKTIF</color>" : "mati")}   (B)\n" +
            $"Level of detail   : {(levelOfDetail ? "<color=#00d1b2>AKTIF</color>" : "mati")}   (L)\n" +
            $"Skala resolusi    : {skalaResolusi * 100f:F0}%   (R)\n" +
            $"\n" +
            $"Titik ukur terakhir (SPASI):\n{_catatanTerakhir}";

        GUI.Box(new Rect(10, 10, 600, 300), teks, st);

        // Bar anggaran: hijau selama di bawah batas, merah begitu terlampaui
        float lebar = 580f;
        float rasio = Mathf.Clamp01(_frameTimeHalus / (anggaran * 2f));
        GUI.Box(new Rect(20, 318, lebar, 20), "");
        GUI.color = lewat ? new Color(1f, 0.42f, 0.42f) : new Color(0f, 0.82f, 0.70f);
        GUI.Box(new Rect(20, 318, lebar * rasio, 20), "");
        GUI.color = Color.white;
        // Penanda garis anggaran tepat di tengah bar
        GUI.Box(new Rect(20 + lebar * 0.5f - 1, 314, 2, 28), "");

        var kecil = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        GUI.Label(new Rect(20, 344, 760, 20),
            "Garis tengah = batas anggaran   ·   C mengembalikan semua pengaturan ke awal", kecil);
    }
}

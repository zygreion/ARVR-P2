// =============================================================================
//  TrackerSimulator.cs
//  Mata Kuliah : Realitas Virtual dan Realitas Augmentasi (SE.1) - INF124312
//  Pertemuan 5 : Viewer dan Object Tracking  |  Pengayaan Unity
//  Prodi Informatika - Fakultas Ilmu Komputer - UPN Veteran Jakarta
//
//  Fungsi:
//    Mensimulasikan sebuah tracker yang TIDAK sempurna. Script membaca pose
//    "sebenarnya" dari sebuah Transform, lalu menghasilkan pose "hasil tracking"
//    yang sudah dikotori oleh keterbatasan tracker nyata:
//
//      lajuUpdateHz      -> laju update     (gerak patah-patah)
//      latensiMs         -> latensi         (objek tertinggal)
//      noisePosisiMm     -> presisi/jitter  (objek bergetar)
//      driftMmPerDetik   -> drift           (objek melantur)
//      offsetKalibrasi   -> akurasi         (objek selalu meleset)
//      sensorOptik       -> oklusi          (tracking hilang)
//      penghalusan       -> filter          (getar berkurang, jeda bertambah)
//
//  Satu script dipakai untuk DUA peran:
//    - Viewer tracking : sumber = kepala sebenarnya,  hasil = Main Camera
//    - Object tracking : sumber = benda nyata,        hasil = objek virtual
//
//  Tombol global: R = reset drift pada semua tracker.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

public class TrackerSimulator : MonoBehaviour
{
    public enum PerilakuSaatHilang { TahanPosisiTerakhir, Sembunyikan }

    [Header("Identitas panel")]
    public string label = "Object Tracking";
    [Tooltip("Posisi vertikal panel data di layar, agar dua tracker tidak bertumpuk.")]
    public float posisiPanelY = 10f;

    [Header("Sumber dan keluaran")]
    [Tooltip("Pose SEBENARNYA — benda atau kepala di dunia nyata.")]
    public Transform sumberSebenarnya;
    [Tooltip("Pose HASIL TRACKING — digerakkan oleh script ini.")]
    public Transform hasilTracking;
    [Tooltip("Opsional. Bila diisi, panel menampilkan pose hasil relatif terhadap viewer ini.")]
    public Transform viewerReferensi;

    [Header("Parameter kualitas tracker")]
    [Range(1f, 240f)] public float lajuUpdateHz = 90f;
    [Range(0f, 500f)] public float latensiMs = 0f;
    [Range(0f, 50f)] public float noisePosisiMm = 0f;
    [Range(0f, 5f)] public float noiseRotasiDerajat = 0f;
    [Range(0f, 50f)] public float driftMmPerDetik = 0f;
    [Tooltip("Galat sistematis akibat kalibrasi keliru, dalam meter.")]
    public Vector3 offsetKalibrasi = Vector3.zero;

    [Header("Oklusi (khusus tracking optik)")]
    [Tooltip("Bila diisi, tracking hilang saat garis pandang sensor ke sumber terhalang.")]
    public Transform sensorOptik;
    public PerilakuSaatHilang saatHilang = PerilakuSaatHilang.TahanPosisiTerakhir;

    [Header("Filter")]
    [Tooltip("0 = tanpa filter. Mendekati 1 = sangat halus tetapi sangat tertinggal.")]
    [Range(0f, 0.98f)] public float penghalusan = 0f;

    // --- keadaan internal ---
    struct Sampel { public float t; public Vector3 p; public Quaternion r; }
    readonly Queue<Sampel> _antrian = new Queue<Sampel>();
    float _waktuSampelBerikut;
    Vector3 _drift;
    Vector3 _posMentah, _posFilter;
    Quaternion _rotMentah, _rotFilter;
    bool _siap, _hilang;
    float _error, _errorRata;
    Renderer[] _renderers;

    void Start()
    {
        if (hasilTracking != null)
            _renderers = hasilTracking.GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        if (sumberSebenarnya == null || hasilTracking == null) return;
        float now = Time.time;

        if (Input.GetKeyDown(KeyCode.R)) _drift = Vector3.zero;

        // 1) Drift: galat acak yang menumpuk sedikit demi sedikit (random walk)
        if (driftMmPerDetik > 0f)
            _drift += Random.onUnitSphere * (driftMmPerDetik / 1000f) * Time.deltaTime;

        // 2) Oklusi: sensor optik harus dapat "melihat" sumber
        _hilang = CekOklusi();

        // 3) Sampling pada laju tertentu (sample-and-hold)
        if (now >= _waktuSampelBerikut)
        {
            _waktuSampelBerikut = now + 1f / lajuUpdateHz;
            if (!_hilang)
            {
                Vector3 p = sumberSebenarnya.position
                            + Random.insideUnitSphere * (noisePosisiMm / 1000f)
                            + _drift
                            + offsetKalibrasi;
                Quaternion r = sumberSebenarnya.rotation
                               * Quaternion.Euler(Random.insideUnitSphere * noiseRotasiDerajat);
                _antrian.Enqueue(new Sampel { t = now, p = p, r = r });
            }
        }

        // 4) Latensi: hanya sampel yang "sudah cukup tua" yang boleh dipakai
        float batas = now - latensiMs / 1000f;
        while (_antrian.Count > 0 && _antrian.Peek().t <= batas)
        {
            Sampel s = _antrian.Dequeue();
            _posMentah = s.p;
            _rotMentah = s.r;
            if (!_siap) { _posFilter = _posMentah; _rotFilter = _rotMentah; _siap = true; }
        }
        if (!_siap) return;

        // 5) Filter: exponential smoothing, dibuat tidak bergantung frame rate
        float alpha = 1f - Mathf.Pow(penghalusan, Time.deltaTime * 60f);
        _posFilter = Vector3.Lerp(_posFilter, _posMentah, alpha);
        _rotFilter = Quaternion.Slerp(_rotFilter, _rotMentah, alpha);

        hasilTracking.SetPositionAndRotation(_posFilter, _rotFilter);

        // 6) Perilaku saat tracking hilang
        if (_renderers != null)
        {
            bool tampil = !(_hilang && saatHilang == PerilakuSaatHilang.Sembunyikan);
            foreach (var rd in _renderers) rd.enabled = tampil;
        }

        // 7) Ukur galat terhadap kebenaran
        _error = Vector3.Distance(hasilTracking.position, sumberSebenarnya.position) * 1000f;
        _errorRata = Mathf.Lerp(_errorRata, _error, Time.deltaTime * 2f);
    }

    bool CekOklusi()
    {
        if (sensorOptik == null) return false;

        Vector3 asal = sensorOptik.position;
        Vector3 arah = sumberSebenarnya.position - asal;

        // Abaikan collider milik objek hasil tracking agar tidak menghalangi dirinya sendiri
        RaycastHit[] hits = Physics.RaycastAll(asal, arah.normalized, arah.magnitude);
        foreach (var h in hits)
        {
            if (h.transform == sumberSebenarnya || h.transform.IsChildOf(sumberSebenarnya)) continue;
            if (hasilTracking != null && (h.transform == hasilTracking || h.transform.IsChildOf(hasilTracking))) continue;
            return true;   // ada penghalang di antara sensor dan sumber
        }
        return false;
    }

    void OnDrawGizmos()
    {
        if (sensorOptik != null && sumberSebenarnya != null)
        {
            Gizmos.color = _hilang ? Color.red : Color.green;
            Gizmos.DrawLine(sensorOptik.position, sumberSebenarnya.position);
        }
        if (sumberSebenarnya != null && hasilTracking != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(sumberSebenarnya.position, hasilTracking.position);
        }
    }

    void OnGUI()
    {
        var st = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14 };
        string status = _hilang ? "<color=#ff6b6b>TRACKING HILANG</color>" : "<color=#00d1b2>terlacak</color>";
        st.richText = true;

        string rel = "";
        if (viewerReferensi != null && hasilTracking != null)
        {
            Vector3 lokal = viewerReferensi.InverseTransformPoint(hasilTracking.position);
            rel = $"\nRelatif thd viewer : ({lokal.x:F2}, {lokal.y:F2}, {lokal.z:F2}) m";
        }

        string teks =
            $"<b>{label}</b>   [{status}]\n" +
            $"Laju {lajuUpdateHz:F0} Hz  |  Latensi {latensiMs:F0} ms  |  Noise {noisePosisiMm:F1} mm  |  Drift {driftMmPerDetik:F1} mm/s\n" +
            $"Filter {penghalusan:F2}  |  Galat saat ini {_error:F1} mm  |  Rata-rata {_errorRata:F1} mm" + rel;

        GUI.Box(new Rect(10, posisiPanelY, 640, viewerReferensi != null ? 84 : 66), teks, st);
    }
}

// =============================================================================
//  MaterialHaptik.cs
//  Mata Kuliah : Realitas Virtual dan Realitas Augmentasi (SE.1) - INF124312
//  Pertemuan 4 : Force Feedback Simulation dan Haptic Device
//
//  Dipasang pada objek uji untuk memberinya sifat bahan yang berbeda-beda.
//  Nilai di sini dibaca oleh HapticFeedbackLab saat probe menyentuh objek.
//
//  Nilai acuan untuk latihan:
//    Busa   : kekakuan  60  redaman  2
//    Karet  : kekakuan 250  redaman  8
//    Kayu   : kekakuan 900  redaman  4
//    Logam  : kekakuan 1800 redaman  2
// =============================================================================

using UnityEngine;

public class MaterialHaptik : MonoBehaviour
{
    [Tooltip("Nama bahan, ditampilkan pada panel data di layar.")]
    public string namaBahan = "Bahan";

    [Tooltip("Kekakuan bahan dalam newton per meter. Besar = terasa keras.")]
    [Range(10f, 2000f)] public float kekakuan = 400f;

    [Tooltip("Redaman bahan. Besar = terasa seperti menekan benda basah atau kental.")]
    [Range(0f, 50f)] public float redaman = 5f;
}

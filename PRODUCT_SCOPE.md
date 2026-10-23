# SmartScanner - Batas Produk

## Tujuan

SmartScanner adalah aplikasi penyaringan awal untuk submission tugas pemrograman. Aplikasi ini menangani pemeriksaan yang objektif dan repetitif, lalu menyatakan apakah sebuah submission lolos pemeriksaan awal atau tidak.

SmartScanner bukan sistem penilaian. Hasil screening menjadi informasi pendukung bagi grader sebelum melakukan penilaian manual.

## Keputusan MVP yang dibekukan

Keputusan berikut berlaku untuk MVP dan hanya dapat diubah melalui keputusan scope baru:

- SmartScanner hanya menerima proyek C# console.
- SmartScanner hanya menerima submission berformat ZIP.
- Hasil screening bukan nilai akademik.
- SmartScanner tidak melakukan plagiarism detection.
- Metadata dan riwayat screening disimpan di SQLite.
- ZIP asli, log, dan artifact screening disimpan di filesystem lokal.
- Assignment awal hanya `LATIHAN_1` dan `JURNAL_1`.

Fitur untuk bahasa pemrograman lain, format submission lain, penilaian, atau analisis kode lanjutan berada di luar MVP.

## SmartScanner melakukan

- Menerima metadata mahasiswa dan file ZIP submission.
- Memeriksa format nama file berdasarkan assignment yang dipilih.
- Memeriksa keamanan dan struktur ZIP sebelum ekstraksi.
- Memastikan file `.cs` dan `.csproj` yang diwajibkan tersedia.
- Mengompilasi proyek dalam lingkungan yang terisolasi.
- Menjalankan functional test yang telah ditentukan untuk assignment.
- Menghasilkan status akhir beserta alasan yang objektif dan dapat diperiksa.
- Menyimpan riwayat screening untuk setiap submission.

## SmartScanner tidak melakukan

- Memberikan nilai akademik.
- Menilai kualitas atau kebersihan kode.
- Menilai nama variabel.
- Mendeteksi plagiarisme.
- Membandingkan kode antar mahasiswa.
- Menggantikan keputusan grader.
- Mengubah source code mahasiswa.

## Status akhir

Status internal submission di SmartScanner adalah:

```text
RECEIVED
PROCESSING
PASSED
FAILED
SYSTEM_ERROR
```

Tampilan status untuk pengguna:

| Status internal | Tampilan |
|---|---|
| `RECEIVED` | Belum diproses |
| `PROCESSING` | Sedang diproses |
| `PASSED` | Lolos |
| `FAILED` | Tidak lolos |
| `SYSTEM_ERROR` | Gagal diproses |

Aturan penentuan status akhir:

```text
Jika scanner mengalami kesalahan:
    SYSTEM_ERROR

Jika minimal satu pemeriksaan wajib gagal:
    FAILED

Jika seluruh pemeriksaan wajib berhasil:
    PASSED
```

Setiap pemeriksaan individual menggunakan salah satu hasil berikut:

```text
PASS
FAIL
SKIPPED
ERROR
```

`SKIPPED` digunakan ketika pemeriksaan tidak dapat dijalankan karena tahap sebelumnya gagal. `ERROR` digunakan ketika pemeriksaan gagal dilakukan karena masalah pada SmartScanner atau lingkungan eksekusinya.

Makna hasil akhir yang ditampilkan SmartScanner:

- `LOLOS` (`PASSED`): seluruh pemeriksaan wajib berhasil.
- `TIDAK_LOLOS` (`FAILED`): minimal satu pemeriksaan wajib gagal, disertai alasan.
- `GAGAL_DIPROSES` (`SYSTEM_ERROR`): terjadi masalah pada SmartScanner atau lingkungan eksekusi, sehingga submission belum dapat diklasifikasikan.

Status `GAGAL_DIPROSES` tidak boleh dianggap sebagai kesalahan mahasiswa.

## Prinsip produk

1. Hanya requirement yang objektif dan dapat diverifikasi secara konsisten yang diperiksa otomatis.
2. Setiap status gagal harus memiliki alasan yang jelas dan dapat ditelusuri.
3. Kegagalan sistem harus dibedakan dari kegagalan submission.
4. File asli mahasiswa tidak boleh dimodifikasi.
5. Kode submission harus dianggap tidak tepercaya dan hanya dijalankan dalam lingkungan terisolasi.
6. Keputusan akademik akhir tetap berada pada grader.

## Kriteria penerimaan batas produk

Dokumen ini dianggap terpenuhi apabila setiap fitur yang direncanakan dapat dikategorikan dengan jelas sebagai:

- berada dalam cakupan SmartScanner;
- berada di luar cakupan SmartScanner; atau
- memerlukan keputusan produk sebelum dikembangkan.

Fitur yang tidak mendukung pemeriksaan objektif, pelaporan alasan, keamanan pemrosesan, atau riwayat screening tidak termasuk dalam MVP tanpa keputusan scope baru.

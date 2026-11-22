# PlannerCleaner

Aplikasi console .NET 8 untuk membersihkan tugas Microsoft To Do dan Planner lewat Microsoft Graph:

1. Menghapus tugas yang sudah **selesai** (To Do dan rencana "Private tasks").
2. Menghapus tugas To Do yang **ganda** dengan tugas di "Private tasks" (salinan Private tasks dipertahankan). Tugas To Do berulang ikut terhapus seluruh serinya.
3. Opsional: merapikan ganda di antara To Do sendiri, dan di dalam Planner.

Default berjalan dalam mode **dry run** (hanya menampilkan daftar), lalu meminta konfirmasi `YA` sebelum menghapus.

## Struktur komponen (siap pakai ulang)

| Komponen | Fungsi |
|---|---|
| `Auth/GraphClientFactory` | Login Device Code atau Entra ID (browser), dipilih lewat flag `--auth` |
| `Services/TodoTaskService` | Baca semua daftar dan tugas To Do, hapus tugas |
| `Services/PlannerTaskService` | Cari rencana per nama, baca tugas, hapus dengan header `If-Match` (ETag) |
| `Services/CleanupPlanner` | Logika murni penentuan tugas yang dihapus (tanpa Graph, mudah diuji unit) |
| `Utils/TitleNormalizer` | Normalisasi judul (huruf kecil, spasi dirapikan) |

## 1. Pilih metode login dengan flag `--auth`

| Flag | Metode | Perlu App Registration |
|---|---|---|
| `--auth devicecode` (default) | Kode perangkat, Client ID publik Microsoft Graph Command Line Tools (`14d82eec-204b-4c2f-b7e8-296a70dab67e`) | Tidak |
| `--auth entra` | Login browser interaktif dengan App Registration sendiri | Ya, isi `--client-id` |

### Device Code

1. Jalankan `PlannerCleaner --auth devicecode`, konsol menampilkan URL `https://microsoft.com/devicelogin` dan kode.
2. Buka URL di browser mana pun, masukkan kode, login dengan akun Microsoft 365.
3. Saat pertama kali, setujui izin `Tasks.ReadWrite`. Token disimpan di cache sehingga login berikutnya tidak perlu kode lagi.

Bila ditolak karena kebijakan tenant memblokir device code flow atau consent pengguna, pakai mode Entra.

### Entra ID (App Registration sendiri)

1. Entra admin center > App registrations > New registration, nama `PlannerCleaner`, akun di tenant ini saja.
2. Authentication > Add a platform > **Mobile and desktop applications**, redirect URI `http://localhost`.
3. Authentication > Allow public client flows: **Yes**.
4. API permissions > Microsoft Graph > Delegated: `User.Read`, `Tasks.ReadWrite`, lalu Grant admin consent bila perlu.
5. Salin Application (client) ID ke `Cleaner:EntraClientId` atau berikan lewat `--client-id`.

## 2. Menjalankan

Visual Studio 2022: buka `PlannerCleaner.sln`, atur argumen di Project Properties > Debug > Command line arguments, lalu tekan F5.

VS Code / terminal:

```bash
cd src/PlannerCleaner
dotnet run -- --help                                        # daftar flag
dotnet run -- --auth devicecode                             # dry run dengan device code
dotnet run -- --auth entra --client-id <GUID>               # dry run dengan Entra ID
dotnet run -- --auth devicecode --dry-run false             # eksekusi, tetap minta konfirmasi YA
dotnet run -- --dry-run false --Cleaner:DeleteCompleted=false   # hanya duplikat
```

| Flag | Keterangan |
|---|---|
| `--auth devicecode\|entra` | Metode login |
| `--client-id <GUID>` | Client ID App Registration (mode entra) |
| `--tenant <ID\|domain>` | Tenant ID atau domain |
| `--plan "<nama>"` | Rencana Planner acuan |
| `--dry-run true\|false` | Mode simulasi |

## Pengaturan (`appsettings.json`)

| Kunci | Default | Keterangan |
|---|---|---|
| `AuthMode` | `DeviceCode` | `DeviceCode` atau `Entra` |
| `EntraClientId` | kosong | Client ID App Registration untuk mode Entra |
| `PlanName` | `Private tasks` | Rencana Planner acuan yang dipertahankan |
| `DryRun` | `true` | `false` untuk benar benar menghapus |
| `DeleteCompleted` | `true` | Hapus tugas selesai |
| `DeleteDuplicates` | `true` | Hapus duplikat |
| `DedupeWithinTodo` | `true` | Ganda antar To Do, sisakan satu (berulang dan paling lama diutamakan) |
| `DedupeWithinPlanner` | `false` | Ganda di dalam Planner, sisakan yang paling lama |
| `ExcludedTodoLists` | `Flagged Emails` | Daftar To Do yang dilewati |

## Catatan

- Penghapusan bersifat **permanen**. Selalu jalankan dry run dulu.
- Bila rencana "Private tasks" tidak terbaca oleh Graph (beberapa rencana pribadi di Planner baru tidak tersedia di Graph v1.0), aplikasi tidak memproses duplikat terhadap Planner agar tidak salah hapus, dan hanya memproses tugas selesai serta ganda antar To Do.

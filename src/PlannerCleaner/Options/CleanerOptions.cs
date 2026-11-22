namespace PlannerCleaner.Options;

/// <summary>Metode login ke Microsoft Graph.</summary>
public enum AuthMode
{
    /// <summary>Device code dengan Client ID publik Microsoft Graph Command Line Tools. Tanpa App Registration.</summary>
    DeviceCode,

    /// <summary>Login browser interaktif dengan App Registration sendiri di Entra ID.</summary>
    Entra
}

/// <summary>Konfigurasi dari bagian "Cleaner" di appsettings.json atau argumen baris perintah.</summary>
public sealed class CleanerOptions
{
    public const string GraphCliClientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e";

    public AuthMode AuthMode { get; set; } = AuthMode.DeviceCode;

    public string TenantId { get; set; } = "organizations";

    /// <summary>Client ID App Registration sendiri, dipakai hanya pada mode Entra.</summary>
    public string EntraClientId { get; set; } = string.Empty;

    /// <summary>Redirect URI App Registration (platform Mobile and desktop), dipakai hanya pada mode Entra.</summary>
    public string RedirectUri { get; set; } = "http://localhost";

    /// <summary>Nama rencana Planner yang menjadi sumber utama (salinannya dipertahankan).</summary>
    public string PlanName { get; set; } = "Private tasks";

    /// <summary>True: hanya menampilkan rencana penghapusan tanpa menghapus.</summary>
    public bool DryRun { get; set; } = true;

    public bool DeleteCompleted { get; set; } = true;
    public bool DeleteDuplicates { get; set; } = true;

    /// <summary>Hapus tugas To Do yang judulnya ganda di antara daftar To Do sendiri (sisakan satu).</summary>
    public bool DedupeWithinTodo { get; set; } = true;

    /// <summary>Hapus tugas ganda di dalam rencana Planner (sisakan yang paling lama dibuat). Default mati agar Private tasks aman.</summary>
    public bool DedupeWithinPlanner { get; set; } = false;

    public List<string> ExcludedTodoLists { get; set; } = new();

    public static readonly string[] Scopes = { "User.Read", "Tasks.ReadWrite" };
}

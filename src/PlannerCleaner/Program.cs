using Microsoft.Extensions.Configuration;
using Microsoft.Graph.Models.ODataErrors;
using PlannerCleaner.Auth;
using PlannerCleaner.Models;
using PlannerCleaner.Options;
using PlannerCleaner.Services;

// Contoh:
//   dotnet run -- --auth devicecode
//   dotnet run -- --auth entra --client-id <GUID> --dry-run false
Console.Title = "PlannerCleaner";
Console.WriteLine("==============================================");
Console.WriteLine(" PlannerCleaner");
Console.WriteLine(" Hapus tugas selesai dan tugas ganda (To Do + Planner)");
Console.WriteLine("==============================================");

if (args.Any(a => a is "--help" or "-h" or "/?"))
{
    PrintHelp();
    return 0;
}

var switchMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["--auth"] = "Cleaner:AuthMode",
    ["--client-id"] = "Cleaner:EntraClientId",
    ["--tenant"] = "Cleaner:TenantId",
    ["--plan"] = "Cleaner:PlanName",
    ["--dry-run"] = "Cleaner:DryRun"
};

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddCommandLine(args, switchMappings)
    .Build();

CleanerOptions options;
try
{
    options = config.GetSection("Cleaner").Get<CleanerOptions>() ?? new CleanerOptions();
}
catch (InvalidOperationException)
{
    Console.WriteLine("Nilai --auth tidak valid. Gunakan: devicecode atau entra.");
    return 1;
}
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    var graph = GraphClientFactory.Create(options);
    var todo = new TodoTaskService(graph);
    var planner = new PlannerTaskService(graph);

    Console.WriteLine("Login dan membaca tugas...");
    var plan = await planner.FindPlanAsync(options.PlanName, cts.Token);
    if (plan is null)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Rencana '{options.PlanName}' tidak ditemukan. Duplikat terhadap Planner tidak diproses agar tidak salah hapus.");
        Console.ResetColor();
    }

    var todoTasks = await todo.GetAllTasksAsync(options.ExcludedTodoLists, cts.Token);
    var plannerTasks = plan is null ? new List<TaskItem>() : await planner.GetTasksAsync(plan, cts.Token);
    Console.WriteLine($"To Do: {todoTasks.Count} tugas | Planner '{options.PlanName}': {plannerTasks.Count} tugas");

    var actions = CleanupPlanner.Build(todoTasks, plannerTasks, options);
    if (actions.Count == 0)
    {
        Console.WriteLine("Tidak ada tugas yang perlu dihapus.");
        return 0;
    }

    PrintPlan(actions);

    if (options.DryRun)
    {
        Console.WriteLine("\nMode DRY RUN: tidak ada yang dihapus. Jalankan dengan --Cleaner:DryRun=false untuk eksekusi.");
        return 0;
    }

    Console.Write($"\nHapus permanen {actions.Count} tugas di atas? Ketik 'YA' untuk lanjut: ");
    if (!string.Equals(Console.ReadLine()?.Trim(), "YA", StringComparison.Ordinal))
    {
        Console.WriteLine("Dibatalkan.");
        return 0;
    }

    int ok = 0, fail = 0;
    foreach (var a in actions)
    {
        try
        {
            if (a.Task.Source == TaskSource.Todo) await todo.DeleteAsync(a.Task, cts.Token);
            else await planner.DeleteAsync(a.Task, cts.Token);
            ok++;
            Console.WriteLine($"  [OK]    {a.Task.Title}");
        }
        catch (ODataError ex)
        {
            fail++;
            Console.WriteLine($"  [GAGAL] {a.Task.Title}: {ex.Error?.Code} {ex.Error?.Message}");
        }
    }

    Console.WriteLine($"\nSelesai. Terhapus: {ok}, gagal: {fail}.");
    return fail == 0 ? 0 : 2;
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: {ex.Message}");
    Console.ResetColor();
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine("""

    Penggunaan: PlannerCleaner [opsi]

      --auth devicecode|entra   Metode login (default: devicecode)
                                devicecode : kode perangkat, tanpa App Registration
                                entra      : login browser dengan App Registration sendiri
      --client-id <GUID>        Client ID App Registration (wajib untuk --auth entra)
      --tenant <ID|domain>      Tenant ID atau domain (default dari appsettings.json)
      --plan "<nama>"           Rencana Planner acuan (default: Private tasks)
      --dry-run true|false      true = hanya tampilkan daftar (default: true)
      --help                    Tampilkan bantuan ini

    Contoh:
      PlannerCleaner --auth devicecode
      PlannerCleaner --auth entra --client-id 00000000-0000-0000-0000-000000000000 --dry-run false
    """);
}

static void PrintPlan(List<CleanupAction> actions)
{
    Console.WriteLine($"\nRencana penghapusan ({actions.Count} tugas):");
    foreach (var group in actions.GroupBy(a => a.Reason))
    {
        Console.WriteLine($"\n== {Describe(group.Key)} ({group.Count()}) ==");
        foreach (var a in group.OrderBy(a => a.Task.Title))
        {
            var rec = a.Task.IsRecurring ? " [berulang]" : string.Empty;
            Console.WriteLine($"  - {a.Task.Title}{rec}  ({a.Task.Source}: {a.Task.ContainerName})");
        }
    }
}

static string Describe(CleanupReason r) => r switch
{
    CleanupReason.Completed => "Tugas selesai",
    CleanupReason.DuplicateOfPlanner => "Ganda dengan Private tasks (salinan To Do dihapus)",
    CleanupReason.DuplicateWithinTodo => "Ganda di antara To Do",
    CleanupReason.DuplicateWithinPlanner => "Ganda di dalam Planner",
    _ => r.ToString()
};

using PlannerCleaner.Models;
using PlannerCleaner.Options;
using PlannerCleaner.Utils;

namespace PlannerCleaner.Services;

/// <summary>Logika murni (tanpa Graph): menentukan tugas mana yang dihapus. Mudah diuji unit.</summary>
public static class CleanupPlanner
{
    public static List<CleanupAction> Build(
        IReadOnlyList<TaskItem> todoTasks,
        IReadOnlyList<TaskItem> plannerTasks,
        CleanerOptions options)
    {
        var actions = new List<CleanupAction>();
        var marked = new HashSet<string>();

        void Mark(TaskItem t, CleanupReason r)
        {
            if (marked.Add($"{t.Source}:{t.Id}")) actions.Add(new CleanupAction(t, r));
        }

        // 1. Tugas selesai di kedua sumber.
        if (options.DeleteCompleted)
        {
            foreach (var t in todoTasks.Concat(plannerTasks).Where(t => t.IsCompleted))
                Mark(t, CleanupReason.Completed);
        }

        if (!options.DeleteDuplicates) return actions;

        // 2. Tugas To Do yang judulnya sudah ada di rencana Planner (Planner dipertahankan).
        var plannerTitles = plannerTasks
            .Select(t => TitleNormalizer.Normalize(t.Title))
            .ToHashSet();

        foreach (var t in todoTasks.Where(t => plannerTitles.Contains(TitleNormalizer.Normalize(t.Title))))
            Mark(t, CleanupReason.DuplicateOfPlanner);

        // 3. Ganda di antara To Do sendiri: sisakan satu (berulang diutamakan, lalu yang paling lama).
        if (options.DedupeWithinTodo)
        {
            foreach (var group in todoTasks
                         .Where(t => !marked.Contains($"{t.Source}:{t.Id}"))
                         .GroupBy(t => TitleNormalizer.Normalize(t.Title))
                         .Where(g => g.Count() > 1))
            {
                foreach (var extra in OrderKeepFirst(group).Skip(1))
                    Mark(extra, CleanupReason.DuplicateWithinTodo);
            }
        }

        // 4. Ganda di dalam rencana Planner (opsional).
        if (options.DedupeWithinPlanner)
        {
            foreach (var group in plannerTasks
                         .Where(t => !marked.Contains($"{t.Source}:{t.Id}"))
                         .GroupBy(t => TitleNormalizer.Normalize(t.Title))
                         .Where(g => g.Count() > 1))
            {
                foreach (var extra in OrderKeepFirst(group).Skip(1))
                    Mark(extra, CleanupReason.DuplicateWithinPlanner);
            }
        }

        return actions;
    }

    private static IEnumerable<TaskItem> OrderKeepFirst(IEnumerable<TaskItem> group) =>
        group.OrderByDescending(t => t.IsRecurring)
             .ThenBy(t => t.CreatedAt ?? DateTimeOffset.MaxValue);
}

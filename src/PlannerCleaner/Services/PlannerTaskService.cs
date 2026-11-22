using Microsoft.Graph;
using Microsoft.Graph.Models;
using PlannerCleaner.Models;

namespace PlannerCleaner.Services;

/// <summary>Membaca dan menghapus tugas di satu rencana Planner (default "Private tasks").</summary>
public sealed class PlannerTaskService
{
    private readonly GraphServiceClient _graph;

    public PlannerTaskService(GraphServiceClient graph) => _graph = graph;

    /// <summary>Mencari rencana berdasarkan judul. Coba /me/planner/plans, lalu fallback lewat /me/planner/tasks.</summary>
    public async Task<PlannerPlan?> FindPlanAsync(string planName, CancellationToken ct = default)
    {
        var plans = await _graph.Me.Planner.Plans.GetAsync(cancellationToken: ct);
        var match = plans?.Value?.FirstOrDefault(p =>
            string.Equals(p.Title, planName, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match;

        // Fallback: rencana pribadi kadang tidak muncul di /me/planner/plans.
        var myTasks = await GetPagedAsync(await _graph.Me.Planner.Tasks.GetAsync(cancellationToken: ct), ct);
        foreach (var planId in myTasks.Select(t => t.PlanId).Where(id => id is not null).Distinct())
        {
            try
            {
                var plan = await _graph.Planner.Plans[planId].GetAsync(cancellationToken: ct);
                if (string.Equals(plan?.Title, planName, StringComparison.OrdinalIgnoreCase)) return plan;
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError) { /* rencana tidak dapat dibaca, lewati */ }
        }
        return null;
    }

    public async Task<List<TaskItem>> GetTasksAsync(PlannerPlan plan, CancellationToken ct = default)
    {
        var page = await _graph.Planner.Plans[plan.Id].Tasks.GetAsync(cancellationToken: ct);
        var tasks = await GetPagedAsync(page, ct);

        return tasks.Select(t => new TaskItem(
            TaskSource.Planner,
            t.Id!,
            t.Title ?? string.Empty,
            plan.Id!,
            plan.Title ?? "(rencana)",
            t.PercentComplete == 100,
            t.AdditionalData?.ContainsKey("recurrence") == true,
            t.CreatedDateTime,
            ReadETag(t))).ToList();
    }

    /// <summary>Planner mewajibkan header If-Match berisi ETag terbaru.</summary>
    public async Task DeleteAsync(TaskItem task, CancellationToken ct = default)
    {
        var etag = task.ETag;
        if (string.IsNullOrEmpty(etag))
        {
            var fresh = await _graph.Planner.Tasks[task.Id].GetAsync(cancellationToken: ct);
            etag = fresh is null ? null : ReadETag(fresh);
        }
        if (string.IsNullOrEmpty(etag))
            throw new InvalidOperationException($"ETag tidak ditemukan untuk tugas Planner '{task.Title}'.");

        await _graph.Planner.Tasks[task.Id].DeleteAsync(rc => rc.Headers.Add("If-Match", etag), ct);
    }

    private async Task<List<PlannerTask>> GetPagedAsync(PlannerTaskCollectionResponse? page, CancellationToken ct)
    {
        var all = new List<PlannerTask>();
        if (page is null) return all;
        var it = PageIterator<PlannerTask, PlannerTaskCollectionResponse>
            .CreatePageIterator(_graph, page, t => { all.Add(t); return true; });
        await it.IterateAsync(ct);
        return all;
    }

    private static string? ReadETag(PlannerTask t) =>
        t.AdditionalData is not null && t.AdditionalData.TryGetValue("@odata.etag", out var v) ? v?.ToString() : null;
}

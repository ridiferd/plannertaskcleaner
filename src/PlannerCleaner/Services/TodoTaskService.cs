using Microsoft.Graph;
using Microsoft.Graph.Models;
using PlannerCleaner.Models;
using GraphTaskStatus = Microsoft.Graph.Models.TaskStatus;

namespace PlannerCleaner.Services;

/// <summary>Membaca dan menghapus tugas Microsoft To Do lewat Graph (/me/todo).</summary>
public sealed class TodoTaskService
{
    private readonly GraphServiceClient _graph;

    public TodoTaskService(GraphServiceClient graph) => _graph = graph;

    public async Task<List<TaskItem>> GetAllTasksAsync(IEnumerable<string> excludedLists, CancellationToken ct = default)
    {
        var excluded = new HashSet<string>(excludedLists, StringComparer.OrdinalIgnoreCase);
        var lists = new List<TodoTaskList>();

        var listPage = await _graph.Me.Todo.Lists.GetAsync(cancellationToken: ct);
        if (listPage is not null)
        {
            var it = PageIterator<TodoTaskList, TodoTaskListCollectionResponse>
                .CreatePageIterator(_graph, listPage, l => { lists.Add(l); return true; });
            await it.IterateAsync(ct);
        }

        var result = new List<TaskItem>();
        foreach (var list in lists.Where(l => !excluded.Contains(l.DisplayName ?? string.Empty)))
        {
            var taskPage = await _graph.Me.Todo.Lists[list.Id].Tasks
                .GetAsync(rc => rc.QueryParameters.Top = 100, ct);
            if (taskPage is null) continue;

            var it = PageIterator<TodoTask, TodoTaskCollectionResponse>.CreatePageIterator(_graph, taskPage, t =>
            {
                result.Add(new TaskItem(
                    TaskSource.Todo,
                    t.Id!,
                    t.Title ?? string.Empty,
                    list.Id!,
                    list.DisplayName ?? "(tanpa nama)",
                    t.Status == GraphTaskStatus.Completed,
                    t.Recurrence is not null,
                    t.CreatedDateTime,
                    null));
                return true;
            });
            await it.IterateAsync(ct);
        }

        return result;
    }

    /// <summary>
    /// Menghapus tugas To Do. Pengulangan melekat pada tugas, jadi menghapus tugas
    /// berulang sekaligus menghentikan seluruh serinya.
    /// </summary>
    public Task DeleteAsync(TaskItem task, CancellationToken ct = default) =>
        _graph.Me.Todo.Lists[task.ContainerId].Tasks[task.Id].DeleteAsync(cancellationToken: ct);
}

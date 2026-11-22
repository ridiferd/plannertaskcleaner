namespace PlannerCleaner.Models;

public enum TaskSource
{
    Todo,
    Planner
}

public enum CleanupReason
{
    Completed,
    DuplicateOfPlanner,
    DuplicateWithinTodo,
    DuplicateWithinPlanner
}

/// <summary>Representasi seragam untuk tugas To Do maupun Planner.</summary>
public sealed record TaskItem(
    TaskSource Source,
    string Id,
    string Title,
    string ContainerId,
    string ContainerName,
    bool IsCompleted,
    bool IsRecurring,
    DateTimeOffset? CreatedAt,
    string? ETag);

public sealed record CleanupAction(TaskItem Task, CleanupReason Reason);

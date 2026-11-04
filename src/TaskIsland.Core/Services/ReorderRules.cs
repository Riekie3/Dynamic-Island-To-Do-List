namespace TaskIsland.Core.Services;

/// <summary>Drag-to-reorder maths (PLAN.md section 6.4): a task moves inside its own (list, priority) group only.</summary>
public static class ReorderRules
{
    /// <summary>
    /// Returns <paramref name="ids"/> with <paramref name="movingId"/> taken out and put so that it ends up at
    /// <paramref name="insertIndex"/> counted in the ORIGINAL list (0 = before the first item, Count = after the last).
    /// Returns null when the id is unknown or the order would not change.
    /// </summary>
    public static IReadOnlyList<long>? Move(IReadOnlyList<long> ids, long movingId, int insertIndex)
    {
        int from = IndexOf(ids, movingId);
        if (from < 0) return null;

        insertIndex = Math.Clamp(insertIndex, 0, ids.Count);
        // Dropping directly above or below itself changes nothing.
        if (insertIndex == from || insertIndex == from + 1) return null;

        var list = ids.ToList();
        list.RemoveAt(from);
        list.Insert(insertIndex > from ? insertIndex - 1 : insertIndex, movingId);
        return list;
    }

    /// <summary>One step up (-1) or down (+1) inside the group (keyboard alternative to dragging). Null at the ends.</summary>
    public static IReadOnlyList<long>? Step(IReadOnlyList<long> ids, long movingId, int direction)
    {
        int from = IndexOf(ids, movingId);
        if (from < 0) return null;
        int to = from + Math.Sign(direction);
        if (to < 0 || to >= ids.Count) return null;
        var list = ids.ToList();
        (list[from], list[to]) = (list[to], list[from]);
        return list;
    }

    private static int IndexOf(IReadOnlyList<long> ids, long id)
    {
        for (int i = 0; i < ids.Count; i++)
            if (ids[i] == id) return i;
        return -1;
    }
}

namespace BATT_PPMS.Helpers;

public sealed class PageWindow
{
    public const int Size = 10;
    public int TotalCount { get; }
    public int PageSize { get; }
    public int TotalPages { get; }
    public int CurrentPage { get; }
    public int Offset => (CurrentPage - 1) * PageSize;
    public int FirstItem => TotalCount == 0 ? 0 : Offset + 1;
    public int LastItem => Offset + Math.Min(PageSize, TotalCount - Offset);

    public PageWindow(int totalCount, int requestedPage, int pageSize = Size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        PageSize = pageSize;
        TotalCount = Math.Max(0, totalCount);
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        CurrentPage = Math.Clamp(requestedPage, 1, TotalPages);
    }

    // "page" is a reserved Razor route value. Prefer pageNumber, but still
    // accept old pagination URLs by reading their query string explicitly.
    public static int ReadRequestedPage(Microsoft.AspNetCore.Http.IQueryCollection query, int fallback = 1)
    {
        var key = query.ContainsKey("pageNumber") ? "pageNumber" : "page";
        return int.TryParse(query[key], out var number) ? Math.Max(1, number) : Math.Max(1, fallback);
    }
}

public record AdminPaginationViewModel(PageWindow Window, string PageName,
    Dictionary<string, string?> Filters, string PageNumberParameter = "pageNumber");

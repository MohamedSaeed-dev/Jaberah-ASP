using Microsoft.EntityFrameworkCore;

namespace Jaberah.Helpers
{
    public class PagedList<T>
    {
        public IEnumerable<T> Data { get; private set; }
        public int TotalPages { get; private set; }
        public int CurrentPage { get; private set; }
        public int PageSize { get; private set; }
        public int TotalCount { get; private set; }
        public bool HasNext { get; private set; }
        public bool HasPrevious { get; private set; }

        public PagedList(IEnumerable<T> data, int count, int page, int pageSize)
        {
            Data = data;
            CurrentPage = page;
            PageSize = pageSize;
            TotalCount = count;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);

            HasNext = CurrentPage < TotalPages;
            HasPrevious = CurrentPage > 1;
        }
    }

    public static class IQueryableExtensions
    {
        public static async Task<PagedList<T>> ToPagedListAsync<T>(
            this IQueryable<T> source,
            int pageNumber,
            int pageSize)
        {
            var count = await source.CountAsync();
            var data = await source
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedList<T>(data, count, pageNumber, pageSize);
        }
        public static PagedList<T> ToPagedList<T>(this IEnumerable<T> source, int count, int pageNumber, int pageSize) => new(source.ToList(), count, pageNumber, pageSize);
    }
}

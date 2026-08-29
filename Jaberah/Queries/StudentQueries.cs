using Jaberah.Models.JaberahModels;

namespace Jaberah.Queries
{
    /// <summary>
    /// Query shaping for <see cref="Student"/>.
    ///
    /// This lives outside the controller so the filtering and ordering rules can be
    /// exercised by unit tests without an HTTP request or a database connection.
    /// </summary>
    public static class StudentQueries
    {
        /// <summary>
        /// Applies the student list filters and the default sort order.
        ///
        /// The sort MUST be applied here, before any paging is layered on top by the
        /// caller. Ordering after <c>Skip</c>/<c>Take</c> only sorts the rows that
        /// happen to land on the requested page, which silently returns the wrong
        /// students for every page.
        /// </summary>
        public static IQueryable<Student> FilterAndSort(
            IQueryable<Student> source,
            string searchText = "",
            bool withoutGroup = false)
        {
            var query = source.Where(x => x.StudentName.Contains(searchText));

            if (withoutGroup)
            {
                query = query.Where(x => !x.GroupId.HasValue);
            }

            return query.OrderByDescending(x => x.MemoRate);
        }
    }
}

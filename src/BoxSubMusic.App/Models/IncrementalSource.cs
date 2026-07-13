using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace BoxSubMusic.App.Models
{
    /// <summary>
    /// Items loaded page-by-page as the user scrolls (decision 2026-07-13:
    /// browse views use incremental loading because one API request caps at
    /// 500 items and libraries can be far larger). The host page awaits
    /// <see cref="LoadFirstPageAsync"/> so it can drive its
    /// loading/empty/error states; the view then pulls further pages itself
    /// through <see cref="ISupportIncrementalLoading"/>. A failed later page
    /// raises <see cref="LoadFailed"/> instead of throwing into the view
    /// (§3: failures must surface explicitly).
    /// </summary>
    public sealed class IncrementalSource<T> : ObservableCollection<T>, ISupportIncrementalLoading
    {
        public const int PageSize = 100;

        private readonly Func<int, int, CancellationToken, Task<IReadOnlyList<T>>> _fetchPage;

        // False until the first page proves more may exist, so the views
        // don't race the host's explicit first load.
        private bool _hasMore;
        private bool _loading;

        public IncrementalSource(
            Func<int, int, CancellationToken, Task<IReadOnlyList<T>>> fetchPage)
        {
            _fetchPage = fetchPage ?? throw new ArgumentNullException(nameof(fetchPage));
        }

        /// <summary>Raised when a later page fails to load; the host owns the error UX.</summary>
        public event EventHandler<Exception> LoadFailed;

        public bool HasMoreItems => _hasMore && !_loading;

        /// <summary>
        /// Loads page one, throwing on failure so the host page can show its
        /// error state. Returns the number of items loaded.
        /// </summary>
        public async Task<int> LoadFirstPageAsync(CancellationToken cancellationToken)
        {
            _loading = true;
            try
            {
                var page = await _fetchPage(0, PageSize, cancellationToken);
                foreach (var item in page)
                {
                    Add(item);
                }

                _hasMore = page.Count == PageSize;
                return page.Count;
            }
            finally
            {
                _loading = false;
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(LoadNextPageAsync);
        }

        private async Task<LoadMoreItemsResult> LoadNextPageAsync(CancellationToken cancellationToken)
        {
            if (_loading || !_hasMore)
            {
                return new LoadMoreItemsResult { Count = 0 };
            }

            _loading = true;
            try
            {
                var page = await _fetchPage(Count, PageSize, cancellationToken);
                foreach (var item in page)
                {
                    Add(item);
                }

                _hasMore = page.Count == PageSize;
                return new LoadMoreItemsResult { Count = (uint)page.Count };
            }
            catch (OperationCanceledException)
            {
                // The view tore the operation down (e.g. navigation); nothing
                // to report and the next scroll may try again.
                return new LoadMoreItemsResult { Count = 0 };
            }
            catch (Exception ex)
            {
                // Stop pulling and hand the failure to the host page; letting
                // it escape here would fault the view with no UX at all.
                _hasMore = false;
                LoadFailed?.Invoke(this, ex);
                return new LoadMoreItemsResult { Count = 0 };
            }
            finally
            {
                _loading = false;
            }
        }
    }
}

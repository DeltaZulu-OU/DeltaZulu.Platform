
using DeltaZulu.Platform.Web.Analytics.Services;
using Microsoft.AspNetCore.Components;

namespace DeltaZulu.Platform.Web.Analytics.Library;
public sealed class LibraryPageController
{
    private readonly EditorBus _editorBus;
    private readonly LibraryService _library;
    private readonly NavigationManager _navigation;

    public LibraryPageController(
        LibraryService library,
        EditorBus editorBus,
        NavigationManager navigation)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _editorBus = editorBus ?? throw new ArgumentNullException(nameof(editorBus));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    public LibraryPageState State { get; } = new();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        State.Loading = true;
        State.Error = null;

        try
        {
            var items = await _library.ListAsync(cancellationToken);
            State.Items.Clear();
            State.Items.AddRange(items);
            State.ClampPage();
        }
        catch (Exception ex)
        {
            State.Error = $"Could not load library. {ex.Message}";
        }
        finally
        {
            State.Loading = false;
        }
    }

    public async Task OpenItemAsync(LibraryItem item, CancellationToken cancellationToken = default)
    {
        State.ResetPendingDelete();

        switch (item.Kind)
        {
            case LibraryItemKind.Dashboard:
                _navigation.NavigateTo($"/analytics/dashboards/{item.Id}");
                return;

            case LibraryItemKind.SavedQuery:
                await OpenSavedQueryAsync(item.Id, cancellationToken);
                return;

            case LibraryItemKind.Visualization:
                await OpenVisualizationAsync(item.Id, cancellationToken);
                return;
        }
    }

    public async Task DeleteItemAsync(LibraryItem item, CancellationToken cancellationToken = default)
    {
        if (!State.IsDeletePendingFor(item))
        {
            State.Error = null;
            State.PendingDeleteId = item.Id;
            State.PendingDeleteKind = item.Kind;
            State.Message = $"Click Confirm delete to delete {LibraryLabels.KindLabel(item.Kind).ToLowerInvariant()} '{item.Name}'.";
            return;
        }

        State.Deleting = true;
        State.Error = null;
        State.Message = null;

        try
        {
            await _library.DeleteAsync(item, cancellationToken);
            State.Message = $"{LibraryLabels.KindLabel(item.Kind)} '{item.Name}' deleted.";
            State.ResetPendingDelete();
            await LoadAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            State.Message = ex.Message;
            State.ResetPendingDelete();
        }
        catch (Exception ex)
        {
            State.Error = $"Could not delete {LibraryLabels.KindLabel(item.Kind).ToLowerInvariant()} '{item.Name}'. {ex.Message}";
            State.ResetPendingDelete();
        }
        finally
        {
            State.Deleting = false;
        }
    }

    public void CreateSavedQuery()
    {
        State.ResetPendingDelete();
        _editorBus.RequestInsert(
            """
            ProcessEvent
            | where FileName has "powershell"
            | project Timestamp, DeviceName, AccountName, ProcessCommandLine
            | take 50
            """);
        _navigation.NavigateTo("/analytics");
    }

    public void CreateVisualization()
    {
        State.ResetPendingDelete();
        _editorBus.RequestInsert(
            """
            ProcessEvent
            | summarize Count = count() by AccountName
            | render barchart xcolumn=AccountName ycolumns=Count title='Events by account'
            """);
        _navigation.NavigateTo("/analytics");
    }

    public void CreateDashboard()
    {
        State.ResetPendingDelete();
        _navigation.NavigateTo("/analytics/dashboards");
    }

    public void SetFilter(LibraryItemKind? kind)
    {
        State.KindFilter = kind;
        State.Page = 1;
        State.ResetPendingDelete();
    }

    public void SetSearchText(string? searchText)
    {
        State.SearchText = searchText ?? string.Empty;
        State.Page = 1;
        State.ResetPendingDelete();
    }

    public void ClearSearch()
    {
        State.SearchText = string.Empty;
        State.KindFilter = null;
        State.Page = 1;
        State.ResetPendingDelete();
    }

    public void PreviousPage()
    {
        if (State.Page <= 1)
        {
            return;
        }

        State.Page--;
        State.ResetPendingDelete();
    }

    public void NextPage()
    {
        if (State.Page >= State.TotalPages)
        {
            return;
        }

        State.Page++;
        State.ResetPendingDelete();
    }

    public string FilterButtonClass(LibraryItemKind? kind)
        => State.KindFilter == kind
            ? "hunt-btn hunt-btn-run dashboard-primary-action"
            : "hunt-btn";

    public bool IsFilterActive(LibraryItemKind? kind)
        => State.KindFilter == kind;

    public string DeleteButtonText(LibraryItem item)
        => State.IsDeletePendingFor(item) ? "Confirm delete" : "Delete";

    public string DeleteButtonTitle(LibraryItem item)
        => State.IsDeletePendingFor(item)
            ? $"Confirm deleting {LibraryLabels.KindLabel(item.Kind).ToLowerInvariant()} '{item.Name}'"
            : $"Delete {LibraryLabels.KindLabel(item.Kind).ToLowerInvariant()} '{item.Name}'";

    public string DeleteButtonClass(LibraryItem item)
        => State.IsDeletePendingFor(item)
            ? "hunt-btn hunt-btn-run dashboard-primary-action"
            : "hunt-btn hunt-btn-clear";

    private async Task OpenSavedQueryAsync(string id, CancellationToken cancellationToken)
    {
        var queryText = await _library.LoadSavedQueryTextAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(queryText))
        {
            State.Error = "Saved query could not be opened because its query text was not found.";
            return;
        }

        _editorBus.RequestInsert(queryText);
        _navigation.NavigateTo("/analytics");
    }

    private async Task OpenVisualizationAsync(string id, CancellationToken cancellationToken)
    {
        var queryText = await _library.LoadVisualizationTextAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(queryText))
        {
            State.Error = "Visualization could not be opened because its saved query or visualization definition was not found.";
            return;
        }

        _editorBus.RequestInsert(queryText);
        _navigation.NavigateTo("/analytics");
    }
}

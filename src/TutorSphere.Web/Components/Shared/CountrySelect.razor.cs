using Microsoft.AspNetCore.Components;
using TutorSphere.Web.Common;

namespace TutorSphere.Web.Components.Shared;

/// <summary>
/// Searchable country picker (flag + name), backed by <see cref="CountryCatalog"/>.
/// Requires a live circuit (InteractiveServer, not prerendered) — it is a plain C#
/// event-driven dropdown, not a native &lt;select&gt;.
/// </summary>
public partial class CountrySelect : ComponentBase
{
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string Placeholder { get; set; } = "Sélectionner un pays…";
    [Parameter] public string SearchPlaceholder { get; set; } = "Rechercher un pays ou un code…";
    [Parameter] public string EmptyText { get; set; } = "Aucun pays trouvé.";

    private bool _isOpen;
    private string _search = "";
    private List<CountryInfo> _filtered = CountryCatalog.All.ToList();

    private CountryInfo? SelectedCountry => CountryCatalog.Find(Value);

    private void ToggleDropdown()
    {
        if (Disabled) return;
        _isOpen = !_isOpen;
        if (_isOpen)
        {
            _search = "";
            _filtered = CountryCatalog.All.ToList();
        }
    }

    private void OnSearchInput(ChangeEventArgs e)
    {
        _search = e.Value?.ToString() ?? "";
        _filtered = CountryCatalog.Search(_search);
    }

    private async Task SelectCountryAsync(CountryInfo country)
    {
        _isOpen = false;
        Value = country.Code;
        await ValueChanged.InvokeAsync(country.Code);
    }

    private async Task HandleFocusOutAsync()
    {
        // Give a click on a list item time to be dispatched before we collapse the panel.
        await Task.Delay(180);
        _isOpen = false;
        StateHasChanged();
    }
}

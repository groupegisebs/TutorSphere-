using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using TutorSphere.Web.Common;

namespace TutorSphere.Web.Components.Shared;

/// <summary>
/// Searchable country picker (flag + name), backed by <see cref="CountryCatalog"/>.
/// Requires a live circuit (InteractiveServer) — custom dropdown, not a native &lt;select&gt;.
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
    private ElementReference _searchInput;
    private bool _focusSearchPending;

    private CountryInfo? SelectedCountry => CountryCatalog.Find(Value);

    private void ToggleDropdown()
    {
        if (Disabled) return;
        if (_isOpen)
        {
            CloseDropdown();
            return;
        }

        _isOpen = true;
        _search = "";
        _filtered = CountryCatalog.All.ToList();
        _focusSearchPending = true;
    }

    private void CloseDropdown()
    {
        _isOpen = false;
        _search = "";
    }

    private void OnSearchInput(ChangeEventArgs e)
    {
        _search = e.Value?.ToString() ?? "";
        _filtered = CountryCatalog.Search(_search);
    }

    private async Task SelectCountryAsync(CountryInfo country)
    {
        Value = country.Code;
        CloseDropdown();
        await ValueChanged.InvokeAsync(country.Code);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusSearchPending || !_isOpen)
            return;

        _focusSearchPending = false;
        try
        {
            await _searchInput.FocusAsync();
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone — ignore.
        }
    }
}

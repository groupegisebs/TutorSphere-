using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TutorSphere.Web.Common;

namespace TutorSphere.Web.Components.Shared;

/// <summary>
/// Sélecteur de plusieurs pays (drapeau + nom), adossé à <see cref="CountryCatalog"/>.
/// Même habillage que <see cref="CountrySelect"/>, mais le panneau reste ouvert après un choix et
/// les pays retenus s'affichent en pastilles sous le champ : une offre valable dans huit pays doit
/// se relire d'un coup d'œil.
/// Nécessite un circuit interactif (InteractiveServer) — liste personnalisée, pas un &lt;select&gt;.
/// </summary>
public partial class CountryMultiSelect : ComponentBase
{
    [Parameter] public IReadOnlyList<string>? Values { get; set; }
    [Parameter] public EventCallback<IReadOnlyList<string>> ValuesChanged { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string Placeholder { get; set; } = "Sélectionner un ou plusieurs pays…";
    [Parameter] public string SearchPlaceholder { get; set; } = "Rechercher un pays ou un code…";
    [Parameter] public string EmptyText { get; set; } = "Aucun pays trouvé.";

    private bool _isOpen;
    private string _search = "";
    private List<CountryInfo> _filtered = CountryCatalog.All.ToList();
    private ElementReference _searchInput;
    private bool _focusSearchPending;

    private IReadOnlyList<string> Codes => Values ?? [];

    /// <summary>Pays retenus, dans l'ordre de sélection ; les codes inconnus sont ignorés.</summary>
    private List<CountryInfo> Selected =>
        [.. Codes.Select(CountryCatalog.Find).Where(c => c is not null).Select(c => c!)];

    private string SummaryText
    {
        get
        {
            var selected = Selected;
            return selected.Count switch
            {
                0 => Placeholder,
                1 => selected[0].Name,
                2 => $"{selected[0].Name}, {selected[1].Name}",
                _ => $"{selected[0].Name}, {selected[1].Name} +{selected.Count - 2}"
            };
        }
    }

    private bool IsPicked(string code) =>
        Codes.Any(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase));

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

    private async Task ToggleCountryAsync(CountryInfo country)
    {
        if (Disabled) return;

        var next = Codes.ToList();
        var existing = next.FindIndex(c => string.Equals(c, country.Code, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
            next.RemoveAt(existing);
        else
            next.Add(country.Code);

        Values = next;
        await ValuesChanged.InvokeAsync(next);
    }

    private async Task ClearAsync()
    {
        Values = [];
        CloseDropdown();
        await ValuesChanged.InvokeAsync([]);
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

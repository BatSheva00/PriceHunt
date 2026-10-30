namespace PriceHunt.Api.Suppliers;

/// <summary>
/// The seven simulated suppliers. Delays stay inside 0.5-5s so a supplier that answers always
/// beats the 6s deadline, while the one that never answers is cut by it.
/// </summary>
public sealed class SupplierCatalog
{
    private static readonly SupplierProfile[] Profiles =
    {
        new("SwiftShip",      500, 1000, 35, 1.35, 0.00, false),
        new("BudgetBox",      800, 1600, 22, 1.05, 0.00, false),
        new("GlobalExpress", 1500, 2500, 48, 1.90, 0.00, false),
        new("EcoFreight",    2000, 3500, 28, 1.15, 0.00, false),
        new("CargoPro",      3000, 4500, 40, 1.60, 0.00, false),
        new("FlakyFreight",  1000, 4000, 30, 1.25, 0.30, false),
        new("SilentLine",       0,    0, 26, 1.10, 0.00, true)
    };

    private static readonly IReadOnlyList<string> SupplierNames =
        Profiles.Select(profile => profile.Name).ToArray();

    private readonly Dictionary<string, ISupplier> _byName;

    public SupplierCatalog()
    {
        _byName = Profiles
            .Select(profile => (ISupplier)new SimulatedSupplier(profile))
            .ToDictionary(supplier => supplier.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> Names => SupplierNames;

    /// <summary>
    /// Resolves the requested names, keeping the catalog order and ignoring duplicates: the same
    /// supplier may only answer once per run.
    /// </summary>
    public IReadOnlyList<ISupplier> Resolve(IEnumerable<string> names)
    {
        var requested = new HashSet<string>(names.Select(name => name.Trim()), StringComparer.OrdinalIgnoreCase);

        return SupplierNames
            .Where(requested.Contains)
            .Select(name => _byName[name])
            .ToArray();
    }
}

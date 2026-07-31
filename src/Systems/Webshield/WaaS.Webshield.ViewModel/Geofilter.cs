using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace WaaS.Webshield.ViewModel;

/// <summary>Geofilter configuration for a URI config.</summary>
public class Geofilter
{
    /// <summary>Whether to allow or deny the specified countries.</summary>
    [Required]
    [DefaultValue(GeofilterType.Allow)]
    public required GeofilterType Type { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country codes to filter.</summary>
    /// <example>["de", "gb", "us"]</example>
    [Required]
    public required HashSet<string> Countries { get; set; }
}

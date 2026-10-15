using System.ComponentModel.DataAnnotations;

namespace WaaS.Space.Stretch.ViewModel;

public class DomainBinding : Space.ViewModel.DomainBinding
{
    /// <summary>
    /// The container environment to use for this domain.
    /// </summary>
    [Required]
    public Environment Environment { get; set; } = new();

    /// <summary>
    /// The target directory the domain maps to.
    /// </summary>
    [Required]
    public TargetDirectory TargetPath { get; set; } = new();
}
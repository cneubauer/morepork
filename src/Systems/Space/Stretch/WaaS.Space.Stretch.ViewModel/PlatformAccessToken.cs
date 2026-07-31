using System.ComponentModel.DataAnnotations;

namespace WaaS.Space.Stretch.ViewModel;

public class PlatformAccessToken
{
    /// <summary>
    /// The hex-encoded public key in lowercase.
    /// </summary>
    /// <example>a3f1b2c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2</example>
    [StringLength(64, MinimumLength = 64)]
    [RegularExpression(@"^[a-f0-9]+$")]
    [Required]
    public required string Pubkey { get; set; }

    /// <summary>
    /// The permission scope for this access token.
    /// </summary>
    [Required]
    public Scope? Scope { get; set; }
}

public class Scope
{
    /// <summary>
    /// 0 => validation nothing else allowed in scope
    /// </summary>
    /// <example>0</example>
    // [Options(0)]
    [Required]
    public required int Version { get; set; }
}

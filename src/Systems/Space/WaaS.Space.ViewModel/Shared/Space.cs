using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using WaaS.Common.ViewModel;

namespace WaaS.Space.ViewModel;

public class Space : ITemporary//, IValidatableObject
{
    /// <summary>
    /// The unique identifier of the space.
    /// </summary>
    /// <example>500123456</example>
    public ulong? SystemInstanceId { get; set; }

    #region Data Access

    /// <summary>
    /// Use DataAccessDomains instead.
    /// </summary>
    [Obsolete("Use DataAccessDomains property instead")]
    public DataAccess? DataAccess { get; set; }

    /// <summary>
    /// A list of data access domains.
    /// </summary>
    [ReadOnly(true)]
    public ICollection<DataAccess>? DataAccessDomains { get; set; }

    #endregion

    /// <summary>
    /// Desired resource limits for this webspace.
    /// </summary>
    [Required]
    public Limits Limits { get; set; } = new Limits();

    /// <summary>
    /// Mail relay configuration for outgoing emails.
    /// </summary>
    public MailConfiguration? MailConfiguration { get; set; }

    /// <summary>
    /// The Linux user/group ownership assigned to this webspace by the backend.
    /// </summary>
    public SpaceOwner? Owner { get; set; }

    /// <summary>
    /// Locks designed for tenant use cases. Platform admins can set and remove tenant locks as well.
    /// </summary>
    public List<LockInfo>? TenantLocks { get; set; }

    /// <summary>
    /// Locks which can be set and removed only by platform admins. For a tenant this collection is read-only.
    /// </summary>
    public List<LockInfo>? AdminLocks { get; set; }

    /// <summary>
    /// Locks managed by the underlying system. Technical locks are mostly set and removed automatically. Best example is a lock due to over quota. For a tenant this collection is read-only.
    /// </summary>
    public List<LockInfo>? TechnicalLocks { get; set; }

    /// <summary>
    /// Configuration for temporary resources. One expiration property is allowed only: either ExpireAt or ExpireIn.
    /// </summary>
    public TemporaryInfo? Temporary { get; set; }

    /// <summary>
    /// The actual state of the webspace as reported by the backend. May differ from the desired state during provisioning.
    /// </summary>
    [ReadOnly(true)]
    public ActualState? ActualState { get; set; }

    /// <summary>
    /// Web analytics access credentials.
    /// </summary>
    public WebAnalytics? WebAnalytics { get; set; }

    /// <summary>
    /// Desired placement tags to influence server selection.
    /// </summary>
    public List<string>? PlacementTags { get; set; }

    /// <summary>
    /// A list of placement tags set by a platform admin. Admin tags always overwrite any tenant tags.
    /// </summary>
    public List<string>? AdminPlacementTags { get; set; }

    /// <summary>
    /// Whether the Biofilter (content scan) is enabled for this webspace.
    /// </summary>
    public bool? BiofilterEnabled { get; set; }

    // https://hosting-jira.1and1.org/browse/GPHWAAS-7935
    // Defines a default BiofilterEnabled value, necessary for proper validation. 
    // Especially important for creation of StretchSpaces, as there must be an initial biofilter mode if tenant does not want or is not allowed to specify one himself. 
    // Adding this default directly in BiofilterEnabled property, leads to various problems on validation. 
    public static readonly bool DefaultBiofilterMode = true;

    // public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    // {
    //     // Check domain bindings for temporary webspaces. They are not allowed
    //     if (Expires != null && DomainBindings != null && DomainBindings.Count != 0)
    //     {
    //         yield return new ValidationResult(
    //             $"Customer domains are not allowed for temporary webspaces",
    //             [nameof(Expires), nameof(DomainBindings)]
    //         );
    //     }

    //     // Check for duplicate domains in list
    //     if (DomainBindings != null && DomainBindings.Count != 0)
    //     {
    //         var duplicateDomains = DomainBindings
    //             .GroupBy(x => x.Domain)
    //             .Where(x => x.Count() > 1)
    //             .Select(x => x.Key)
    //             ;

    //         if (duplicateDomains.Any())
    //             yield return new ValidationResult(
    //                 $"Domains cannot be bound more than once in a single webspace. Duplicate bindings: {string.Join(", ", duplicateDomains)}",
    //                 [nameof(DomainBindings)]
    //             );
    //     }

    //     // Check for duplicate accounts in list
    //     if (Accounts != null && Accounts.Count != 0)
    //     {
    //         var duplicateAccounts = Accounts
    //             .Where(x => x.Id != null)
    //             .GroupBy(x => x.Id)
    //             .Where(x => x.Count() > 1)
    //             .Select(x => x.Key)
    //             ;

    //         if (duplicateAccounts.Any())
    //             yield return new ValidationResult(
    //                 $"Duplicate accounts detected: {string.Join(", ", duplicateAccounts)}",
    //                 [nameof(Accounts)]
    //             );
    //     }

    //     // https://hosting-jira.1and1.org/browse/GPHWAAS-7932
    //     // TODO: Enable again, when tenants have adjusted
    //     // if (Expires != null && Expires.Value.ToUniversalTime() > DateTime.UtcNow.AddHours(4))
    //     //     yield return new ValidationResult(
    //     //         $"Temporary webspace must not life longer than 4h",
    //     //         new[] { nameof(Expires) }
    //     //     );
    // }
}

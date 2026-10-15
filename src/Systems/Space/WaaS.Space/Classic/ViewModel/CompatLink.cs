using System.ComponentModel.DataAnnotations;
using WaaS.Space.ViewModel;

namespace WaaS.Space.Classic.ViewModel;

public class CompatLink
{
   /// <summary>
   /// The filesystem path of the compatibility symlink.
   /// </summary>
   /// <example>/var/www/html</example>
   [Required]
   [UnixPath]
   public string? Path { get; set; }

   /// <summary>
   /// The target directory the symlink points to.
   /// </summary>
   [Required]
   public TargetDirectory? Target { get; set; } = new TargetDirectory();
}
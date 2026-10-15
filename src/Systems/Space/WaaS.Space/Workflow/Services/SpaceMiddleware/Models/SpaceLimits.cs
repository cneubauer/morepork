using System.Text.Json.Serialization;

namespace SpaceMiddleware;

public class SpaceLimits
{
    [JsonPropertyName("disk_quota")]
    public string? DiskQuota { get; set; }

    [JsonIgnore]
    public ulong? DiskQuotaInBytes
    {
        get
        {
            if (string.IsNullOrEmpty(DiskQuota))
                return 0;

            try
            {
                var type = DiskQuota[DiskQuota.Length - 1];
                if (char.IsDigit(type))
                    return ulong.Parse(DiskQuota);

                var value = ulong.Parse(DiskQuota.Substring(0, DiskQuota.Length - 1));

                ulong factor = 1024;
                if (type == 'K' || type == 'M' || type == 'G' || type == 'T' || type == 'P')
                    factor = 1000;

                switch (type)
                {
                    case 'b':
                    case 'B':
                        return value;
                    case 'k':
                    case 'K':
                        return value * factor;
                    case 'm':
                    case 'M':
                        return value * factor * factor;
                    case 'g':
                    case 'G':
                        return value * factor * factor * factor;
                    case 't':
                    case 'T':
                        return value * factor * factor * factor * factor;
                    case 'p':
                    case 'P':
                        return value * factor * factor * factor * factor * factor;
                    default:
                        throw new InvalidCastException($"Cannot parse DiskQuota value {DiskQuota}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidCastException($"Cannot parse DiskQuota value {DiskQuota}", ex);
            }
        }

        set => DiskQuota = value.ToString() + "b";
    }

    [JsonPropertyName("inode_quota")]
    public int? InodeQuota { get; set; }

    [JsonPropertyName("resource_level")]
    public string? ResourceLevel { get; set; }
}

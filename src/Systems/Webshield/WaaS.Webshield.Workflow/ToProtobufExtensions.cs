using System.Text;
using WaaS.Webshield.DesiredState;
using WaaS.Webshield.ProtoBuf;

namespace WaaS.Webshield.Workflow;

public static class ToProtobufExtensions
{
    public static DesiredStateProxy ToProtobuf(
        this WebshieldData data,
        ulong stackInstanceId,
        StateHeader.Zone zone,
        ulong stateVersion,
        bool isTombstone,
        string tenantName,
        string? referenceId = null,
        DesiredState.WebshieldType? webshieldTypeFilter = null,
        IReadOnlyDictionary<string, (string waId, string encryptedWaToken)>? resolvedAnalytics = null)
    {
        var header = new StateHeader
        {
            stackInstanceId = stackInstanceId,
            stateNamespace = StateHeader.Namespace.PROXY,
            stateZone = zone,
            stateVersion = stateVersion,
            isTombstone = isTombstone,
            tenantName = tenantName,
        };

        if (referenceId is not null)
            header.tags.Add(referenceId);

        var proto = new DesiredStateProxy { header = header };

        var mappings = webshieldTypeFilter.HasValue
            ? data.Mappings.Where(x => x.WebshieldType == webshieldTypeFilter.Value && x.IsEnabled)
            : data.Mappings.Where(x => x.IsEnabled);

        foreach (var mapping in mappings)
            proto.mappings.Add(mapping.ToProtobuf(stackInstanceId, resolvedAnalytics));

        foreach (var cert in data.Certificates)
            proto.certificates.Add(cert.ToProtobuf());

        return proto;
    }

    public static ProtoBuf.Mapping ToProtobuf(
        this DesiredState.Mapping mapping,
        ulong stackInstanceId,
        IReadOnlyDictionary<string, (string waId, string encryptedWaToken)>? resolvedAnalytics)
    {
        var proto = new ProtoBuf.Mapping
        {
            hostname = mapping.Domain,
            destination = mapping.Destination,
            mode = (ProtoBuf.Mapping.ModeType)mapping.Mode,
            stackInstanceId = stackInstanceId,
            sslCertificateId = mapping.SslCertificateId,
            forceSsl = mapping.Mode == DesiredState.ModeType.ProxyForceSsl,
        };

        foreach (var uriConfig in mapping.UriConfigs)
            proto.uriConfigs.Add(uriConfig.ToProtobuf());

        if (mapping.WebAnalytics is not null)
            proto.webAnalytics = mapping.WebAnalytics.ToProtobuf(resolvedAnalytics);

        return proto;
    }

    public static SslCertificate ToProtobuf(this DesiredState.Certificate cert)
    {
        var proto = new SslCertificate
        {
            certificateId = cert.CertificateId,
            certificate = Encoding.UTF8.GetBytes(cert.CertificateData),
        };

        if (cert.PrivateKey is not null)
            proto.privateKey = Encoding.UTF8.GetBytes(cert.PrivateKey);

        foreach (var chain in cert.CertificateChain)
            proto.certificateChain.Add(Encoding.UTF8.GetBytes(chain));

        if (cert.EncryptedPrivateKey is not null)
            proto.encryptedPrivateKey = cert.EncryptedPrivateKey.FromProtoBuf<EncryptedContainer>();

        if (cert.OcspStapling is not null)
            proto.ocspStapling = cert.OcspStapling;

        return proto;
    }

    public static ProtoBuf.UriConfig ToProtobuf(this DesiredState.UriConfig uriConfig)
    {
        var proto = new ProtoBuf.UriConfig
        {
            match = new ProtoBuf.UriMatch
            {
                type = (ProtoBuf.UriMatch.UriMatchType)uriConfig.Match.Type,
                protocol = (ProtoBuf.UriMatch.UriProtocol)uriConfig.Match.Protocol,
                prefix = uriConfig.Match.Prefix,
            },
            destination = new ProtoBuf.Destination
            {
                type = (Destination.DestinationType)uriConfig.Destination.Type,
                target = uriConfig.Destination.Target,
                sniMode = (Destination.SniMode)uriConfig.Destination.SniMode,
            },
        };

        if (uriConfig.Waf is not null)
            proto.waf = uriConfig.Waf.ToProtobuf();

        if (uriConfig.Cache is not null)
            proto.cache = uriConfig.Cache.ToProtobuf();

        return proto;
    }

    public static ProtoBuf.WafConfig ToProtobuf(this DesiredState.WafConfig waf)
    {
        var proto = new ProtoBuf.WafConfig
        {
            ruleset = (ProtoBuf.WafConfig.WafRuleset)waf.Ruleset,
            ratelimit = waf.Ratelimit,
        };

        if (waf.Geofilter is not null)
        {
            proto.geofilter = new Geofilter { type = (Geofilter.FilterType)waf.Geofilter.Type };
            foreach (var country in waf.Geofilter.Countries)
                proto.geofilter.countries.Add(country);
        }

        return proto;
    }

    public static ProtoBuf.CacheConfig ToProtobuf(this DesiredState.CacheConfig cache)
        => new()
        {
            policy = (ProtoBuf.CacheConfig.CachePolicy)cache.Policy,
            salt = cache.Salt ?? "",
        };

    public static WebshieldAnalytics ToProtobuf(
        this AnalyticsRef analytics,
        IReadOnlyDictionary<string, (string waId, string encryptedWaToken)>? resolvedAnalytics)
    {
        var proto = new WebshieldAnalytics();

        var key = analytics.WaProfileId ?? analytics.ExternalReference;

        if (key is not null && resolvedAnalytics?.TryGetValue(key, out var resolved) == true)
        {
            proto.waId = resolved.waId;
            proto.encryptedWaToken = resolved.encryptedWaToken;
        }

        return proto;
    }
}

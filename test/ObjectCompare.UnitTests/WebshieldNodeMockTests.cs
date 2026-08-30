using WaaS.Webshield.ProtoBuf;

namespace ObjectCompare.UnitTests;

public class WebshieldNodeMockTests
{
    [Fact]
    public void DesiredStateProxy_To_ActualStateProxy_RoundTrip_MatchesExpectedHeaderAndNode()
    {
        var header = new StateHeader
        {
            stackInstanceId = 1234567,
            stateNamespace = StateHeader.Namespace.PROXY,
            stateZone = StateHeader.Zone.DE,
            stateVersion = 42,
            isTombstone = false,
            tenantName = "demo",
        };
        header.tags.Add("waas-tx-123e4567-e89b-12d3-a456-426614174000");

        var desiredState = new DesiredStateProxy
        {
            header = header
        };
        desiredState.mappings.Add(new Mapping
        {
            hostname = "example.com",
            destination = "some-infong.schlund.de",
            mode = Mapping.ModeType.PROXY,
            stackInstanceId = 1234567,
        });

        // 1. Serialize DesiredState
        var desiredBytes = desiredState.ToProtoBuf();
        Assert.NotNull(desiredBytes);
        Assert.NotEmpty(desiredBytes);

        // 2. Deserialize DesiredState in Mock
        var receivedDesiredState = desiredBytes.FromProtoBuf<DesiredStateProxy>();
        Assert.NotNull(receivedDesiredState.header);
        Assert.Equal(1234567UL, receivedDesiredState.header.stackInstanceId);
        Assert.Equal(StateHeader.Zone.DE, receivedDesiredState.header.stateZone);
        Assert.Equal("demo", receivedDesiredState.header.tenantName);
        Assert.Equal("waas-tx-123e4567-e89b-12d3-a456-426614174000", receivedDesiredState.header.tags[0]);

        // 3. Construct ActualState for configured node
        const string nodeFqdn = "some-de-webshield-node-1.server.lan";
        var actualState = new ActualStateProxy
        {
            header = receivedDesiredState.header,
            nodeFqdn = nodeFqdn
        };

        // 4. Serialize ActualState
        var actualBytes = actualState.ToProtoBuf();
        Assert.NotNull(actualBytes);
        Assert.NotEmpty(actualBytes);

        // 5. Deserialize ActualState as received by WebshieldActualStateListener
        var receivedActualState = actualBytes.FromProtoBuf<ActualStateProxy>();
        Assert.NotNull(receivedActualState.header);
        Assert.Equal(1234567UL, receivedActualState.header.stackInstanceId);
        Assert.Equal(nodeFqdn, receivedActualState.nodeFqdn);
        Assert.Null(receivedActualState.error);
    }

    [Theory]
    [InlineData(StateHeader.Zone.DE, "ActualState.Proxy.De")]
    [InlineData(StateHeader.Zone.US, "ActualState.Proxy.Us")]
    [InlineData(StateHeader.Zone.ES, "ActualState.Proxy.Es")]
    [InlineData(StateHeader.Zone.UK, "ActualState.Proxy.Uk")]
    [InlineData(StateHeader.Zone.MM, "ActualState.Proxy.Mm")]
    [InlineData(StateHeader.Zone.GLOBALCDN, "ActualState.Proxy.GlobalCdn")]
    public void RoutingKey_MatchesZoneConvention(StateHeader.Zone zone, string expectedRoutingKey)
    {
        var zoneStr = zone switch
        {
            StateHeader.Zone.DE => "De",
            StateHeader.Zone.US => "Us",
            StateHeader.Zone.ES => "Es",
            StateHeader.Zone.UK => "Uk",
            StateHeader.Zone.MM => "Mm",
            StateHeader.Zone.GLOBALCDN => "GlobalCdn",
            _ => "De"
        };

        var routingKey = $"ActualState.Proxy.{zoneStr}";
        Assert.Equal(expectedRoutingKey, routingKey);
    }

    [Fact]
    public void MultiNodeConfig_SplitsFqdnsCorrectly()
    {
        const string config = "some-de-webshield-node-1.server.lan, some-de-webshield-node-2.server.lan, ";
        var nodes = config.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(2, nodes.Length);
        Assert.Equal("some-de-webshield-node-1.server.lan", nodes[0]);
        Assert.Equal("some-de-webshield-node-2.server.lan", nodes[1]);
    }
}

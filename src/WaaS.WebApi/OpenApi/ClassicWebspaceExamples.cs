using System.Text.Json;
using System.Text.Json.Nodes;
using WaaS.Common.ViewModel;
using Vm = WaaS.Space.ViewModel;
using ClassicVm = WaaS.Space.Classic.ViewModel;

namespace WaaS.WebApi.OpenApi;

public static class ClassicWebspaceExamples
{
    /// <summary>
    /// Constructs a SharedWebspace view model that mirrors the exact seeded Desired State
    /// for tenant 'demo', stackInstanceId 1234567, and systemInstanceId 5001234567.
    /// </summary>
    public static ClassicVm.SharedWebspace CreateSeededDesiredStateExample()
    {
        return new ClassicVm.SharedWebspace
        {
            SystemInstanceId = 5001234567,
            Data = new ClassicVm.SharedWebspaceData
            {
                Platform = Vm.PlatformType.Linux,
                WebspaceId = 43210001,
                Hostname = "some-infong.schlund.de",
                Ipv4 = "123.123.123.123",
                Ipv6 = "aa42:bb42:cc42:42:123:123:123:123",
                RegionName = "europe",
                PlacementTags = ["shl:standard"],
                Limits = new Vm.SpaceData.ActualWebspaceLimits
                {
                    DiskQuotaInBytes = 5000000000
                }
            },
            Limits = new Vm.Limits
            {
                DiskQuota = 5000000000,
                ResourceLevel = "M"
            },
            Owner = new Vm.SpaceOwner
            {
                Uid = 654321,
                Gid = 600,
                Username = "ws654321",
                Groupname = "ftpusers"
            },
            Accounts =
            [
                new Vm.Account
                {
                    AccountId = 5432101,
                    Id = "5c9392216d3e486f956b8e7b079f2c36",
                    Username = "a5432101",
                    PasswordToken = "03axxx755ddfab6b8b0dc5e005926a99",
                    AccessTypes = Vm.SpaceAccessType.Sftp,
                    AccountType = "standard",
                    HomeDirPubKeys = true
                },
                new Vm.Account
                {
                    AccountId = 5432102,
                    Id = "bd075fa6bdb8434d99dcb6b5e7acd570",
                    Username = "a5432102",
                    PasswordToken = "818xxxfcbbaa449f99dd9dc81ecc55cd",
                    AccessTypes = Vm.SpaceAccessType.Sftp | Vm.SpaceAccessType.Ssh,
                    AccountType = "standard",
                    HomeDirPubKeys = true
                }
            ],
            Domains =
            [
                new Vm.DomainBinding
                {
                    DomainId = 1230001,
                    Domain = "foo.de",
                    IsEnabled = true
                },
                new Vm.DomainBinding
                {
                    DomainId = 1230002,
                    Domain = "www.foo.de",
                    IsEnabled = true
                }
            ],
            ManagedDomainBindings =
            [
                new Vm.DomainBinding
                {
                    DomainId = 67890001,
                    Domain = "home-5004265496.some-product-domain.de",
                    IsEnabled = true
                }
            ],
            MailConfiguration = new Vm.MailConfiguration
            {
                Host = "some-mail-host.de",
                HostPort = 25,
                Username = "some-mail-user",
                PasswordToken = "ca6xxx3feb5842baaad3fae7123428f",
                DefaultSender = "some-mail@domain.de",
                DefaultEnvelopeFromPolicy = "default_sender"
            },
            PlacementTags = ["shl:standard"],
            BiofilterEnabled = true
        };
    }

    public static JsonNode ToJsonNode(JsonSerializerOptions serializerOptions)
    {
        return JsonSerializer.SerializeToNode(CreateSeededDesiredStateExample(), serializerOptions)!;
    }
}

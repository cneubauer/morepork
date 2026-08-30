using WaaS.Common.DesiredState;
using WaaS.Common.ViewModel;
using WaaS.Space.Classic.DesiredState;
using WaaS.Space.DesiredState;
using WaaS.WebApi;
using WaaS.WebApi.OpenApi;
using Xunit;
using ClassicVm = WaaS.Space.Classic.ViewModel;
using Vm = WaaS.Space.ViewModel;

namespace ObjectCompare.UnitTests;

public class DesiredStateExtensionsTests
{
    [Fact]
    public void Apply_SeededExample_AppliesAllPropertiesToDesiredState()
    {
        // Arrange
        var viewModel = ClassicWebspaceExamples.CreateSeededDesiredStateExample();
        var desiredState = new SharedWebspace();

        // Act
        desiredState.Apply(viewModel);

        // Assert
        Assert.Equal(43210001UL, desiredState.WebspaceId);
        Assert.Equal("some-infong.schlund.de", desiredState.Hostname);
        Assert.NotNull(desiredState.IpSet);
        Assert.Equal("123.123.123.123", desiredState.IpSet.IPv4);
        Assert.Equal("aa42:bb42:cc42:42:123:123:123:123", desiredState.IpSet.IPv6);
        Assert.Equal("europe", desiredState.Region);
        Assert.Equal(WaaS.Space.DesiredState.Platform.Linux, desiredState.Platform);

        // Limits
        Assert.NotNull(desiredState.Limits);
        Assert.Equal(5000000000UL, desiredState.Limits.DiskQuota);
        Assert.Equal("M", desiredState.Limits.ResourceLevel);

        // Owner
        Assert.NotNull(desiredState.Owner);
        Assert.Equal(654321, desiredState.Owner.Uid);
        Assert.Equal(600, desiredState.Owner.Gid);
        Assert.Equal("ws654321", desiredState.Owner.Username);
        Assert.Equal("ftpusers", desiredState.Owner.Groupname);

        // MailConfiguration
        Assert.NotNull(desiredState.MailConfiguration);
        Assert.Equal("some-mail-host.de", desiredState.MailConfiguration.Host);
        Assert.Equal(25u, desiredState.MailConfiguration.Hostport);
        Assert.Equal("some-mail-user", desiredState.MailConfiguration.Username);
        Assert.Equal("ca6xxx3feb5842baaad3fae7123428f", desiredState.MailConfiguration.SecurePasswordToken);
        Assert.Equal("some-mail@domain.de", desiredState.MailConfiguration.DefaultSender);
        Assert.Equal("default_sender", desiredState.MailConfiguration.DefaultEnvelopeFromPolicy);

        // Biofilter and PlacementTags
        Assert.True(desiredState.BiofilterEnabled);
        Assert.Single(desiredState.PlacementTags);
        Assert.Equal("shl:standard", desiredState.PlacementTags[0]);

        // Accounts
        Assert.Equal(2, desiredState.Accounts.Count);
        var acc1 = desiredState.Accounts.First(a => a.AccountId == 5432101);
        Assert.Equal("5c9392216d3e486f956b8e7b079f2c36", acc1.ReferenceId);
        Assert.Equal("a5432101", acc1.Username);
        Assert.Equal("03axxx755ddfab6b8b0dc5e005926a99", acc1.SecurePasswordToken);
        Assert.Equal("sftp", acc1.AccessType);
        Assert.True(acc1.HomeDirPubKeys);

        var acc2 = desiredState.Accounts.First(a => a.AccountId == 5432102);
        Assert.Equal("bd075fa6bdb8434d99dcb6b5e7acd570", acc2.ReferenceId);
        Assert.Equal("a5432102", acc2.Username);
        Assert.Equal("818xxxfcbbaa449f99dd9dc81ecc55cd", acc2.SecurePasswordToken);
        Assert.Equal("sftp,ssh", acc2.AccessType);

        // Domains
        Assert.Equal(2, desiredState.Domains.Count);
        Assert.Contains(desiredState.Domains, d => d.DomainId == 1230001 && d.DomainName == "foo.de" && (d.IsEnabled ?? false));
        Assert.Contains(desiredState.Domains, d => d.DomainId == 1230002 && d.DomainName == "www.foo.de" && (d.IsEnabled ?? false));

        // Managed HTTP Access Domains
        Assert.Single(desiredState.HttpAccessDomains);
        var httpDomain = desiredState.HttpAccessDomains[0];
        Assert.Equal(67890001UL, httpDomain.DomainId);
        Assert.Equal("home-5004265496.some-product-domain.de", httpDomain.DomainName);
        Assert.True(httpDomain.IsEnabled);
    }

    [Fact]
    public void Apply_UpdatesExistingCollectionsAndProperties()
    {
        // Arrange
        var desiredState = new SharedWebspace
        {
            WebspaceId = 100,
            Accounts =
            [
                new WaaS.Space.DesiredState.Account
                {
                    ReferenceId = "acc-1",
                    Username = "u100",
                    AccountId = 1,
                    AccessType = "sftp",
                    SecurePasswordToken = "old-token"
                }
            ],
            Domains =
            [
                new WaaS.Space.DesiredState.DomainBinding<string>
                {
                    DomainId = 50,
                    DomainName = "old.com",
                    IsEnabled = true
                }
            ],
            CronTabs =
            [
                new WaaS.Space.DesiredState.CronTab { Command = "echo 1", Schedule = "* * * * *" }
            ]
        };

        var viewModel = new ClassicVm.SharedWebspace
        {
            Accounts =
            [
                new Vm.Account
                {
                    Id = "acc-1",
                    Username = "u100",
                    PasswordToken = "new-token",
                    AccessTypes = Vm.SpaceAccessType.Sftp | Vm.SpaceAccessType.Ssh,
                    HomeDirPubKeys = true,
                },
                new Vm.Account
                {
                    Id = "acc-2",
                    Username = "u200",
                    AccessTypes = Vm.SpaceAccessType.Sftp,
                    HomeDirPubKeys = false,
                }
            ],
            Domains =
            [
                new ClassicVm.DomainBinding
                {
                    DomainId = 50,
                    Domain = "old.com",
                    IsEnabled = false,
                    Environment = "php8.5",
                },
                new ClassicVm.DomainBinding
                {
                    DomainId = 51,
                    Domain = "new.com",
                    IsEnabled = true,
                    Environment = "php8.5",
                }
            ],
            CronTabs =
            [
                new Vm.CronTab
                {
                    Command = "php /cron.php",
                    Schedule = "0 0 * * *",
                    Comment = "Daily cron"
                }
            ],
            TenantLocks =
            [
                new LockInfo
                {
                    Id = "lock-1",
                    Reason = "Payment overdue",
                    Category = WaaS.Common.ViewModel.LockCategory.Abuse
                }
            ]
        };

        // Act
        desiredState.Apply(viewModel);

        // Assert
        // Accounts: existing acc-1 updated, acc-2 added
        Assert.Equal(2, desiredState.Accounts.Count);
        var acc1 = desiredState.Accounts.First(a => a.ReferenceId == "acc-1");
        Assert.Equal(1UL, acc1.AccountId); // preserved existing AccountId
        Assert.Equal("new-token", acc1.SecurePasswordToken);
        Assert.Equal("sftp,ssh", acc1.AccessType);

        var acc2 = desiredState.Accounts.First(a => a.ReferenceId == "acc-2");
        Assert.Equal("u200", acc2.Username);
        Assert.False(acc2.HomeDirPubKeys);

        // Domains: old.com updated to IsEnabled=false, new.com added
        Assert.Equal(2, desiredState.Domains.Count);
        var oldDomain = desiredState.Domains.First(d => d.DomainId == 50);
        Assert.False(oldDomain.IsEnabled);
        var newDomain = desiredState.Domains.First(d => d.DomainId == 51);
        Assert.True(newDomain.IsEnabled);

        // CronTabs: replaced
        Assert.Single(desiredState.CronTabs);
        Assert.Equal("php /cron.php", desiredState.CronTabs[0].Command);
        Assert.Equal("0 0 * * *", desiredState.CronTabs[0].Schedule);
        Assert.Equal("Daily cron", desiredState.CronTabs[0].Comment);

        // TenantLocks
        Assert.Single(desiredState.LockItems);
        Assert.Equal("lock-1", desiredState.LockItems[0].Id);
        Assert.Equal(LockItemType.Tenant, desiredState.LockItems[0].LockType);
        Assert.Equal(WaaS.Common.DesiredState.LockCategory.Abuse, desiredState.LockItems[0].Category);
        Assert.True(desiredState.LockItems[0].RemovableByTenant);
    }
}

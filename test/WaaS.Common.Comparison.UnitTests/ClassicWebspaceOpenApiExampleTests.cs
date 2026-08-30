using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using WaaS.WebApi.OpenApi;
using ClassicVm = WaaS.Space.Classic.ViewModel;
using Vm = WaaS.Space.ViewModel;

namespace WaaS.Common.Comparison.UnitTests;

public class ClassicWebspaceOpenApiExampleTests
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public void CreateSeededDesiredStateExample_UsesOnlyPasswordTokens_AndNoPlaintextPasswords()
    {
        var example = ClassicWebspaceExamples.CreateSeededDesiredStateExample();

        Assert.NotNull(example.Accounts);
        Assert.NotEmpty(example.Accounts);
        foreach (var account in example.Accounts)
        {
            Assert.Null(account.Password);
            Assert.NotNull(account.PasswordToken);
        }

        Assert.NotNull(example.MailConfiguration);
        Assert.Null(example.MailConfiguration.Password);
        Assert.NotNull(example.MailConfiguration.PasswordToken);
    }

    [Fact]
    public void CreateSeededDesiredStateExample_MatchesSeededData()
    {
        var example = ClassicWebspaceExamples.CreateSeededDesiredStateExample();

        Assert.Equal(5001234567UL, example.SystemInstanceId);
        Assert.Equal(43210001UL, example.Data.WebspaceId);
        Assert.Equal("some-infong.schlund.de", example.Data.Hostname);
        Assert.Equal("123.123.123.123", example.Data.Ipv4);
        Assert.Equal("aa42:bb42:cc42:42:123:123:123:123", example.Data.Ipv6);
        Assert.Equal("europe", example.Data.RegionName);
        Assert.Equal(5000000000UL, example.Limits.DiskQuota);
        Assert.Equal("M", example.Limits.ResourceLevel);
        Assert.Equal("ws654321", example.Owner?.Username);
        Assert.Equal(654321, example.Owner?.Uid);
        Assert.Equal(600, example.Owner?.Gid);

        Assert.Equal(2, example.Domains?.Count);
        Assert.Contains(example.Domains!, d => d.Domain == "foo.de" && d.DomainId == 1230001UL);
        Assert.Contains(example.Domains!, d => d.Domain == "www.foo.de" && d.DomainId == 1230002UL);

        Assert.Single(example.ManagedDomainBindings!);
        Assert.Equal("home-5004265496.some-product-domain.de", example.ManagedDomainBindings![0].Domain);
        Assert.Equal(67890001UL, example.ManagedDomainBindings[0].DomainId);

        Assert.Equal(2, example.Accounts?.Count);
        Assert.Contains(example.Accounts!, a => a.Username == "a5432101" && a.PasswordToken == "03axxx755ddfab6b8b0dc5e005926a99" && a.AccessTypes == Vm.SpaceAccessType.Sftp);
        Assert.Contains(example.Accounts!, a => a.Username == "a5432102" && a.PasswordToken == "818xxxfcbbaa449f99dd9dc81ecc55cd" && a.AccessTypes == (Vm.SpaceAccessType.Sftp | Vm.SpaceAccessType.Ssh));

        Assert.Equal("some-mail-host.de", example.MailConfiguration?.Host);
        Assert.Equal(25u, example.MailConfiguration?.HostPort);
        Assert.Equal("some-mail-user", example.MailConfiguration?.Username);
        Assert.Equal("some-mail@domain.de", example.MailConfiguration?.DefaultSender);
        Assert.Equal("default_sender", example.MailConfiguration?.DefaultEnvelopeFromPolicy);
    }

    [Fact]
    public void ToJsonNode_ProducesValidJsonWithExpectedStructure()
    {
        var node = ClassicWebspaceExamples.ToJsonNode(_jsonSerializerOptions);
        var json = node.ToJsonString();

        Assert.Contains("\"systemInstanceId\":5001234567", json);
        Assert.Contains("\"webspaceId\":43210001", json);
        Assert.Contains("\"foo.de\"", json);
        Assert.Contains("\"www.foo.de\"", json);
        Assert.Contains("\"a5432101\"", json);
        Assert.Contains("\"03axxx755ddfab6b8b0dc5e005926a99\"", json);
        Assert.DoesNotContain("\"password\":", json);
    }

    [Fact]
    public async Task SharedWebspaceOperationTransformer_AttachesExample_WhenSharedWebspaceParameterPresent()
    {
        var transformer = new SharedWebspaceOperationTransformer(_jsonSerializerOptions);

        var operation = new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType()
                }
            }
        };

        var apiDescription = new ApiDescription();
        apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
        {
            Name = "webspace",
            Type = typeof(ClassicVm.SharedWebspace),
            Source = Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Body
        });

        var services = new ServiceCollection().BuildServiceProvider();
        var context = new OpenApiOperationTransformerContext
        {
            Description = apiDescription,
            DocumentName = "v1",
            ApplicationServices = services
        };

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        var mediaType = operation.RequestBody!.Content!["application/json"];
        Assert.NotNull(mediaType.Example);
        Assert.NotNull(mediaType.Examples);
        Assert.True(mediaType.Examples.ContainsKey("Seeded Desired State"));

        var json = mediaType.Example!.ToJsonString();
        Assert.Contains("\"systemInstanceId\":5001234567", json);
        Assert.Contains("\"webspaceId\":43210001", json);
    }
}

using ObjectCompare;
using WaaS.Space.DesiredState;

namespace ObjectCompare.UnitTests;

public class ChangeTypeResolverTests
{
    [Fact]
    public void Resolve_FullName_ReturnsType()
    {
        Assert.Equal(typeof(Account), ChangeTypeResolver.Resolve(typeof(Account).FullName));
    }

    [Fact]
    public void Resolve_GenericFullName_ReturnsClosedType()
    {
        Assert.Equal(
            typeof(DomainBinding<string>),
            ChangeTypeResolver.Resolve(typeof(DomainBinding<string>).FullName));
    }

    [Fact]
    public void Resolve_UnqualifiedName_ResolvesBySimpleName()
    {
        Assert.Equal(typeof(MailConfiguration), ChangeTypeResolver.Resolve("MailConfiguration"));
    }

    [Fact]
    public void Resolve_UnknownName_ReturnsNullWithoutThrowing()
    {
        Assert.Null(ChangeTypeResolver.Resolve("WaaS.Nope.DoesNotExist"));
        Assert.Null(ChangeTypeResolver.Resolve("DoesNotExistAnywhere"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Resolve_EmptyName_ReturnsNull(string? typeName)
    {
        Assert.Null(ChangeTypeResolver.Resolve(typeName));
    }
}

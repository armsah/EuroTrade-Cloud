using System.Reflection;

namespace EuroTrade.Architecture.Tests;

public sealed class DependencyRulesTests
{
    private static readonly Assembly ApiAssembly =
        typeof(Program).Assembly;

    private static readonly Assembly ApplicationAssembly =
        typeof(EuroTrade.Application.Orders.CreateOrderService).Assembly;

    private static readonly Assembly DomainAssembly =
        typeof(EuroTrade.Domain.Orders.Order).Assembly;

    private static readonly Assembly InfrastructureAssembly =
        typeof(EuroTrade.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_must_not_depend_on_application_infrastructure_or_api()
    {
        var references = GetReferencedAssemblyNames(DomainAssembly);

        Assert.DoesNotContain(
            references,
            name => name is
                "EuroTrade.Application" or
                "EuroTrade.Infrastructure" or
                "EuroTrade.Api");
    }

    [Fact]
    public void Application_must_not_depend_on_infrastructure_or_api()
    {
        var references = GetReferencedAssemblyNames(ApplicationAssembly);

        Assert.DoesNotContain(
            references,
            name => name is
                "EuroTrade.Infrastructure" or
                "EuroTrade.Api");
    }

    [Fact]
    public void Infrastructure_must_not_depend_on_api()
    {
        var references = GetReferencedAssemblyNames(InfrastructureAssembly);

        Assert.DoesNotContain(
            references,
            name => name == "EuroTrade.Api");
    }

    [Fact]
    public void Api_must_not_be_referenced_by_application_or_domain()
    {
        var applicationReferences =
            GetReferencedAssemblyNames(ApplicationAssembly);

        var domainReferences =
            GetReferencedAssemblyNames(DomainAssembly);

        Assert.DoesNotContain(
            "EuroTrade.Api",
            applicationReferences);

        Assert.DoesNotContain(
            "EuroTrade.Api",
            domainReferences);
    }

    private static HashSet<string> GetReferencedAssemblyNames(
        Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);
    }
}
namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Portal.Infrastructure.Persistence;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class ContextosDePersistenciaTests
{
    [Fact(DisplayName = "Os únicos DbContexts concretos são os do monólito e do Portal")]
    public void ContextosConcretos_SaoOsDoMonolitoEPortal() =>
        Classes().That().AreAssignableTo(typeof(DbContext)).And().AreNotAbstract().And().HaveFullNameStartingWith("Unifesspa.UniPlus.")
            .GetObjects(ArquiteturaDasTravas.Produto).Select(tipo => tipo.FullName)
            .Should().BeEquivalentTo([typeof(MonolitoDbContext).FullName, typeof(PortalDbContext).FullName]);
}

namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class AcervoPublicoCaminhoUnicoDeEscritaTests
{
    [Fact(DisplayName = "Só o serviço do acervo público copia para o bucket público")]
    public void SoOAcervoDeSelecaoUsaACopiaPublica()
    {
        MethodMember[] copia = [.. MethodMembers().That().AreDeclaredIn(typeof(IStorageService))
            .And().HaveNameStartingWith("CopiarComoObjetoPublicoAsync(").GetObjects(ArquiteturaDasTravas.Produto)];
        const string servico = "Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices.AcervoPublicoService";

        Types().That().HaveFullName(servico).And().CallAny(copia)
            .GetObjects(ArquiteturaDasTravas.Produto).Should().NotBeEmpty("a chamada permitida deve ser detectada");

        Types().That().HaveFullNameStartingWith("Unifesspa.UniPlus.")
            .And().DoNotHaveFullName(servico)
            .And().DoNotHaveFullNameStartingWith(servico + "+")
            .Should().NotCallAny(copia)
            .Check(ArquiteturaDasTravas.Produto);
    }
}

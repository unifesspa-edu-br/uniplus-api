namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Services;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class AvaliacaoDaInscricaoArquiteturaTests
{
    [Fact(DisplayName = "Só a avaliação da inscrição avalia as regras de comprovação")]
    public void SoAAvaliacaoDaInscricaoAvaliaAComprovacao()
    {
        IArchRule regra = Types()
            .That().HaveFullNameStartingWith("Unifesspa.UniPlus.")
            .And().DoNotHaveFullNameStartingWith("Unifesspa.UniPlus.Selecao.ArchTests")
            .And().DoNotHaveFullNameStartingWith("Unifesspa.UniPlus.ArchTests.")
            .And().AreNot(typeof(AvaliacaoDaInscricao))
            .And().AreNot(typeof(AvaliadorDaComprovacao))
            .Should()
            .NotDependOnAny(typeof(AvaliadorDaComprovacao))
            .Because("a avaliação da inscrição é a única composição da avaliação do formulário com a avaliação da comprovação (ADR-0143)");

        regra.Check(ArquiteturaDasTravas.Produto);
    }
}

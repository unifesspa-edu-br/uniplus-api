namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;

using Unifesspa.UniPlus.Selecao.Domain.Services;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

/// <summary>
/// As respostas e as modalidades em que o candidato concorre são dados sensíveis (ADR-0143).
/// </summary>
public sealed class AvaliacaoDaInscricaoArquiteturaTests
{
    private static readonly Architecture Arquitetura = ArquiteturaDaSelecao.Produto;

    [Fact(DisplayName = "Quem avalia as respostas da inscrição não registra log")]
    public void QuemAvaliaAsRespostasNaoRegistraLog()
    {
        IArchRule regra = Types()
            .That()
            .Are(
                typeof(AvaliacaoDaInscricao),
                typeof(Application.Services.PreparacaoDaAvaliacaoDaInscricao),
                typeof(Application.Queries.Inscricoes.AvaliarInscricaoQueryHandler))
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Microsoft\.Extensions\.Logging(\.|$)")
            .Because("as respostas e as modalidades em que o candidato concorre são dado sensível e não vão a log");

        regra.Check(Arquitetura);
    }
}

namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using Unifesspa.UniPlus.Selecao.Domain.Services;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// A avaliação da inscrição (ADR-0143): a árvore de exigências é resolvida sobre a avaliação do
/// formulário num lugar só, a <see cref="AvaliacaoDaInscricao"/> — outra composição do avaliador com
/// o resolvedor seria uma segunda conta do que se exige do candidato —; e quem avalia as respostas
/// não registra log, porque elas e as modalidades em que o candidato concorre são dado sensível.
/// </summary>
public sealed class AvaliacaoDaInscricaoArquiteturaTests
{
    private static readonly Architecture Arquitetura = new ArchLoader()
        .LoadAssemblies(
        [
            typeof(AvaliacaoDaInscricao).Assembly,
            typeof(Application.Services.PreparacaoDaAvaliacaoDaInscricao).Assembly,
            typeof(Infrastructure.Persistence.SelecaoDbContext).Assembly,
            typeof(API.Controllers.InscricoesController).Assembly,
        ])
        .Build();

    [Fact(DisplayName = "Só a avaliação da inscrição resolve a árvore de exigências")]
    public void SoAAvaliacaoDaInscricaoResolveAArvore()
    {
        IArchRule regra = Types()
            .That()
            .AreNot(typeof(AvaliacaoDaInscricao))
            .And()
            .AreNot(typeof(ResolvedorArvoreSatisfacao))
            .Should()
            .NotDependOnAny(typeof(ResolvedorArvoreSatisfacao))
            .Because("a avaliação da inscrição é a única composição do avaliador do formulário com o resolvedor da árvore (ADR-0143)");

        regra.Check(Arquitetura);
    }

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

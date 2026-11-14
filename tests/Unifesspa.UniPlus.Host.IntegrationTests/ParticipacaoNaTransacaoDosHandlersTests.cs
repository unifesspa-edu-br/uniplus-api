namespace Unifesspa.UniPlus.Host.IntegrationTests;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;
using Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;
using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Queries.CampoDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Wolverine.Runtime;
using Wolverine.Runtime.Handlers;

/// <summary>
/// O contexto de persistência é um só no monólito, então o detector de transação do Wolverine enrola o handler que lê
/// readers de outros módulos junto do unit of work. Os handlers abaixo só ficavam de fora por haver dois contextos; a
/// chain deles agora tem a transação ambiente (ADR-0004), como a de todo handler que grava.
/// </summary>
/// <remarks>
/// Os cinco handlers de consulta que registram o acesso continuam fora de propósito: o registro é publicado de forma
/// síncrona no Kafka, e a transação ambiente seguraria a conexão aberta até o broker confirmar, sem nada a gravar
/// atomicamente.
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
public sealed class ParticipacaoNaTransacaoDosHandlersTests
{
    private const string FrameQueEnrolaOContexto = "EnrollDbContextInTransaction";

    private static readonly Type[] MensagensNaTransacao =
    [
        typeof(CriarOfertaCursoCommand),
        typeof(CriarFaseCanonicaCommand),
        typeof(AtualizarFaseCanonicaCommand),
        typeof(CriarObrigatoriedadeLegalCommand),
        typeof(AtualizarObrigatoriedadeLegalCommand),
        typeof(CriarProcessoSeletivoCommand),
        typeof(ListarProcessosSeletivosQuery),
        typeof(LerProcessoSeletivoQuery),
        typeof(LerSnapshotVigenteQuery),
    ];

    private static readonly Type[] MensagensForaDaTransacao =
    [
        typeof(ListarInscricoesDoProcessoQuery),
        typeof(ObterInscricaoAdministrativaQuery),
        typeof(EmitirComprovanteDaInscricaoQuery),
        typeof(RevelarCampoDaInscricaoQuery),
        typeof(ObterAcessoAoDocumentoDaInscricaoQuery),
    ];

    private readonly MonolitoPostgresFixture _fixture;

    public ParticipacaoNaTransacaoDosHandlersTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Os handlers que só ficavam de fora por haver dois contextos rodam na transação ambiente do Wolverine")]
    public void HandlersSemEfeitoExterno_RodamNaTransacaoAmbiente() =>
        MensagensSemTransacao(MensagensNaTransacao).Should().BeEquivalentTo(Array.Empty<string>());

    [Fact(DisplayName = "As consultas que registram o acesso no Kafka não seguram a conexão numa transação ambiente")]
    public void ConsultasQueRegistramAcesso_FicamForaDaTransacao() =>
        MensagensSemTransacao(MensagensForaDaTransacao).Should().BeEquivalentTo(MensagensForaDaTransacao.Select(t => t.Name));

    private IEnumerable<string> MensagensSemTransacao(IEnumerable<Type> mensagens)
    {
        HandlerGraph grafo = ((WolverineRuntime)_fixture.Factory.Services.GetRequiredService<IWolverineRuntime>()).Handlers;

        return mensagens
            .Where(mensagem => !EnrolaOContexto(grafo.ChainFor(mensagem)))
            .Select(mensagem => mensagem.Name);
    }

    private static bool EnrolaOContexto(HandlerChain? chain) =>
        chain is not null && chain.Middleware.Any(frame => frame.GetType().Name == FrameQueEnrolaOContexto);
}

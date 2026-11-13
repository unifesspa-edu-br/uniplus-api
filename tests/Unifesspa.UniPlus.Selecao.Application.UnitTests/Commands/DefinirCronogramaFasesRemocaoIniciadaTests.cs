namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static PublicacaoDeTeste;

/// <summary>
/// A edição do cronograma na retificação recusa a remoção da fase iniciada, conferida contra a versão publicada vigente
/// (UNI-REQ-0364); a fase que ainda não começou segue só as demais regras de remoção.
/// </summary>
public sealed class DefinirCronogramaFasesRemocaoIniciadaTests
{
    private const string CodigoDaRemocao = "FaseCronograma.FaseIniciadaNaoSaiDoCronograma";

    [Fact(DisplayName = "Na retificação, trocar a fase iniciada por outra recusa a remoção dela")]
    public async Task Handle_RetificacaoQueRemoveFaseIniciada_Recusa()
    {
        (ProcessoSeletivo processo, Guid novaFase) = RetificacaoAberta();
        FaseCronograma publicada = processo.CronogramaFases.Single();
        FasesIniciadas iniciada = new(
            [KeyValuePair.Create<Guid, Guid?>(publicada.FaseCanonicaOrigemId, UnidadesDeTeste.Ceps.OrigemId)],
            [KeyValuePair.Create(publicada.FaseCanonicaOrigemId, publicada.Codigo)]);

        Result<MutacaoAceita> resultado = await DefinirAsync(processo, novaFase, iniciada);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(CodigoDaRemocao);
    }

    [Fact(DisplayName = "Na retificação, a fase que não começou segue só as demais regras de remoção")]
    public async Task Handle_RetificacaoQueRemoveFaseNaoIniciada_NaoRecusaPelaRegraDaFaseIniciada()
    {
        (ProcessoSeletivo processo, Guid novaFase) = RetificacaoAberta();

        Result<MutacaoAceita> resultado = await DefinirAsync(processo, novaFase, FasesIniciadas.Nenhuma);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FaseReferenciadaPorFormulario");
    }

    private static (ProcessoSeletivo Processo, Guid NovaFase) RetificacaoAberta()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicado();
        processo.AbrirRetificacao(
            "Correção do prazo", versaoAtual, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), "user-sub-1", Agora)
            .IsSuccess.Should().BeTrue();
        return (processo, Guid.CreateVersion7());
    }

    private static Task<Result<MutacaoAceita>> DefinirAsync(ProcessoSeletivo processo, Guid novaFase, FasesIniciadas iniciadas)
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.Para(processo)
            .ComFasesIniciadas(iniciadas)
            .ComTipoDeAto(Resultado());
        cenario.CadastrarFase(FaseCanonica(novaFase));

        return cenario.DefinirAsync(
            new DefinirCronogramaFasesCommand(processo.Id, [InputDaFase(novaFase)], PrecondicaoIfMatch.Curinga));
    }

    private static TipoAtoPublicadoView Resultado() =>
        new("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true);

    private static FaseCanonicaView FaseCanonica(Guid id) => new(
        id, "RESULTADO_FINAL", "Resultado Final", null,
        AgrupaEtapas: false, PermiteComplementacao: false, BaseLegal: null,
        ColetaInscricao: false, ColetaSolicitacaoIsencao: false, OrigemData: "NO_EDITAL");

    private static FaseCronogramaInput InputDaFase(Guid faseCanonicaId) => new(
        Ordem: 1,
        FaseCanonicaId: faseCanonicaId,
        Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        Produtos: [new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Definitivo)],
        FaseConcluinteCodigo: null,
        EmiteParecerIndividual: false,
        BancasRequeridas: [],
        RegraRecurso: null);
}

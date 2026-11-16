namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoDescarteAposDeslocamentoDeOrdem;

internal sealed class CenarioDoDeslocamentoDeOrdem(ProcessoSeletivoDbFixture fixture, bool comRecurso = false)
{
    internal Guid FaseInscricaoOrigem { get; } = Guid.CreateVersion7();

    internal Guid FasePreliminarOrigem { get; } = Guid.CreateVersion7();

    internal Guid FaseFinalOrigem { get; } = Guid.CreateVersion7();

    internal Guid ProcessoId { get; private set; }

    internal Guid VersaoId { get; private set; }

    internal Guid ProdutoPreliminarId { get; private set; }

    internal Guid ProdutoFinalId { get; private set; }

    internal VersaoConfiguracao Versao { get; private set; } = null!;

    internal async Task PublicarAsync(string nome)
    {
        ProcessoSeletivo processo = NovoProcesso($"{nome} {Guid.CreateVersion7()}");
        DeclararCronogramaCongelado(processo);
        Result<VersaoConfiguracao> publicacao = Publicar(processo, comRecurso ? ComCalendario() : null);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        processo.ClearDomainEvents();
        Versao = publicacao.Value!;
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        await contexto.ProcessosSeletivos.AddAsync(processo);
        await contexto.AddAsync(Versao);
        await contexto.SaveChangesAsync();
        GuardarIdentidades(processo);
    }

    internal async Task InserirHomologacaoNoMeioAsync()
    {
        await using UniPlusDbContext sessao = fixture.CreateDbContext();
        ProcessoSeletivo rastreado = await CarregarAsync(sessao, ProcessoId);
        VersaoConfiguracao versaoDaRetificacao = await VersaoParaAbrirARetificacaoAsync(sessao);
        rastreado.AbrirRetificacao("Insere a homologação no meio do cronograma", versaoDaRetificacao, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(rastreado), "teste", Agora)
            .IsSuccess.Should().BeTrue();
        Result deslocamento = rastreado.DefinirCronogramaFases(CronogramaComHomologacaoNoMeio(), [], PrecondicaoIfMatch.Curinga);
        deslocamento.IsSuccess.Should().BeTrue(deslocamento.Error?.Message);
        await sessao.SaveChangesAsync();
    }

    internal async Task DescartarAsync()
    {
        await using UniPlusDbContext descarte = fixture.CreateDbContext();
        ProcessoSeletivo rastreado = await CarregarAsync(descarte, ProcessoId);
        VersaoConfiguracao versaoDoBanco = await descarte.Set<VersaoConfiguracao>().FirstAsync(v => v.Id == VersaoId);
        Result reposicao = rastreado.RestaurarConfiguracaoCongelada(versaoDoBanco, GrafoCongeladoDoCenario());
        reposicao.IsSuccess.Should().BeTrue(reposicao.Error?.Message);
        Result resultadoDoDescarte = rastreado.DescartarRetificacao(PrecondicaoIfMatch.Curinga);
        resultadoDoDescarte.IsSuccess.Should().BeTrue(resultadoDoDescarte.Error?.Message);
        await descarte.SaveChangesAsync();
    }

    private async Task<VersaoConfiguracao> VersaoParaAbrirARetificacaoAsync(UniPlusDbContext sessao) =>
        comRecurso ? await sessao.Set<VersaoConfiguracao>().FirstAsync(v => v.Id == VersaoId) : Versao;

    internal async Task<ProcessoSeletivo> CarregarEmContextoNovoAsync()
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        return await CarregarAsync(contexto, ProcessoId);
    }

    private void DeclararCronogramaCongelado(ProcessoSeletivo processo)
    {
        if (comRecurso)
        {
            // O prazo de interposição corre em dia útil, e a publicação só passa com a
            // convenção de contagem declarada e o calendário vigente em mãos.
            processo.DefinirAlgoritmoContagemPrazo(
                Regra(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, 'f'), PrecondicaoIfMatch.Ausente)
                .IsSuccess.Should().BeTrue();
        }

        processo.DefinirCronogramaFases(CronogramaCongelado(), [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    private FaseCronograma[] CronogramaCongelado()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);
        return
        [
            Fase(1, "INSCRICAO", FaseInscricaoOrigem, []),
            Fase(2, "RESULTADO_PRELIMINAR", FasePreliminarOrigem, [preliminar],
                faseConcluinteCodigo: "RESULTADO_FINAL", regraRecurso: comRecurso ? Recurso(preliminar.Id) : null),
            Fase(3, "RESULTADO_FINAL", FaseFinalOrigem,
                [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)]),
        ];
    }

    // O produto preliminar da sessão nasce com identidade nova: só o rastreado sobrevive à reconciliação.
    private FaseCronograma[] CronogramaComHomologacaoNoMeio()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);
        preliminar.Id.Should().NotBe(ProdutoPreliminarId,
            "pré-condição: a coleção que chega traz identidade nova, e é a rastreada que sobrevive");
        return
        [
            Fase(1, "INSCRICAO", FaseInscricaoOrigem, []),
            Fase(2, "HOMOLOGACAO_INSCRICOES", Guid.CreateVersion7(),
                [ProdutoDaFase.Criar("HOMOLOGACAO_INSCRICOES", PapelProdutoFase.Definitivo)]),
            Fase(3, "RESULTADO_PRELIMINAR", FasePreliminarOrigem, [preliminar],
                faseConcluinteCodigo: "RESULTADO_FINAL", regraRecurso: comRecurso ? Recurso(preliminar.Id) : null),
            Fase(4, "RESULTADO_FINAL", FaseFinalOrigem,
                [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)]),
        ];
    }

    private GrafoConfiguracao GrafoCongeladoDoCenario() => comRecurso
        ? GrafoCongeladoComRecurso(FaseInscricaoOrigem, FasePreliminarOrigem, FaseFinalOrigem, ProdutoPreliminarId, ProdutoFinalId)
        : GrafoCongelado(FaseInscricaoOrigem, FasePreliminarOrigem, FaseFinalOrigem, ProdutoPreliminarId, ProdutoFinalId);

    private void GuardarIdentidades(ProcessoSeletivo processo)
    {
        ProcessoId = processo.Id;
        VersaoId = Versao.Id;
        ProdutoPreliminarId = ProdutoDe(processo, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar).Id;
        ProdutoFinalId = ProdutoDe(processo, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo).Id;
    }

    private static GrafoConfiguracao GrafoCongeladoComRecurso(
        Guid faseInscricaoOrigem,
        Guid fasePreliminarOrigem,
        Guid faseFinalOrigem,
        Guid produtoPreliminarId,
        Guid produtoFinalId) => new(
        etapas: [],
        atendimentoEspecializado: AtendimentoEspecializado.Criar([], [], []).Value!,
        distribuicaoVagas: [Distribuicao()],
        bonusRegional: null,
        criteriosDesempate: [],
        classificacao: Classificacao(),
        cronogramaFases: FasesCongeladasComRecurso(
            faseInscricaoOrigem, fasePreliminarOrigem, faseFinalOrigem, produtoPreliminarId, produtoFinalId),
        documentosExigidos: [],
        nosExigencia: [],
        referenciaTemporalFatos: null,
        configuracaoTaxaInscricao: ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
        localidade: LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
        numeroDeOpcoesDeCurso: 1);
    private static FaseCronograma[] FasesCongeladasComRecurso(
        Guid faseInscricaoOrigem,
        Guid fasePreliminarOrigem,
        Guid faseFinalOrigem,
        Guid produtoPreliminarId,
        Guid produtoFinalId) =>
    [
        FaseReidratada(1, "INSCRICAO", faseInscricaoOrigem, [], null),
        FaseReidratada(2, "RESULTADO_PRELIMINAR", fasePreliminarOrigem,
            [ProdutoDaFase.Reidratar(produtoPreliminarId, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar)],
            "RESULTADO_FINAL",
            Recurso(produtoPreliminarId)),
        FaseReidratada(3, "RESULTADO_FINAL", faseFinalOrigem,
            [ProdutoDaFase.Reidratar(produtoFinalId, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
            null),
    ];

    /// <summary>
    /// Calendário vigente mínimo — um feriado nacional basta. Só o cenário do prazo de recurso
    /// precisa dele: sem calendário, a publicação recusaria por uma pendência que não é a
    /// testada ali.
    /// </summary>
    private static ContextoDeContagemDePrazos ComCalendario() => new(
        CalendarioDiasUteisCongelado.Criar(
            Guid.CreateVersion7(),
            "2026",
            [DiaNaoUtilCongelado.Criar(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null).Value!]).Value,
        FusoInstitucional: TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId));
    private static RegraRecursoFase Recurso(Guid produtoAncoraId) => RegraRecursoFase.Criar(
        Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, 'e'),
        new ArgsRegraPrazoRecurso(
            PrazoValor: 48m,
            PrazoUnidade: UnidadePrazo.Horas,
            SuspensividadePrimeiraInstanciaValor: null,
            SuspensividadePrimeiraInstanciaUnidade: null,
            AdmiteInstanciaSuperior: false,
            SuspensividadeSegundaInstanciaValor: null,
            SuspensividadeSegundaInstanciaUnidade: null),
        produtoAncoraId).Value!;
}

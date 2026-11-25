namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O processo e os cadastros que o handler da classificação consulta. Os testes descrevem o que o catálogo
/// de regras e o cadastro de pesos por área têm; a chamada ao handler fica toda aqui, de modo que uma
/// mudança de assinatura dele se faz num lugar só.
/// </summary>
internal sealed class CenarioDaClassificacao
{
    public const string ResolucaoDePesos = "Res. 805/2024";

    public static readonly (string Codigo, string Rotulo)[] GruposDoAnexoI =
    [
        ("HUMANISTICA_I", "Humanística I"),
        ("HUMANISTICA_II", "Humanística II"),
        ("SAUDE_E_BIOLOGICAS", "Saúde e Biológicas"),
        ("TECNOLOGICA", "Tecnológica"),
    ];

    private readonly IProcessoSeletivoRepository _repository = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IRegraCatalogoReader _regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();

    private CenarioDaClassificacao(ProcessoSeletivo? processo, Guid processoId)
    {
        _repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
    }

    public IPesoAreaEnemReader PesoAreaEnemReader { get; } = Substitute.For<IPesoAreaEnemReader>();

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public static CenarioDaClassificacao Com(ProcessoSeletivo processo) => new(processo, processo.Id);

    public static CenarioDaClassificacao SemProcesso() => new(null, Guid.CreateVersion7());

    public static ProcessoSeletivo NovoProcesso(string titulo, TipoProcessoSnapshot tipo) =>
        ProcessoSeletivo.Criar(titulo, tipo, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    public static ProcessoSeletivo NovoProcessoEnem() => NovoProcesso("PS Medicina 2027", TiposDeProcessoDeTeste.VagasRemanescentes);

    public static EtapaProcesso EtapaObjetiva() =>
        EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    public static DefinirClassificacaoCommand ComandoDeMediaPonderada(Guid processoId) =>
        new(processoId,
            RegraDeCalculoDaNota.MediaPonderada.Codigo(), "v1",
            RegraDeArredondamentoDaNota.Truncar.Codigo(), "v1", 2,
            RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1",
            [], false, null, null);

    public static DefinirClassificacaoCommand ComandoComEliminacao(Guid processoId, RegraEliminacaoInput eliminacao) =>
        ComandoDeMediaPonderada(processoId) with { RegrasEliminacao = [eliminacao] };

    public static DefinirClassificacaoCommand ComandoEnemLocal(
        Guid processoId, string? resolucao, bool baseadoEmEnem = true, string? regraCalculo = null)
    {
        regraCalculo ??= RegraDeCalculoDaNota.MediaPonderada.Codigo();
        return regraCalculo == RegraDeCalculoDaNota.ClassificacaoImportada.Codigo()
            ? new(processoId, regraCalculo, "v1", null, null, null,
                RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", [], baseadoEmEnem, resolucao, null)
            : new(processoId, regraCalculo, "v1", RegraDeArredondamentoDaNota.Truncar.Codigo(), "v1", 2,
                RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", [], baseadoEmEnem, resolucao, null);
    }

    public static DefinirClassificacaoCommand ComandoEnemLocalComEliminacao(Guid processoId, RegraEliminacaoInput eliminacao) =>
        ComandoEnemLocal(processoId, ResolucaoDePesos) with { RegrasEliminacao = [eliminacao] };

    public static ResolucaoPesoAreaEnemView ResolucaoCom(
        IEnumerable<(string Codigo, string Rotulo)> presentes,
        params (string Codigo, string Rotulo)[] ausentes) =>
        new(
            ResolucaoDePesos,
            [.. presentes.Select(LinhaDoGrupo)],
            [.. ausentes.Select(static g => new GrupoAreaEnemView(g.Codigo, g.Rotulo))]);

    public CenarioDaClassificacao ComRegra(string codigo, TipoRegra tipo)
    {
        _regraCatalogoReader.ObterAsync(codigo, "v1", Arg.Any<CancellationToken>()).Returns(Regra(codigo, tipo));
        return this;
    }

    public CenarioDaClassificacao ComRegrasBasicas() =>
        ComRegra(RegraDeCalculoDaNota.MediaPonderada.Codigo(), TipoRegra.RegraCalculo)
            .ComRegra(RegraDeArredondamentoDaNota.Truncar.Codigo(), TipoRegra.RegraArredondamento)
            .ComRegra(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), TipoRegra.RegraOrdemAlocacao);

    public CenarioDaClassificacao SemNenhumaRegraNoCatalogo()
    {
        _regraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((RegraCatalogo?)null);
        return this;
    }

    public CenarioDaClassificacao ComResolucaoDosPesos(ResolucaoPesoAreaEnemView? resolucao = null)
    {
        PesoAreaEnemReader.ObterPorResolucaoAsync(ResolucaoDePesos, Arg.Any<CancellationToken>())
            .Returns(resolucao ?? ResolucaoCom(GruposDoAnexoI));
        return this;
    }

    public CenarioDaClassificacao ComResolucaoCadastrada(ResolucaoPesoAreaEnemView? resolucao)
    {
        PesoAreaEnemReader.ObterPorResolucaoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(resolucao);
        return this;
    }

    public Task<Result> DefinirAsync(DefinirClassificacaoCommand command) =>
        DefinirClassificacaoCommandHandler.Handle(
            command, _repository, _regraCatalogoReader, PesoAreaEnemReader, UnitOfWork, CancellationToken.None);

    public Task NaoConsultouOCadastroDePesosAsync() =>
        PesoAreaEnemReader.DidNotReceiveWithAnyArgs().ObterPorResolucaoAsync(default!, default);

    private static RegraCatalogo Regra(string codigo, TipoRegra tipo) =>
        RegraCatalogo.Criar(codigo, "v1", tipo, Json("{}"), Json("[]"), "base legal");

    private static JsonElement Json(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    private static PesoAreaEnemView LinhaDoGrupo((string Codigo, string Rotulo) grupo) =>
        new(
            Guid.CreateVersion7(),
            ResolucaoDePesos,
            new GrupoAreaEnemView(grupo.Codigo, grupo.Rotulo),
            [
                new PesoAreaEnemAreaView("REDACAO", "Redação", 2.00m, 400m),
                new PesoAreaEnemAreaView("CIENCIAS_DA_NATUREZA", "Ciências da Natureza e suas Tecnologias", 1.50m, null),
                new PesoAreaEnemAreaView("CIENCIAS_HUMANAS", "Ciências Humanas e suas Tecnologias", 2.50m, null),
                new PesoAreaEnemAreaView("LINGUAGENS", "Linguagens e suas Tecnologias", 2.50m, null),
                new PesoAreaEnemAreaView("MATEMATICA", "Matemática e suas Tecnologias", 1.50m, null),
            ],
            $"Resolução nº 805/2024/Consepe – Anexo I ({grupo.Rotulo})");
}

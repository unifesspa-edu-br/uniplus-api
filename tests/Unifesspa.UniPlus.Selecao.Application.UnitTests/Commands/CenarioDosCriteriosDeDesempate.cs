namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O processo e os cadastros que o handler dos critérios de desempate consulta. O catálogo de critérios vem
/// completo, como em produção: o caminho de reserva por <c>ObterAsync</c> tem teste próprio. A chamada ao
/// handler fica toda aqui, de modo que uma mudança de assinatura dele se faz num lugar só.
/// </summary>
internal sealed class CenarioDosCriteriosDeDesempate
{
    private CenarioDosCriteriosDeDesempate(ProcessoSeletivo? processo, Guid processoId)
    {
        Repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(FatosDoCatalogo());
        RegraCatalogoReader.ListarPorTipoAsync(TipoRegra.CriterioDesempate, Arg.Any<CancellationToken>()).Returns(CriteriosDoCatalogo());
    }

    public IProcessoSeletivoRepository Repository { get; } = Substitute.For<IProcessoSeletivoRepository>();

    public IRegraCatalogoReader RegraCatalogoReader { get; } = Substitute.For<IRegraCatalogoReader>();

    public IFatoCandidatoReader FatoCandidatoReader { get; } = Substitute.For<IFatoCandidatoReader>();

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public static CenarioDosCriteriosDeDesempate Com(ProcessoSeletivo processo) => new(processo, processo.Id);

    public static CenarioDosCriteriosDeDesempate SemProcesso() => new(null, Guid.CreateVersion7());

    public static ProcessoSeletivo NovoProcesso(string titulo, TipoProcessoSnapshot tipo) =>
        ProcessoSeletivo.Criar(titulo, tipo, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    public static ProcessoSeletivo ProcessoComClassificacaoEnem()
    {
        ProcessoSeletivo processo = NovoProcesso("PS ENEM 2027", TiposDeProcessoDeTeste.PSIQ);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.FormulaMediaPonderada, "v1", new string('a', 64)).Value!,
            ReferenciaRegra.Criar(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", new string('b', 64)).Value!,
            2,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('c', 64)).Value!,
            [],
            baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnemDeTeste.Completo(), escalaNotaFinal: 100m).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    public static DefinirCriteriosDesempateCommand Comando(Guid processoId, params CriterioDesempateInput[] criterios) =>
        new(processoId, criterios, PrecondicaoIfMatch.Ausente);

    public static CriterioDesempateInput CriterioSimples(int ordem, string codigo) =>
        new(ordem, codigo, "v1", null, null, null, null, null);

    public static CriterioDesempateInput CriterioDeAreas(int ordem, params string[] areas) =>
        new(ordem, CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", null, null, null, null, null, areas);

    public CenarioDosCriteriosDeDesempate ComRegra(string codigo, TipoRegra tipo = TipoRegra.CriterioDesempate)
    {
        RegraCatalogoReader.ObterAsync(codigo, "v1", Arg.Any<CancellationToken>()).Returns(Regra(codigo, tipo));
        return this;
    }

    public CenarioDosCriteriosDeDesempate ComRegraInexistente(string codigo)
    {
        RegraCatalogoReader.ObterAsync(codigo, "v1", Arg.Any<CancellationToken>()).Returns((RegraCatalogo?)null);
        return this;
    }

    public CenarioDosCriteriosDeDesempate SemNenhumaRegraNoCatalogo()
    {
        RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((RegraCatalogo?)null);
        return this;
    }

    public CenarioDosCriteriosDeDesempate ComCriteriosNoCatalogo(params string[] codigos)
    {
        RegraCatalogoReader.ListarPorTipoAsync(TipoRegra.CriterioDesempate, Arg.Any<CancellationToken>()).Returns(
            (IReadOnlyList<RegraCatalogo>)[.. codigos.Select(static codigo => Regra(codigo, TipoRegra.CriterioDesempate))]);
        return this;
    }

    public Task<Result<MutacaoAceita>> DefinirAsync(DefinirCriteriosDesempateCommand command) =>
        DefinirCriteriosDesempateCommandHandler.Handle(
            command, Repository, RegraCatalogoReader, FatoCandidatoReader, UnitOfWork, CancellationToken.None);

    private static RegraCatalogo Regra(string codigo, TipoRegra tipo) =>
        RegraCatalogo.Criar(codigo, "v1", tipo, Json("{}"), Json("[]"), "base legal");

    private static JsonElement Json(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    private static IReadOnlyList<FatoCandidatoView> FatosDoCatalogo() =>
    [
        new FatoCandidatoView(
            Guid.CreateVersion7(), "PROFESSOR_RURAL", "Professor da rede pública rural", "Descrição do fato.",
            "BOOLEANO", "DECLARADO", "ESCALAR", null, "INSCRICAO", "CAMPO_FORMULARIO:PROFESSOR_RURAL", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
    ];

    private static IReadOnlyList<RegraCatalogo> CriteriosDoCatalogo() =>
    [
        Regra(CriterioDesempateCodigo.MaiorNotaEtapa, TipoRegra.CriterioDesempate),
        Regra(CriterioDesempateCodigo.MaiorIdade, TipoRegra.CriterioDesempate),
        Regra(CriterioDesempateCodigo.Idoso, TipoRegra.CriterioDesempate),
        Regra(CriterioDesempateCodigo.PredicadoFato, TipoRegra.CriterioDesempate),
        Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, TipoRegra.CriterioDesempate),
    ];
}

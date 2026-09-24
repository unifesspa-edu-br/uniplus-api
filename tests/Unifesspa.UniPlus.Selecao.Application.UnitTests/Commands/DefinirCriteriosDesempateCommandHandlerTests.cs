namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class DefinirCriteriosDesempateCommandHandlerTests
{
    private static JsonElement Json(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    private static RegraCatalogo Regra(string codigo, TipoRegra tipo) =>
        RegraCatalogo.Criar(codigo, "v1", tipo, Json("{}"), Json("[]"), "base legal").Value!;

    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IRegraCatalogoReader RegraCatalogoReader,
        IFatoCandidatoReader FatoCandidatoReader,
        ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);

        IFatoCandidatoReader fatoCandidatoReader = Substitute.For<IFatoCandidatoReader>();
        fatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(
            (IReadOnlyList<FatoCandidatoView>)
            [
                new FatoCandidatoView(
                    Guid.CreateVersion7(), "PROFESSOR_RURAL", "Professor da rede pública rural", null,
                    "BOOLEANO", "DECLARADO", "ESCALAR", null, "INSCRICAO", "CAMPO_INSCRICAO:PROFESSOR_RURAL", null),
            ]);

        // O catálogo de critérios de desempate vem completo, como em produção: o caminho de
        // reserva por ObterAsync tem teste próprio.
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        regraCatalogoReader.ListarPorTipoAsync(TipoRegra.CriterioDesempate, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<RegraCatalogo>)
            [
                Regra(CriterioDesempateCodigo.MaiorNotaEtapa, TipoRegra.CriterioDesempate),
                Regra(CriterioDesempateCodigo.MaiorIdade, TipoRegra.CriterioDesempate),
                Regra(CriterioDesempateCodigo.Idoso, TipoRegra.CriterioDesempate),
                Regra(CriterioDesempateCodigo.PredicadoFato, TipoRegra.CriterioDesempate),
                Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, TipoRegra.CriterioDesempate),
            ]);

        return new Mocks(repository, regraCatalogoReader, fatoCandidatoReader, Substitute.For<ISelecaoUnitOfWork>());
    }

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());
        DefinirCriteriosDesempateCommand command = new(Guid.CreateVersion7(), [], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com DESEMPATE-MAIOR-NOTA-ETAPA resolve args e persiste")]
    public async Task Handle_MaiorNotaEtapa_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso etapa = EtapaProcesso.Criar("Entrevista", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente);

        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorNotaEtapa, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorNotaEtapa, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id, [new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorNotaEtapa, "v1", etapa.Id, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.CriteriosDesempate.Should().ContainSingle();
        ((ArgsDesempateMaiorNotaEtapa)processo.CriteriosDesempate.Single().Args).EtapaRef.Should().Be(etapa.Id);
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com DESEMPATE-MAIOR-NOTA-ETAPA sem EtapaRef recusa")]
    public async Task Handle_MaiorNotaEtapaSemEtapaRef_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorNotaEtapa, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorNotaEtapa, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id, [new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorNotaEtapa, "v1", null, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CriterioDesempate.EtapaRefObrigatorio");
    }

    [Fact(DisplayName = "Handle com DESEMPATE-IDOSO resolve args e persiste")]
    public async Task Handle_Idoso_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSE 2026", TipoProcesso.PSECampo, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.Idoso, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.Idoso, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id, [new CriterioDesempateInput(1, CriterioDesempateCodigo.Idoso, "v1", null, 60, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ((ArgsDesempateIdoso)processo.CriteriosDesempate.Single().Args).IdadeMinima.Should().Be(60);
    }

    [Fact(DisplayName = "Handle com DESEMPATE-MAIOR-NOTA-AREA-ENEM guarda os códigos aparados, na ordem declarada")]
    public async Task Handle_MaiorNotaAreaEnem_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS ENEM 2027", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id,
            [new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", null, null, null, null, null, [" REDACAO ", "MATEMATICA"])],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        ((ArgsDesempateMaiorNotaAreaEnem)processo.CriteriosDesempate.Single().Args).Areas.Should().Equal("REDACAO", "MATEMATICA");
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com DESEMPATE-MAIOR-NOTA-AREA-ENEM sem áreas recusa no campo do critério")]
    public async Task Handle_MaiorNotaAreaEnemSemAreas_RecusaNoCampo()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS ENEM 2027", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id,
            [new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", null, null, null, null, null)],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("criterios[0].areas");
        erro.Error.Code.Should().Be("CriterioDesempate.AreasObrigatorias");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com área nula na ordem recusa no campo do item, sem estourar")]
    public async Task Handle_MaiorNotaAreaEnemComItemNulo_RecusaNoCampoDoItem()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS ENEM 2027", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id,
            [new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", null, null, null, null, null, ["REDACAO", null!])],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("criterios[0].areas[1]");
        erro.Error.Code.Should().Be("CriterioDesempate.AreaInvalida");
    }

    [Fact(DisplayName = "Handle devolve todas as áreas fora do quadro, cada uma no campo do item, e não persiste")]
    public async Task Handle_AreasForaDoQuadroEmDoisCriterios_DevolveTodasNosCampos()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS ENEM 2027", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.FormulaMediaPonderada, "v1", new string('a', 64)).Value!,
            ReferenciaRegra.Criar(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", new string('b', 64)).Value!,
            2,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", new string('c', 64)).Value!,
            1,
            [],
            baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnemDeTeste.Completo()).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id,
            [
                new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", null, null, null, null, null, ["FISICA", "REDACAO"]),
                new CriterioDesempateInput(2, CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", null, null, null, null, null, ["MATEMATICA", "QUIMICA"]),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[0].areas[0]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"),
            ("criterios[1].areas[1]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"));
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static ProcessoSeletivo ProcessoComClassificacaoEnem()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS ENEM 2027", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.FormulaMediaPonderada, "v1", new string('a', 64)).Value!,
            ReferenciaRegra.Criar(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", new string('b', 64)).Value!,
            2,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", new string('c', 64)).Value!,
            1,
            [],
            baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnemDeTeste.Completo()).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    [Fact(DisplayName = "Handle acumula a recusa de um critério que nem é criado com a área fora do quadro de outro")]
    public async Task Handle_AreaRepetidaEAreaForaDoQuadroEmCriteriosDiferentes_DevolveAsDuas()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id,
            [
                new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", null, null, null, null, null, ["REDACAO", "REDACAO"]),
                new CriterioDesempateInput(2, CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", null, null, null, null, null, ["FISICA"]),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[0].areas[1]", "CriterioDesempate.AreaRepetida"),
            ("criterios[1].areas[0]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"));
        processo.CriteriosDesempate.Should().BeEmpty();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Lista vazia remove os critérios sem consultar o catálogo")]
    public async Task Handle_ListaVazia_NaoConsultaOCatalogo()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            new DefinirCriteriosDesempateCommand(processo.Id, [], PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await mocks.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ListarPorTipoAsync(default, default);
    }

    [Fact(DisplayName = "Lista acima do teto é recusada pelo agregado com um único erro, sem ler item nem catálogo")]
    public async Task Handle_AcimaDoTeto_RecusaComUmErroSemLerItens()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        CriterioDesempateInput[] criterios = [.. Enumerable.Range(1, 10_000).Select(static ordem =>
            ordem % 2 == 0 ? null! : new CriterioDesempateInput(-ordem, string.Empty, string.Empty, null, null, null, null, null))];

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            new DefinirCriteriosDesempateCommand(processo.Id, criterios, PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.CriteriosDesempateEmExcesso");
        await mocks.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ListarPorTipoAsync(default, default);
        await mocks.FatoCandidatoReader.DidNotReceiveWithAnyArgs().ListarAsync(default);
    }

    [Theory(DisplayName = "Área recusada pela forma traz a lista de aceitas, sem ecoar o texto nem somar a recusa fora do quadro")]
    [InlineData("Redação")]
    [InlineData("RED\u202EACAO\n\u2028X")]
    [InlineData(null)]
    public async Task Handle_AreaForaDaForma_SoARecusaDaForma(string? area)
    {
        area ??= new string('A', 200_000);
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, TipoRegra.CriterioDesempate));

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            new DefinirCriteriosDesempateCommand(
                processo.Id,
                [new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", null, null, null, null, null, [area])],
                PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        (erro.Field, erro.Error.Code).Should().Be(("criterios[0].areas[0]", "CriterioDesempate.AreaInvalida"));
        erro.Error.Message.Should().StartWith("Cada área da ordem de desempate é um código")
            .And.Contain("Áreas aceitas: CIENCIAS_DA_NATUREZA (Ciências da Natureza e suas Tecnologias)")
            .And.NotContain("\u202E").And.NotContain("\n").And.NotContain("AAAAAAAAAA");
    }

    [Fact(DisplayName = "Sem quadro de pesos por área, a recusa pela forma sai sem lista de áreas aceitas")]
    public async Task Handle_AreaForaDaFormaSemQuadro_SemLista()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS ENEM 2027", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('a', 64)).Value!,
            null,
            null,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", new string('c', 64)).Value!,
            1,
            [],
            baseadoEmEnem: true,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, TipoRegra.CriterioDesempate));

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            new DefinirCriteriosDesempateCommand(
                processo.Id,
                [new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", null, null, null, null, null, ["Redação"])],
                PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        FieldError forma = result.Errors.Single(static e => e.Error.Code == "CriterioDesempate.AreaInvalida");
        forma.Error.Message.Should().NotContain("Áreas aceitas");
    }

    [Fact(DisplayName = "A lista de áreas aceitas vai uma vez só, mesmo com recusas de forma e fora do quadro juntas")]
    public async Task Handle_VariasRecusasDeArea_ListaUmaVez()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, TipoRegra.CriterioDesempate));

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            new DefinirCriteriosDesempateCommand(
                processo.Id,
                [
                    new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", null, null, null, null, null, ["Redação", "FISICA"]),
                    new CriterioDesempateInput(2, CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", null, null, null, null, null, ["QUIMICA"]),
                ],
                PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.Errors.Should().HaveCount(3);
        result.Errors.Count(static e => e.Error.Message.Contains("Áreas aceitas", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact(DisplayName = "Área repetida e lista acima do teto de áreas não geram também a recusa fora do quadro")]
    public async Task Handle_AreaRepetidaEListaAcimaDoTeto_SemRecusaDuplicada()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, TipoRegra.CriterioDesempate));
        string[] areasDemais = [.. Enumerable.Range(0, CriterioDesempate.AreasMaximo + 1).Select(static i => $"AREA_{i}")];

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            new DefinirCriteriosDesempateCommand(
                processo.Id,
                [
                    new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", null, null, null, null, null, ["FISICA", "FISICA"]),
                    new CriterioDesempateInput(2, CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", null, null, null, null, null, areasDemais),
                ],
                PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("criterios[0].areas[1]", "CriterioDesempate.AreaRepetida"),
            ("criterios[1].areas", "CriterioDesempate.AreasEmExcesso"),
            ("criterios[0].areas[0]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"),
        ]);
    }

    [Fact(DisplayName = "O catálogo é lido antes de travar o processo para a mutação")]
    public async Task Handle_CatalogoLidoAntesDaTrava()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ListarPorTipoAsync(TipoRegra.CriterioDesempate, Arg.Any<CancellationToken>()).Returns(
            (IReadOnlyList<RegraCatalogo>)[Regra(CriterioDesempateCodigo.MaiorIdade, TipoRegra.CriterioDesempate)]);

        await DefinirCriteriosDesempateCommandHandler.Handle(
            new DefinirCriteriosDesempateCommand(
                processo.Id,
                [new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorIdade, "v1", null, null, null, null, null)],
                PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        Received.InOrder(() =>
        {
            _ = mocks.RegraCatalogoReader.ListarPorTipoAsync(TipoRegra.CriterioDesempate, Arg.Any<CancellationToken>());
            _ = mocks.Repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "Predicado sem fato e sem valor recusa nos dois campos")]
    public async Task Handle_PredicadoSemFatoESemValor_RecusaNosDoisCampos()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.PredicadoFato, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.PredicadoFato, TipoRegra.CriterioDesempate));

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            new DefinirCriteriosDesempateCommand(
                processo.Id,
                [new CriterioDesempateInput(1, CriterioDesempateCodigo.PredicadoFato, "v1", null, null, null, "IGUAL", null)],
                PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.Errors.Select(static e => e.Field).Should().Equal("criterios[0].fato", "criterios[0].valor");
    }

    [Fact(DisplayName = "Recusas que antes saíam sem campo saem no campo do item, em cada critério")]
    public async Task Handle_RecusasSemCampo_SaemNoCampoDoItem()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync("INEXISTENTE", "v1", Arg.Any<CancellationToken>()).Returns((RegraCatalogo?)null);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorNotaEtapa, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorNotaEtapa, TipoRegra.CriterioDesempate));
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.Idoso, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.Idoso, TipoRegra.CriterioDesempate));
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.PredicadoFato, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.PredicadoFato, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id,
            [
                new CriterioDesempateInput(1, "INEXISTENTE", "v1", null, null, null, null, null),
                new CriterioDesempateInput(2, CriterioDesempateCodigo.MaiorNotaEtapa, "v1", null, null, null, null, null),
                new CriterioDesempateInput(3, CriterioDesempateCodigo.Idoso, "v1", null, null, null, null, null),
                new CriterioDesempateInput(4, CriterioDesempateCodigo.PredicadoFato, "v1", null, null, "PROFESSOR_RURAL", null, "true"),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[0].regraCodigo", "CriterioDesempate.RegraNaoEncontrada"),
            ("criterios[1].etapaRef", "CriterioDesempate.EtapaRefObrigatorio"),
            ("criterios[2].idadeMinima", "CriterioDesempate.IdadeMinimaObrigatoria"),
            ("criterios[3].operador", "CriterioDesempate.PredicadoFatoIncompleto"));
    }

    [Fact(DisplayName = "Ordem duplicada entre um critério recusado e um válido também é acusada")]
    public async Task Handle_OrdemDuplicadaComCriterioRecusado_Acusa()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync("INEXISTENTE", "v1", Arg.Any<CancellationToken>()).Returns((RegraCatalogo?)null);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorIdade, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorIdade, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id,
            [
                new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorIdade, "v1", null, null, null, null, null),
                new CriterioDesempateInput(1, "INEXISTENTE", "v1", null, null, null, null, null),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[1].regraCodigo", "CriterioDesempate.RegraNaoEncontrada"),
            ("criterios[1].ordem", "ProcessoSeletivo.OrdemDesempateDuplicada"));
    }

    [Fact(DisplayName = "O catálogo de critérios é lido uma vez, e as regras dele se resolvem sem consulta por critério")]
    public async Task Handle_CatalogoLidoUmaVez_ResolveEmMemoria()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ListarPorTipoAsync(TipoRegra.CriterioDesempate, Arg.Any<CancellationToken>()).Returns(
            (IReadOnlyList<RegraCatalogo>)
            [
                Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, TipoRegra.CriterioDesempate),
                Regra(CriterioDesempateCodigo.MaiorIdade, TipoRegra.CriterioDesempate),
            ]);

        DefinirCriteriosDesempateCommand command = new(
            processo.Id,
            [
                new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", null, null, null, null, null, ["REDACAO"]),
                new CriterioDesempateInput(2, CriterioDesempateCodigo.MaiorIdade, "v1", null, null, null, null, null),
                new CriterioDesempateInput(3, CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1", null, null, null, null, null, ["MATEMATICA"]),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        await mocks.RegraCatalogoReader.Received(1).ListarPorTipoAsync(TipoRegra.CriterioDesempate, Arg.Any<CancellationToken>());
        await mocks.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, default);
    }

    [Fact(DisplayName = "Handle com DESEMPATE-PREDICADO-FATO sobre fato do vocabulário fechado resolve e persiste")]
    public async Task Handle_PredicadoFato_FatoConhecido_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSE 2026", TipoProcesso.PSECampo, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.PredicadoFato, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.PredicadoFato, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id, [new CriterioDesempateInput(1, CriterioDesempateCodigo.PredicadoFato, "v1", null, null, "PROFESSOR_RURAL", "IGUAL", "true")], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ArgsDesempatePredicadoFato args = (ArgsDesempatePredicadoFato)processo.CriteriosDesempate.Single().Args;
        args.Condicao.Fato.Should().Be("PROFESSOR_RURAL");
        args.Condicao.Valor.GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "Handle com DESEMPATE-PREDICADO-FATO sobre fato fora do vocabulário fechado recusa")]
    public async Task Handle_PredicadoFato_FatoDesconhecido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSE 2026", TipoProcesso.PSECampo, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.PredicadoFato, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.PredicadoFato, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id, [new CriterioDesempateInput(1, CriterioDesempateCodigo.PredicadoFato, "v1", null, null, "FATO_INEXISTENTE", "IGUAL", "S")], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("PredicadoDnf.FatoDesconhecido");
    }

    [Fact(DisplayName = "Handle com DESEMPATE-PREDICADO-FATO incompleto recusa")]
    public async Task Handle_PredicadoFatoIncompleto_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSE 2026", TipoProcesso.PSECampo, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.PredicadoFato, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.PredicadoFato, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id, [new CriterioDesempateInput(1, CriterioDesempateCodigo.PredicadoFato, "v1", null, null, "PROFESSOR_RURAL", null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CriterioDesempate.PredicadoFatoIncompleto");
    }

    [Fact(DisplayName = "Handle com regra inexistente recusa")]
    public async Task Handle_RegraNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RegraCatalogo?)null);

        DefinirCriteriosDesempateCommand command = new(
            processo.Id, [new CriterioDesempateInput(1, "INEXISTENTE", "v1", null, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CriterioDesempate.RegraNaoEncontrada");
    }

    [Fact(DisplayName = "Código fora do catálogo de critérios cai na consulta de reserva: regra de outro tipo recusa")]
    public async Task Handle_RegraTipoInvalido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraCalculoCodigo.FormulaMediaPonderada, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraCalculoCodigo.FormulaMediaPonderada, TipoRegra.RegraCalculo));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id, [new CriterioDesempateInput(1, RegraCalculoCodigo.FormulaMediaPonderada, "v1", null, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CriterioDesempate.RegraTipoInvalido");
    }

    [Fact(DisplayName = "ADR-0125: ordem inválida acumula junto com a regra inexistente, no mesmo item e em outro")]
    public async Task Handle_OrdemInvalidaERegraInexistente_Acumula()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirCriteriosDesempateCommand command = new(processo.Id,
        [
            new CriterioDesempateInput(0, CriterioDesempateCodigo.MaiorIdade, "v1", null, null, null, null, null),
            new CriterioDesempateInput(-1, "INEXISTENTE", "v1", null, null, null, null, null),
            new CriterioDesempateInput(2, "INEXISTENTE", "v1", null, null, null, null, null),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("criterios[0].ordem", "CriterioDesempate.OrdemInvalida"),
            ("criterios[1].ordem", "CriterioDesempate.OrdemInvalida"),
            ("criterios[1].regraCodigo", "CriterioDesempate.RegraNaoEncontrada"),
            ("criterios[2].regraCodigo", "CriterioDesempate.RegraNaoEncontrada"),
        ]);
    }

    [Fact(DisplayName = "ADR-0125: ordem inválida acumula entre critérios, com o índice prefixado ao field")]
    public async Task Handle_DoisCriteriosComOrdemInvalida_AcumulaComIndicePrefixado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirCriteriosDesempateCommand command = new(processo.Id,
        [
            new CriterioDesempateInput(0, CriterioDesempateCodigo.MaiorIdade, "v1", null, null, null, null, null),
            new CriterioDesempateInput(-1, CriterioDesempateCodigo.MaiorIdade, "v1", null, null, null, null, null),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["criterios[0].ordem", "criterios[1].ordem"]);
        await mocks.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, default);
    }

    [Fact(DisplayName = "ADR-0125: a recusa de CriterioDesempate.Criar carrega o índice do item no campo")]
    public async Task Handle_SegundoCriterioComIdadeMinimaInvalida_PrefixaIndiceNoField()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSE 2026", TipoProcesso.PSECampo, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorIdade, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorIdade, TipoRegra.CriterioDesempate));
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.Idoso, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.Idoso, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(processo.Id,
        [
            new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorIdade, "v1", null, null, null, null, null),
            new CriterioDesempateInput(2, CriterioDesempateCodigo.Idoso, "v1", null, 0, null, null, null),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["criterios[1].idadeMinima"]);
    }

    [Fact(DisplayName = "Handle com lista vazia remove todos os critérios e persiste")]
    public async Task Handle_ListaVazia_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirCriteriosDesempateCommand command = new(processo.Id, [], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.CriteriosDesempate.Should().BeEmpty();
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}

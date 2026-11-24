namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) da distribuição
/// de vagas: persiste e recarrega
/// <c>ConfiguracaoDistribuicaoVagas</c> + <c>ModalidadeSelecionada</c>,
/// provando o mapeamento EF (owned type <c>ReferenciaRegra</c>, snapshot de
/// referência demográfica opcional, jsonb de critérios cumulativos) contra
/// Postgres real, e a reconfiguração sobre o agregado tracked (mesma
/// regressão de <c>ValueGeneratedNever</c> validada na F0).
/// </summary>
public sealed class DistribuicaoVagasPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;
    private readonly ApoioDaPersistenciaDoProcessoSeletivo _apoio;

    public DistribuicaoVagasPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaPersistenciaDoProcessoSeletivo(fixture);
    }

    private static ModalidadeSelecionada NovaModalidade(
        string codigo, NaturezaLegalModalidade natureza, ComposicaoVagasModalidade composicao,
        string? composicaoOrigemCodigo = null,
        RegraRemanejamentoModalidade? regraRemanejamento = null,
        int? quantidadeDeclarada = null,
        PredicadoDnf? regraDeAplicabilidade = null) =>
        ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da(codigo, natureza, composicao) with { Descricao = $"Modalidade {codigo}", ComposicaoOrigemCodigo = composicaoOrigemCodigo, RegraRemanejamento = regraRemanejamento, RegraDeAplicabilidade = regraDeAplicabilidade ?? PredicadoDnf.SemClausula }, quantidadeDeclarada).Value!;

    private static readonly PredicadoDnf RegraDaReservaPcd = PredicadoDnf.CriarDeCondicoesAgrupadas(
    [
        (0, CondicaoDnf.Criar("PCD", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!),
        (0, CondicaoDnf.Criar("CONCORRER_PCD", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!),
    ]).Value!;

    [Fact(DisplayName = "Persiste e recarrega distribuição Lei 12.711 com referência demográfica e modalidades")]
    public async Task PersisteERecarrega_Lei12711ComDemografica()
    {
        ProcessoSeletivo processo = NovoProcessoComEtapa(TiposDeProcessoDeTeste.SiSU);
        Guid ofertaCursoId = Guid.CreateVersion7();
        processo.DefinirDistribuicaoVagas([OfertaDaLei12711(ofertaCursoId)], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        await _apoio.GravarAsync(processo);

        ConfiguracaoDistribuicaoVagas distribuicao = (await RecarregarComDistribuicaoAsync(processo.Id)).DistribuicaoVagas.Single();

        ConferirAIdentidadeEAsRegras(distribuicao, ofertaCursoId);
        ConferirAReferenciaDemografica(distribuicao);
        ConferirAsModalidadesEOQuadro(distribuicao);
    }

    [Fact(DisplayName = "Persiste e recarrega distribuição institucional sem referência demográfica")]
    public async Task PersisteERecarrega_InstitucionalSemDemografica()
    {
        ProcessoSeletivo processo = NovoProcessoComEtapa(TiposDeProcessoDeTeste.PSIQ);
        ConfiguracaoDistribuicaoVagas oferta = OfertaInstitucional(Guid.CreateVersion7(), 60, 'b', [NovaAcaoAfirmativa("IND", 60)]);
        processo.DefinirDistribuicaoVagas([oferta], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        await _apoio.GravarAsync(processo);

        ConfiguracaoDistribuicaoVagas distribuicao = (await RecarregarComDistribuicaoAsync(processo.Id)).DistribuicaoVagas.Single();

        distribuicao.RegraDistribuicao.Codigo.Should().Be(RegraDeDistribuicaoDeVagas.Institucional.Codigo());
        distribuicao.ReferenciaDemografica.Should().BeNull();
        distribuicao.GrupoAreaEnem.Should().BeNull("curso sem grupo declarado é aceito no rascunho e persiste sem grupo");
    }

    [Theory(DisplayName = "Grupo de área com uma das duas colunas nula volta como oferta sem grupo")]
    [InlineData("grupo_area_enem_codigo")]
    [InlineData("grupo_area_enem_rotulo")]
    public async Task GrupoComUmaColunaNula_VoltaSemGrupo(string coluna)
    {
        ProcessoSeletivo processo = NovoProcesso(TiposDeProcessoDeTeste.PSIQ);
        ConfiguracaoDistribuicaoVagas oferta = OfertaInstitucional(Guid.CreateVersion7(), 60, 'b', [NovaAcaoAfirmativa("IND", 60)], ("TECNOLOGICA", "Tecnológica"));
        processo.DefinirDistribuicaoVagas([oferta], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        await _apoio.GravarAsync(processo);
        await AnularAColunaDoGrupoAsync(coluna, processo.Id);

        ProcessoSeletivo recarregado = await RecarregarComDistribuicaoAsync(processo.Id);

        recarregado.DistribuicaoVagas.Single().GrupoAreaEnem.Should().BeNull(
            "as duas propriedades do grupo são obrigatórias, e o EF só materializa o grupo com as duas colunas preenchidas");
    }

    /// <summary>
    /// CA-15: redefinir substitui o quadro por inteiro, na mesma transação — nenhuma linha do quadro
    /// anterior ("QUIL" sozinho, quantidade 40) sobrevive.
    /// </summary>
    [Fact(DisplayName = "Reconfigurar distribuição sobre o agregado tracked insere os filhos novos, não falha em UPDATE")]
    public async Task ReconfigurarDistribuicaoSobreAgregadoTracked_InsereFilhos()
    {
        ProcessoSeletivo processo = NovoProcessoComEtapa(TiposDeProcessoDeTeste.PSECampo);
        Guid ofertaCursoId = Guid.CreateVersion7();
        ConfiguracaoDistribuicaoVagas original = OfertaInstitucional(ofertaCursoId, 40, 'c', [NovaAcaoAfirmativa("QUIL", 40)]);
        processo.DefinirDistribuicaoVagas([original], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        await _apoio.GravarAsync(processo);

        await RedefinirADistribuicaoAsync(processo.Id, ofertaCursoId);

        ConfiguracaoDistribuicaoVagas distribuicao = (await RecarregarComDistribuicaoAsync(processo.Id)).DistribuicaoVagas.Single();
        (distribuicao.VagasOfertadasBase, distribuicao.Modalidades.Count).Should().Be((45, 2));
        distribuicao.VagasOfertadas.Should().HaveCount(2);
        distribuicao.VagasOfertadas.Should().Contain(v => v.ModalidadeCodigo == "QUIL" && v.Quantidade == 20);
        distribuicao.VagasOfertadas.Should().Contain(v => v.ModalidadeCodigo == "IND" && v.Quantidade == 25);
    }

    private static ProcessoSeletivo NovoProcesso(TipoProcessoSnapshot tipo) => ProcessoSeletivo.Criar(
        "PS 2026", tipo, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static ProcessoSeletivo NovoProcessoComEtapa(TipoProcessoSnapshot tipo)
    {
        ProcessoSeletivo processo = NovoProcesso(tipo);
        TipoEtapaSnapshot tipoDaEtapa = TipoEtapaSnapshot.Criar(
            Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;
        processo.DefinirEtapas([EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: tipoDaEtapa, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!]);

        return processo;
    }

    private static ModalidadeSelecionada NovaAcaoAfirmativa(string codigo, int quantidade) =>
        NovaModalidade(codigo, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: quantidade);

    private static ConfiguracaoDistribuicaoVagas OfertaInstitucional(
        Guid ofertaCursoId, int vagasBase, char hashDaRegra, ModalidadeSelecionada[] modalidades, (string? Codigo, string? Rotulo)? grupoAreaEnem = null)
    {
        DistribuicaoDeVagasDeclarada declarada = new()
        {
            VagasOfertadasBase = vagasBase,
            PercentualDaReservaLegal = 1m,
            RegraDeDistribuicao = ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", new string(hashDaRegra, 64)),
            Modalidades = modalidades,
        };
        Result<ConfiguracaoDistribuicaoVagas> oferta = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoId, declarada, new FatosDaOfertaNoCadastro { GrupoAreaEnem = grupoAreaEnem, OfertaCurso = OfertaCursoDeTeste.Medicina });
        oferta.IsSuccess.Should().BeTrue(oferta.Error?.Message);

        return oferta.Value!;
    }

    private static ConfiguracaoDistribuicaoVagas OfertaDaLei12711(Guid ofertaCursoId)
    {
        DistribuicaoDeVagasDeclarada declarada = new()
        {
            VagasOfertadasBase = 50,
            PercentualDaReservaLegal = 0.5m,
            RegraDeDistribuicao = ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Lei12711.Codigo(), "v1", new string('a', 64)),
            RegraDeAjuste = ReferenciaRegra.De(RegraDeAjusteDaDistribuicaoDeVagas.AjusteDoTotalPeloArtigo11.Codigo(), "v1", new string('d', 64)),
            ReferenciaDemografica = ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022 (IBGE)").Value!,
            Modalidades = ModalidadesDaLei12711(),
        };
        Result<ConfiguracaoDistribuicaoVagas> oferta = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoId, declarada, new FatosDaOfertaNoCadastro { GrupoAreaEnem = ("HUMANISTICA_I", "Humanística I"), OfertaCurso = OfertaCursoDeTeste.Medicina });
        oferta.IsSuccess.Should().BeTrue(oferta.Error?.Message);

        return oferta.Value!;
    }

    /// <summary>A Lei 12.711 exige as 8 modalidades federais + AC. "V" (PcD retirada da AC) é uma 10ª modalidade além das obrigatórias.</summary>
    private static List<ModalidadeSelecionada> ModalidadesDaLei12711() =>
    [
        .. ModalidadesFederaisLei12711.Codigos.Select(codigo =>
            NovaModalidade(codigo, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, regraRemanejamento: RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento)),
        NovaModalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas),
        NovaModalidade(
            "V", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade,
            composicaoOrigemCodigo: "AC", quantidadeDeclarada: 2, regraDeAplicabilidade: RegraDaReservaPcd),
    ];

    private async Task<ProcessoSeletivo> RecarregarComDistribuicaoAsync(Guid processoId)
    {
        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.VagasOfertadas)
            .FirstOrDefaultAsync(p => p.Id == processoId, CancellationToken.None);

        recarregado.Should().NotBeNull();
        return recarregado!;
    }

    /// <summary>As colunas não têm CHECK de "ambas ou nenhuma": uma correção de dados pode anular só uma delas.</summary>
    private async Task AnularAColunaDoGrupoAsync(string coluna, Guid processoId)
    {
        string anular = coluna switch
        {
            "grupo_area_enem_codigo" => "UPDATE selecao.configuracoes_de_vagas SET grupo_area_enem_codigo = NULL WHERE processo_seletivo_id = {0}",
            _ => "UPDATE selecao.configuracoes_de_vagas SET grupo_area_enem_rotulo = NULL WHERE processo_seletivo_id = {0}",
        };
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        await context.Database.ExecuteSqlRawAsync(anular, processoId);
    }

    private async Task RedefinirADistribuicaoAsync(Guid processoId, Guid ofertaCursoId)
    {
        await using UniPlusDbContext configureContext = _fixture.CreateDbContext();
        ProcessoSeletivo carregado = (await ProcessoSeletivoRepositoryDeTeste.Criar(configureContext).ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
        ConfiguracaoDistribuicaoVagas nova = OfertaInstitucional(
            ofertaCursoId, 45, 'c', [NovaAcaoAfirmativa("QUIL", 20), NovaAcaoAfirmativa("IND", 25)]);

        carregado.DefinirDistribuicaoVagas([nova], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();

        await configureContext.SaveChangesAsync(CancellationToken.None);
    }

    private static void ConferirAIdentidadeEAsRegras(ConfiguracaoDistribuicaoVagas distribuicao, Guid ofertaCursoId)
    {
        distribuicao.OfertaCursoOrigemId.Should().Be(ofertaCursoId);
        distribuicao.GrupoAreaEnem.Should().NotBeNull();
        distribuicao.GrupoAreaEnem!.Codigo.Should().Be("HUMANISTICA_I");
        distribuicao.GrupoAreaEnem.Rotulo.Should().Be("Humanística I");
        distribuicao.VagasOfertadasBase.Should().Be(50);
        distribuicao.PercentualDaReservaLegal.Should().Be(0.5m);
        distribuicao.RegraDistribuicao.Codigo.Should().Be(RegraDeDistribuicaoDeVagas.Lei12711.Codigo());
        distribuicao.RegraDistribuicao.Hash.Should().Be(new string('a', 64));
        distribuicao.RegraAjuste.Should().NotBeNull();
        distribuicao.RegraAjuste!.Codigo.Should().Be(RegraDeAjusteDaDistribuicaoDeVagas.AjusteDoTotalPeloArtigo11.Codigo());
    }

    private static void ConferirAReferenciaDemografica(ConfiguracaoDistribuicaoVagas distribuicao)
    {
        distribuicao.ReferenciaDemografica.Should().NotBeNull();
        distribuicao.ReferenciaDemografica!.CensoReferencia.Should().Be("2022");
        distribuicao.ReferenciaDemografica.PpiPercentual.Should().Be(79m);
    }

    private static void ConferirAsModalidadesEOQuadro(ConfiguracaoDistribuicaoVagas distribuicao)
    {
        distribuicao.Modalidades.Should().HaveCount(10);
        ModalidadeSelecionada retiradaDeOutraModalidade = distribuicao.Modalidades.Single(m => m.Codigo == "V");
        retiradaDeOutraModalidade.ComposicaoVagas.Should().Be(ComposicaoVagasModalidade.RetiradaDeOutraModalidade);
        retiradaDeOutraModalidade.ComposicaoOrigemCodigo.Should().Be("AC");
        retiradaDeOutraModalidade.RegraDeAplicabilidade.MesmoConteudo(RegraDaReservaPcd).Should().BeTrue(
            "a regra de aplicabilidade congelada volta do banco como foi gravada");
        retiradaDeOutraModalidade.QuantidadeDeclarada.Should().Be(2);

        ModalidadeSelecionada cotaReservada = distribuicao.Modalidades.Single(m => m.Codigo == "LB_PPI");
        cotaReservada.RegraRemanejamento.Should().Be(RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento);
        cotaReservada.QuantidadeDeclarada.Should().BeNull("sub-reserva federal é calculada, não fixada pelo edital");

        distribuicao.VagasOfertadas.Should().HaveCount(10);
        distribuicao.VagasOfertadas.Single(v => v.ModalidadeCodigo == "V").Quantidade.Should().Be(2);
    }
}

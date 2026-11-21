namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Story #853 — CA-18 (paridade congelado × cadastro vivo) e CA-19 (o passado não muda,
/// congelamento dos parâmetros na publicação, UNI-REQ-0019), contra Postgres real (Testcontainers): a fonte é a MESMA usada pelo gate — repositório
/// real de <see cref="ObrigatoriedadeLegal"/> + <see cref="AvaliadorConformidadeLegal.Avaliar"/>.
/// </summary>
public sealed class ConformidadeLegalCongelamentoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();
    private static readonly DateOnly DataDeCorte = new(2026, 1, 1);

    /// <summary>
    /// A janela da inscrição em horário de Belém — meia-noite local, não UTC. Fosse
    /// <c>TimeSpan.Zero</c>, o dia civil derivado cairia em 31/12/2025 e a conferência
    /// responderia pela vigência do dia anterior (issue #1350).
    /// </summary>
    private static readonly DateTimeOffset InicioDaInscricao =
        InstanteEmBelem.Em(2026, 1, 1);

    private readonly ProcessoSeletivoDbFixture _fixture;

    public ConformidadeLegalCongelamentoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static ReferenciaRegra Regra(string codigo, string hashChar) =>
        ReferenciaRegra.De(codigo, "v1", new string(hashChar[0], 64));

    private static FaseCronograma FaseDeResultadoFinal() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
        bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]).Value!;

    private static ProcessoSeletivo NovoProcessoConforme(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(nome, TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();

        DefinirDistribuicao(processo);
        DefinirClassificacaoECronograma(processo);
        PrepararPublicacao(processo);
        return processo;
    }

    private static void DefinirDistribuicao(ProcessoSeletivo processo)
    {
        ModalidadeSelecionada modalidade = CriarModalidade();
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional, "a"), Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }

    private static ModalidadeSelecionada CriarModalidade() =>
        ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla),
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;

    private static void DefinirClassificacaoECronograma(ProcessoSeletivo processo)
    {
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.ClassificacaoImportada, "b"),
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "c"),
        }).Value!;
        processo.DefinirClassificacao(classificacao).IsSuccess.Should().BeTrue();

        DefinirCronograma(processo);
    }

    private static void DefinirCronograma(ProcessoSeletivo processo)
    {
        FaseCronograma faseConforme = FaseDeResultadoFinal();
        processo.DefinirCronogramaFases([faseConforme], []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();
    }

    private static void PrepararPublicacao(ProcessoSeletivo processo)
    {
        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
    }

    private async Task<Guid> SemearRegraVigenteAsync(string regraCodigo)
    {
        ObrigatoriedadeLegal regra = NovaRegraVigente(regraCodigo);
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        ObrigatoriedadeLegalRepository repository = new(context, TimeProvider.System);
        await repository.AdicionarAsync(regra, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);
        return regra.Id;
    }

    private static ObrigatoriedadeLegal NovaRegraVigente(string regraCodigo) =>
        ObrigatoriedadeLegal.Criar(
            ObrigatoriedadeLegal.TipoProcessoUniversal,
            CategoriaObrigatoriedade.Outros,
            regraCodigo,
            new EtapaObrigatoria("PROVA_OBJETIVA"),
            new TextoNormativo("Etapa objetiva obrigatória (teste de integração)", "Lei de teste", null, null),
            new DateOnly(2020, 1, 1),
            null).Value!;

    private static DadosEdital DadosDoEdital(DocumentoAPublicar documento)
    {
        Result<DadosEdital> dadosResult = DadosEdital.Criar(
            numero: "001/2026",
            periodoInscricaoInicio: InicioDaInscricao,
            periodoInscricaoFim: InstanteEmBelem.Em(2026, 1, 31, 23, 59, 59),
            documentoEditalId: documento.Id);
        dadosResult.IsSuccess.Should().BeTrue();

        return dadosResult.Value!;
    }

    private async Task<ResultadoConformidade> ConformidadeDoProcessoAsync(ProcessoSeletivo processo)
    {
        // Mesma dupla de chamadas do gate real (ConferenciaDeConformidadeLegal): repositório
        // real + AvaliadorConformidadeLegal.Avaliar — a Application injeta isto no handler;
        // aqui, como as demais integrações desta suíte, a orquestração é feita inline.
        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = new(readContext, TimeProvider.System);
        IReadOnlyList<ObrigatoriedadeLegal> vigentes = await obrigatoriedadeLegalRepository
            .ObterVigentesParaTipoProcessoAsync(processo.TipoProcesso.ToString(), DataDeCorte, CancellationToken.None);
        ResultadoConformidade conformidade = AvaliadorConformidadeLegal.Avaliar(processo, processo.TipoProcesso.ToString(), vigentes, IdentidadesDe(processo), FatosDeModalidadeDeTeste.DoCatalogo);
        conformidade.Regras.Should().OnlyContain(static r => r.Aprovada, "pré-condição do teste — o processo satisfaz a regra semeada");

        return conformidade;
    }

    private async Task GravarAsync(ProcessoSeletivo processo, DocumentoAPublicar documento, VersaoConfiguracao versao)
    {
        await using UniPlusDbContext writeContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(writeContext);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await writeContext.DocumentosAPublicar.AddAsync(documento, CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(versao, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<(Guid ProcessoId, Guid SnapshotId)> PublicarComConformidadeLegalAsync(string nome)
    {
        ProcessoSeletivo processo = NovoProcessoConforme(nome);
        DocumentoAPublicar documento = DocumentoConfirmado(processo);
        DadosEdital dados = DadosDoEdital(documento);
        ResultadoConformidade conformidade = await ConformidadeDoProcessoAsync(processo);
        VersaoConfiguracao versao = Publicar(processo, documento, dados, conformidade);
        await GravarAsync(processo, documento, versao);

        return (processo.Id, versao.Id);
    }

    private static DocumentoAPublicar DocumentoConfirmado(ProcessoSeletivo processo)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();

        return documento;
    }

    private static VersaoConfiguracao Publicar(ProcessoSeletivo processo, DocumentoAPublicar documento, DadosEdital dados, ResultadoConformidade conformidade)
    {
        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(processo, dados, documento.HashSha256!, FusoInstitucional.ZoneId, Conformidade: conformidade, Retificacao: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)));

        Result<VersaoConfiguracao> publicarResult = processo.Publicar(
            new CenarioDeCongelamento { Dados = dados, Documento = new DocumentoCongelado(canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash, documento.HashSha256!), AtorUsuarioContaId = "integration-test-user", Contagem = CorpusEnvelopeDadosDoEdital.ContextoRico() });
        publicarResult.IsSuccess.Should().BeTrue(publicarResult.Error?.Message);

        return publicarResult.Value!;
    }

    [Fact(DisplayName = "CA-13/CA-18 — obrigatoriedades[] congela TODAS as regras vigentes e bate, conjunto a conjunto, com o recômputo do cadastro vivo na mesma data de corte")]
    public async Task Congelamento_BateComRecomputoDoCadastroVivo()
    {
        // DUAS regras — não uma — para que a comparação "conjunto a conjunto" (não só um
        // lookup por id) tenha algo real a provar: nenhuma órfã no congelado, nenhuma
        // vigente de fora faltando.
        Guid regraId1 = await SemearRegraVigenteAsync(UniqueRegraCodigo());
        Guid regraId2 = await SemearRegraVigenteAsync(UniqueRegraCodigo());
        (Guid processoId, Guid snapshotId) = await PublicarComConformidadeLegalAsync(nameof(Congelamento_BateComRecomputoDoCadastroVivo));

        (Guid RegraId, string Hash)[] congeladoSet = await LerObrigatoriedadesCongeladasAsync(snapshotId);
        (Guid RegraId, string Hash)[] recomputadoSet = await RecomputarObrigatoriedadesAsync(processoId);
        recomputadoSet.Should().Equal(congeladoSet, "o cadastro vivo deve reproduzir o conjunto congelado");
        congeladoSet.Select(t => t.RegraId).Should().BeEquivalentTo([regraId1, regraId2]);
    }

    private async Task<(Guid RegraId, string Hash)[]> LerObrigatoriedadesCongeladasAsync(Guid snapshotId)
    {
        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        VersaoConfiguracao versao = await readContext.VersoesConfiguracao
            .AsNoTracking().FirstAsync(v => v.Id == snapshotId, CancellationToken.None);

        JsonObject documentosExigidos = JsonNode.Parse(versao.ConfiguracaoCongelada)!["documentosExigidos"]!.AsObject();
        JsonArray obrigatoriedadesCongeladas = documentosExigidos["obrigatoriedades"]!.AsArray();
        obrigatoriedadesCongeladas.Should().HaveCount(2);
        obrigatoriedadesCongeladas.Should().OnlyContain(o => o!["aprovada"]!.GetValue<bool>());

        (Guid RegraId, string Hash)[] congeladoSet = [.. obrigatoriedadesCongeladas
            .Select(o => (o!["regraId"]!.GetValue<Guid>(), o["hash"]!.GetValue<string>()))
            .OrderBy(t => t.Item1)];

        return congeladoSet;
    }

    private async Task<(Guid RegraId, string Hash)[]> RecomputarObrigatoriedadesAsync(Guid processoId)
    {
        await using UniPlusDbContext freshReadContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository processoRepository = ProcessoSeletivoRepositoryDeTeste.Criar(freshReadContext);
        ObrigatoriedadeLegalRepository freshRepository = new(freshReadContext, TimeProvider.System);

        ProcessoSeletivo processoVivo = (await processoRepository.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
        IReadOnlyList<ObrigatoriedadeLegal> vigentesAgora = await freshRepository
            .ObterVigentesParaTipoProcessoAsync(processoVivo.TipoProcesso.ToString(), DataDeCorte, CancellationToken.None);
        ResultadoConformidade recomputado = AvaliadorConformidadeLegal.Avaliar(
            processoVivo, processoVivo.TipoProcesso.ToString(), vigentesAgora, IdentidadesDe(processoVivo), FatosDeModalidadeDeTeste.DoCatalogo);

        return [.. recomputado.Regras.Select(r => (r.RegraId, r.Hash)).OrderBy(t => t.Item1)];
    }

    [Fact(DisplayName = "CA-19 (congelamento dos parâmetros na publicação, UNI-REQ-0019 — o passado não muda) — desativar a regra depois da publicação não altera o congelado nem o hash")]
    public async Task DesativarRegraAposPublicacao_NaoAlteraOCongeladoNemOHash()
    {
        await SemearRegraVigenteAsync(UniqueRegraCodigo());
        (_, Guid snapshotId) = await PublicarComConformidadeLegalAsync(nameof(DesativarRegraAposPublicacao_NaoAlteraOCongeladoNemOHash));

        (string configuracaoAntes, string hashAntes) = await LerConfiguracaoAsync(snapshotId);
        await DesativarRegrasAsync();
        await ConferirDesativacaoAsync();
        (string configuracaoDepois, string hashDepois) = await LerConfiguracaoAsync(snapshotId);
        configuracaoDepois.Should().Be(configuracaoAntes, "a desativação não altera a configuração publicada");
        hashDepois.Should().Be(hashAntes, "o hash depende do conteúdo congelado");
    }

    private async Task<(string Configuracao, string Hash)> LerConfiguracaoAsync(Guid snapshotId)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        VersaoConfiguracao versao = await context.VersoesConfiguracao.AsNoTracking()
            .FirstAsync(v => v.Id == snapshotId, CancellationToken.None);
        return (versao.ConfiguracaoCongelada, versao.HashConfiguracao);
    }

    private async Task DesativarRegrasAsync()
    {
        // O CEPS desativa a obrigatoriedade no cadastro — a mesma operação de sempre, sem
        // saber (nem precisar saber) que já existe um congelamento que a usou.
        await using UniPlusDbContext escritaContext = _fixture.CreateDbContext();
        await escritaContext.Database.ExecuteSqlAsync(
            $"UPDATE selecao.obrigatoriedades_legais SET is_deleted = true WHERE regra_codigo LIKE 'TEST\\_%' ESCAPE '\\'");
    }

    private async Task ConferirDesativacaoAsync()
    {
        // Uma reavaliação AO VIVO, na MESMA data de corte, já não veria a regra — prova que a
        // desativação teve efeito real no cadastro, e não que o teste está inerte.
        await using UniPlusDbContext posContext = _fixture.CreateDbContext();
        ObrigatoriedadeLegalRepository posRepository = new(posContext, TimeProvider.System);
        IReadOnlyList<ObrigatoriedadeLegal> vigentesDepoisDaDesativacao = await posRepository
            .ObterVigentesParaTipoProcessoAsync(TiposDeProcessoDeTeste.SiSU.ToString(), DataDeCorte, CancellationToken.None);
        vigentesDepoisDaDesativacao.Should().BeEmpty(
            "pré-condição do teste — a desativação precisa ter efeito real no cadastro vivo");
    }

    private static string UniqueRegraCodigo() => $"TEST_GATE_{Guid.CreateVersion7():N}";

    /// <summary>Identidades derivadas das próprias exigências — estado sem renomeação nem reciclagem.</summary>
    /// <summary>
    /// Cadastro vivo deduzido do próprio processo: cada código resolve para a identidade
    /// que o processo congelou — o caminho feliz, em que regra e processo apontam para o
    /// mesmo item de catálogo.
    /// </summary>
    private static IdentidadesDeCadastro IdentidadesDe(ProcessoSeletivo processo) =>
        new(
            MapaDe(processo.DocumentosExigidos, e => e.TipoDocumentoCodigo, e => e.TipoDocumentoOrigemId),
            MapaDe(
                processo.DistribuicaoVagas.SelectMany(d => d.Modalidades),
                m => m.Codigo,
                m => m.ModalidadeOrigemId),
            MapaDe(processo.Etapas, e => e.TipoEtapa.Codigo, e => e.TipoEtapa.OrigemId),
            MapaDe(
                processo.AtendimentoEspecializado?.TiposDeficiencia ?? [],
                t => t.TipoDeficienciaCodigo,
                t => t.TipoDeficienciaOrigemId));

    private static Dictionary<string, Guid> MapaDe<T>(
        IEnumerable<T> itens,
        Func<T, string> codigoDe,
        Func<T, Guid> origemDe) =>
        itens
            .GroupBy(codigoDe, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => origemDe(g.First()), StringComparer.Ordinal);

}

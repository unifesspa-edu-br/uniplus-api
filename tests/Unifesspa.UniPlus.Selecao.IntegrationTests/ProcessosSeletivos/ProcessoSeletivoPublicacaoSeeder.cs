namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.ValueObjects;
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
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Semeia um Processo Seletivo conforme e o publica pelo caminho real do
/// agregado (Publicar → <see cref="VersaoConfiguracao"/>), persistindo tudo numa
/// transação. É o ponto de partida dos testes que precisam de um certame já
/// publicado para exercitar o que vem DEPOIS — em especial os que atacam a tabela
/// de versões com SQL cru.
/// </summary>
internal static class ProcessoSeletivoPublicacaoSeeder
{
    private static readonly string HashDocumento = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    /// <summary>
    /// O que a publicação produz: o certame, o id do ATO que criou a versão (decidido pela
    /// raiz; o ato em si é registrado depois, em Publicações — ADR-0108) e a versão congelada.
    /// </summary>
    internal sealed record Resultado(Guid ProcessoId, Guid AtoId, Guid VersaoId);

    private static ReferenciaRegra Regra(string codigo)
    {
        RegraCatalogoSeedItem regra = RegraCatalogoSeed.Itens.Single(r => r.Codigo == codigo && r.Versao == "v1");
        return ReferenciaRegra.De(codigo, regra.Versao, regra.ComputarHash());
    }

    /// <summary>
    /// Processo com as quatro dimensões estruturalmente obrigatórias
    /// preenchidas (etapas, atendimento, distribuição de vagas, classificação)
    /// — o mínimo que <c>AvaliarConformidade</c> exige para publicar.
    /// </summary>
    public static ProcessoSeletivo NovoProcessoConforme(string nome, DateTimeOffset? inicioDaFase = null, FaseCronograma? fase = null)
    {
        ProcessoSeletivo processo = ProcessoVazio(nome);
        DefinirAtendimentoEspecializadoVazio(processo);
        processo.DefinirDistribuicaoVagas([DistribuicaoDaModalidadeAmpla()], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        DefinirClassificacaoImportadaEFaseDeResultadoFinal(processo, inicioDaFase, fase);

        DefinirTaxaEFormularioDeInscricao(processo);
        return processo;
    }

    // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
    private static void DefinirTaxaEFormularioDeInscricao(ProcessoSeletivo processo)
    {
        DefinirTaxaInscricaoNaoCobra(processo);
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
    }

    private static ProcessoSeletivo ProcessoVazio(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(nome, TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        return processo;
    }

    private static void DefinirAtendimentoEspecializadoVazio(ProcessoSeletivo processo) =>
        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();

    private static ModalidadeSelecionada ModalidadeAmplaDeTeste() => ModalidadeSelecionada.Criar(
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

    private static ConfiguracaoDistribuicaoVagas DistribuicaoDaModalidadeAmpla() => ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
        new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional), Modalidades = [ModalidadeAmplaDeTeste()] },
        new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    private static void DefinirClassificacaoImportadaEFaseDeResultadoFinal(ProcessoSeletivo processo, DateTimeOffset? inicioDaFase = null, FaseCronograma? fase = null)
    {
        DefinirClassificacaoImportada(processo);
        DefinirFaseDeResultadoFinalComProvaObjetiva(processo, inicioDaFase, fase);
    }

    private static void DefinirClassificacaoImportada(ProcessoSeletivo processo)
    {
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.ClassificacaoImportada),
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria),
        }).Value!;
        processo.DefinirClassificacao(classificacao).IsSuccess.Should().BeTrue();
    }

    private static void DefinirFaseDeResultadoFinalComProvaObjetiva(ProcessoSeletivo processo, DateTimeOffset? inicioDaFase = null, FaseCronograma? fase = null)
    {
        processo.DefinirCronogramaFases([fase ?? FaseDeResultadoFinal(inicioDaFase)], []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([ProvaObjetivaDoResultadoFinal()]).IsSuccess.Should().BeTrue();
    }

    private static FaseCronograma FaseDeResultadoFinal(DateTimeOffset? inicioDaFase) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: inicioDaFase ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: inicioDaFase?.AddDays(30) ?? new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            FaseConcluinteCodigo: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
        bancasRequeridas: []).Value!;

    private static EtapaProcesso ProvaObjetivaDoResultadoFinal() => EtapaProcesso.Criar(
        new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
        new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    /// <summary>
    /// Não cobra taxa — declaração padrão dos seeders desta classe, que representam processos
    /// publicáveis e não testam a dimensão de taxa em si (issue #1112, CA-01).
    /// </summary>
    private static void DefinirTaxaInscricaoNaoCobra(ProcessoSeletivo processo)
    {
        Result<ConfiguracaoTaxaInscricao> taxaResult = ConfiguracaoTaxaInscricao.Criar(
            cobra: false, valor: null, fundamentosCodigos: null);
        taxaResult.IsSuccess.Should().BeTrue(taxaResult.Error?.Message);
        processo.DefinirTaxaInscricao(taxaResult.Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
        CriterioDesempate criterio = CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorIdade), new ParametrosDesempateMaiorIdade()).Value!;
        processo.DefinirCriteriosDesempate([criterio]).IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Processo com a oferta federal completa (Lei 12.711: as 8 modalidades federais +
    /// AC, referência demográfica, regra de ajuste) e, por padrão, a cascata de
    /// remanejamento que a cobre — a matriz legal completa (8×7, fallback AC), a mesma
    /// forma semeada em <c>REMANEJ-CASCATA-LEI-12711 v1</c>. É o processo mínimo que
    /// <c>PendenciaDaCascata</c> considera conforme quando a oferta usa o regime federal
    /// (Story #575) — as demais quatro dimensões estruturais (etapas, atendimento,
    /// classificação, cronograma) são as mesmas de <see cref="NovoProcessoConforme"/>.
    /// </summary>
    /// <param name="comCascata">
    /// <see langword="false"/> devolve o processo SEM a cascata — a oferta federal continua
    /// exigindo-a (as 8 modalidades são <c>SegueCascata</c>), então este processo não é
    /// publicável até que a cascata seja definida à parte. Usado pelos testes de
    /// concorrência que racionam <c>DefinirCascataRemanejamentoCommand</c> contra
    /// <c>PublicarProcessoSeletivoCommand</c> — o padrão <see langword="true"/> preserva o
    /// comportamento anterior para os demais chamadores.
    /// </param>
    public static ProcessoSeletivo NovoProcessoComOfertaFederalECascata(string nome, bool comCascata = true)
    {
        ProcessoSeletivo processo = ProcessoVazio(nome);
        DefinirAtendimentoEspecializadoVazio(processo);
        processo.DefinirDistribuicaoVagas([OfertaFederalCompleta()], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        DefinirClassificacaoImportadaEFaseDeResultadoFinal(processo);

        if (comCascata)
        {
            processo.DefinirCascataRemanejamento(CascataLegalCompleta()).IsSuccess.Should().BeTrue();
        }

        DefinirTaxaEFormularioDeInscricao(processo);
        RegrasDeAplicabilidadeDeTeste.DerivarOQueAOpcaoPelasCotasCita(processo);

        return processo;
    }

    private static ConfiguracaoDistribuicaoVagas OfertaFederalCompleta()
    {
        IReadOnlyList<ModalidadeSelecionada> modalidades = ModalidadesDaOfertaFederal();

        return ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada
            {
                VagasOfertadasBase = 50,
                PercentualDaReservaLegal = 0.5m,
                RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Lei12711),
                RegraDeAjuste = Regra(RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico),
                ReferenciaDemografica = ReferenciaReservaDemograficaSnapshot.Criar(
                Guid.CreateVersion7(), "Censo IBGE 2022", 78.55m, 1.20m, 8.40m, "Lei 12.711/2012 art. 3º").Value!,
                Modalidades = modalidades
            },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
    }

    private static List<ModalidadeSelecionada> ModalidadesFederais()
    {
        List<ModalidadeSelecionada> modalidades = [ModalidadeAmplaFederal()];
        modalidades.AddRange(ModalidadesFederaisLei12711.Codigos.Select(ModalidadeFederal));
        return modalidades;
    }

    private static ModalidadeSelecionada ModalidadeAmplaFederal() => ModalidadeSelecionada.Criar(
        Guid.CreateVersion7(), "AC", "Ampla concorrência",
        NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, null,
        RegraRemanejamentoModalidade.Nenhuma, null, null, null,
        regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), acaoQuandoIndeferido: null,
        baseLegal: "Lei 12.711/2012 art. 1º").Value!;

    private static ModalidadeSelecionada ModalidadeFederal(string codigo) => ModalidadeSelecionada.Criar(
        Guid.CreateVersion7(), codigo, $"Reserva {codigo}",
        NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, null,
        RegraRemanejamentoModalidade.SegueCascata, null, null, null,
        regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.CotaReservada), acaoQuandoIndeferido: "RECLASSIFICAR_AC",
        baseLegal: "Lei 12.711/2012, alterada pela Lei 14.723/2023").Value!;

    /// <summary>A matriz legal completa (8×7), fallback AC — mesma forma semeada em REMANEJ-CASCATA-LEI-12711 v1.</summary>
    internal static ConfiguracaoCascataRemanejamento CascataLegalCompleta(char semente = 'f')
    {
        IReadOnlyList<string> origens = ModalidadesFederaisLei12711.Codigos;
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in origens)
        {
            string[] destinosDaOrigem = [.. origens.Where(o => o != origem)];
            for (int i = 0; i < destinosDaOrigem.Length; i++)
            {
                destinos.Add(DestinoRemanejamento.Criar(origem, i + 1, destinosDaOrigem[i]).Value!);
            }
        }

        return ConfiguracaoCascataRemanejamento.Criar(
            ReferenciaRegra.De(RegraRemanejamentoCodigo.Cascata, "v1", new string(semente, 64)), ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    /// <summary>
    /// Publica o processo pelo agregado e persiste raiz, documento e versão 1
    /// da configuração — o mesmo caminho do handler, sem a pipeline HTTP.
    /// </summary>
    public static async Task<Resultado> PublicarAsync(ProcessoSeletivoDbFixture fixture, string nome, DateTimeOffset? inicioDaFase = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        ProcessoSeletivo processo = NovoProcessoConforme(nome, inicioDaFase);
        DocumentoAPublicar documento = DocumentoDoEditalConfirmado(processo);
        VersaoConfiguracao versao = PublicarProcesso(processo, documento);

        await PersistirAsync(fixture, processo, documento, versao);

        return new Resultado(processo.Id, versao.AtoCriadorId, versao.Id);
    }

    private static DocumentoAPublicar DocumentoDoEditalConfirmado(ProcessoSeletivo processo)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, HashDocumento, TimeProvider.System).IsSuccess.Should().BeTrue();

        return documento;
    }

    private static VersaoConfiguracao PublicarProcesso(ProcessoSeletivo processo, DocumentoAPublicar documento)
    {
        DadosEdital dados = DadosDoEdital(documento);
        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(
            processo, dados, documento.HashSha256!, FusoInstitucional.ZoneId,
            Retificacao: null, Conformidade: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo),
            FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)));

        Result<VersaoConfiguracao> publicarResult = processo.Publicar(
            new CenarioDeCongelamento { Dados = dados, Documento = new DocumentoCongelado(canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash, documento.HashSha256!), AtorUsuarioContaId = "integration-test-user", Contagem = CorpusEnvelopeDadosDoEdital.ContextoRico() });
        publicarResult.IsSuccess.Should().BeTrue(publicarResult.Error?.Message);

        return publicarResult.Value!;
    }

    private static DadosEdital DadosDoEdital(DocumentoAPublicar documento) => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        documentoEditalId: documento.Id).Value!;

    private static async Task PersistirAsync(
        ProcessoSeletivoDbFixture fixture, ProcessoSeletivo processo, DocumentoAPublicar documento, VersaoConfiguracao versao)
    {
        await using UniPlusDbContext context = fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(context);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await context.DocumentosAPublicar.AddAsync(documento, CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(versao, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    private static ModalidadeSelecionada AmplaDaOfertaFederal() => ModalidadeSelecionada.Criar(
                Guid.CreateVersion7(), "AC", "Ampla concorrência",
                NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, null,
                RegraRemanejamentoModalidade.Nenhuma, null, null, null,
                regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), acaoQuandoIndeferido: null,
                baseLegal: "Lei 12.711/2012 art. 1º").Value!;


    private static ModalidadeSelecionada CotaDaOfertaFederal(string codigo) => ModalidadeSelecionada.Criar(
                Guid.CreateVersion7(), codigo, $"Reserva {codigo}",
                NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, null,
                RegraRemanejamentoModalidade.SegueCascata, null, null, null,
                regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.CotaReservada), acaoQuandoIndeferido: "RECLASSIFICAR_AC",
                baseLegal: "Lei 12.711/2012, alterada pela Lei 14.723/2023").Value!;


    private static IReadOnlyList<ModalidadeSelecionada> ModalidadesDaOfertaFederal() =>
    [
        AmplaDaOfertaFederal(),
        .. ModalidadesFederaisLei12711.Codigos.Select(CotaDaOfertaFederal),
    ];

}

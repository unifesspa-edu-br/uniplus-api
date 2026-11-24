namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
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
/// Montagem compartilhada dos testes de persistência da retificação (Postgres real): o processo
/// conforme, a publicação da abertura e a retificação por ato que emenda a versão corrente.
/// </summary>
internal sealed class CenarioDeRetificacaoPersistida
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    private readonly ProcessoSeletivoDbFixture _fixture;

    public CenarioDeRetificacaoPersistida(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    public UniPlusDbContext CriarDbContext() => _fixture.CreateDbContext();

    private static ReferenciaRegra Regra(string codigo, string hashChar) =>
        ReferenciaRegra.De(codigo, "v1", new string(hashChar[0], 64));

    public static ProcessoSeletivo NovoProcessoConforme(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(nome, TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        DefinirCronogramaEAtendimento(processo);
        DefinirVagas(processo);
        DefinirClassificacao(processo);
        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }

    private static void DefinirCronogramaEAtendimento(ProcessoSeletivo processo)
    {
        processo.DefinirCronogramaFases([FaseConforme()], []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();
    }

    private static void DefinirVagas(ProcessoSeletivo processo)
    {
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas([ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional, "a"), Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!], FatosDeModalidadeDeTeste.DoCatalogo)
            .IsSuccess.Should().BeTrue();
    }

    private static void DefinirClassificacao(ProcessoSeletivo processo)
    {
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.ClassificacaoImportada, "b"),
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "c"),
        }).Value!).IsSuccess.Should().BeTrue();
    }

    private static FaseCronograma FaseConforme() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
        bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]).Value!;

    public static DocumentoAPublicar DocumentoConfirmado(Guid processoId)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    public static DadosEdital NovosDados(Guid documentoId) => DadosEdital.Criar(
        "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), documentoId).Value!;

    public static async Task<VersaoConfiguracao> LerVersaoAsync(UniPlusDbContext leitura, Guid versaoId) =>
        await leitura.VersoesConfiguracao.AsNoTracking().FirstAsync(v => v.Id == versaoId, CancellationToken.None);

    /// <summary>
    /// Publica o processo e persiste o processo, o documento do edital e a versão da abertura.
    /// </summary>
    public Task<VersaoConfiguracao> PublicarEPersistirAsync(ProcessoSeletivo processo) =>
        PublicarEPersistirAsync(processo, TimeProvider.System);

    private async Task<VersaoConfiguracao> PublicarEPersistirAsync(ProcessoSeletivo processo, TimeProvider relogio)
    {
        DocumentoAPublicar documento = DocumentoConfirmado(processo.Id);
        VersaoConfiguracao abertura = Publicar(processo, documento, relogio);

        await using UniPlusDbContext escrita = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(escrita);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await escrita.DocumentosAPublicar.AddAsync(documento, CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(abertura, CancellationToken.None);
        await escrita.SaveChangesAsync(CancellationToken.None);
        return abertura;
    }

    private static VersaoConfiguracao Publicar(ProcessoSeletivo processo, DocumentoAPublicar documento, TimeProvider relogio)
    {
        DadosEdital dados = NovosDados(documento.Id);
        SnapshotCanonico canonico = Canonicalizar(processo, dados, documento);
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            new CenarioDeCongelamento { Dados = dados, Documento = new DocumentoCongelado(canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash, documento.HashSha256!), AtorUsuarioContaId = "integration-test-user", Relogio = relogio, Contagem = ExemploDeEnvelopeDadosDoEdital.ContextoRico() });
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        return publicar.Value!;
    }

    private static SnapshotCanonico Canonicalizar(ProcessoSeletivo processo, DadosEdital dados, DocumentoAPublicar documento) =>
        Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dados, documento.HashSha256!, FusoInstitucional.ZoneId, Retificacao: null, Conformidade: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)));

    /// <summary>
    /// Publica um processo e o retifica em seguida — persistindo as duas versões da
    /// configuração. Usa um relógio manual para dar instantes distintos à abertura e à
    /// retificação, como o certame real faz; a ordem, porém, vem da cadeia de versões,
    /// não desses instantes.
    /// </summary>
    public async Task<(ProcessoSeletivo Processo, VersaoConfiguracao VersaoAbertura, VersaoConfiguracao VersaoRetificacao)>
        PublicarERetificarAsync(string nome)
    {
        RelogioManual relogio = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        ProcessoSeletivo processo = NovoProcessoConforme(nome);
        VersaoConfiguracao abertura = await PublicarEPersistirAsync(processo, relogio);

        relogio.Avancar(TimeSpan.FromDays(1));
        VersaoConfiguracao retificacao = await RetificarAsync(processo.Id, relogio);
        return (processo, abertura, retificacao);
    }

    private async Task<VersaoConfiguracao> RetificarAsync(Guid processoId, TimeProvider relogio)
    {
        // Recarrega o agregado tracked — como o handler faz.
        DocumentoAPublicar documento = DocumentoConfirmado(processoId);
        await using UniPlusDbContext escrita = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(escrita);
        ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
        VersaoConfiguracao versaoAtual = (await repository.ObterVersaoAtualAsync(processoId, CancellationToken.None))!;
        VersaoConfiguracao retificacao = Retificar(carregado, versaoAtual, documento, relogio);

        await escrita.DocumentosAPublicar.AddAsync(documento, CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(retificacao, CancellationToken.None);
        await escrita.SaveChangesAsync(CancellationToken.None);
        return retificacao;
    }

    private static VersaoConfiguracao Retificar(
        ProcessoSeletivo carregado, VersaoConfiguracao versaoAtual, DocumentoAPublicar documento, TimeProvider relogio)
    {
        DadosEdital dados = NovosDados(documento.Id);
        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(
            carregado, dados, documento.HashSha256!, FusoInstitucional.ZoneId,
            new RetificacaoDoEdital(versaoAtual.AtoCriadorId, "Correção do prazo de inscrição"), null,
            new DadosDeCatalogoDaConfiguracaoPublicada(
                ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(),
                TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(carregado),
                FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)));
        Result<VersaoConfiguracao> retificar = carregado.Retificar(
            new CenarioDeCongelamento { Dados = dados, Documento = new DocumentoCongelado(canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash, documento.HashSha256!), AtorUsuarioContaId = "integration-test-user", Relogio = relogio, Contagem = ExemploDeEnvelopeDadosDoEdital.ContextoRico() }, versaoAtual, "Correção do prazo de inscrição");
        retificar.IsSuccess.Should().BeTrue(retificar.Error?.Message);
        return retificar.Value!;
    }

    private sealed class RelogioManual(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _agora = inicio;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan delta) => _agora = _agora.Add(delta);
    }
}

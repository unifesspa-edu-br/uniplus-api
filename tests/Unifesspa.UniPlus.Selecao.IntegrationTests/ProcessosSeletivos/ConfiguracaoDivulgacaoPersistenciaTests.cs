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
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) da persistência EF de
/// <see cref="ConfiguracaoDivulgacao"/> (UNI-REQ-0050, issue #563) — o que só o banco real
/// prova: o <c>Include</c> de <see cref="ProcessoSeletivoRepository"/>, o cascade, a
/// substituição sem resíduo (CA-05) e a estabilidade do <c>ValueComparer</c> do jsonb.
/// </summary>
public sealed class ConfiguracaoDivulgacaoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public ConfiguracaoDivulgacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static ProcessoSeletivo NovoProcesso(string nome) => ProcessoSeletivo.Criar(
        nome, TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

    [Fact(DisplayName = "Divulgacao_SobreviveASaveChangesEReload — a configuração persiste via Include e sobrevive a um reload")]
    public async Task Divulgacao_SobreviveASaveChangesEReload()
    {
        ProcessoSeletivo processo = NovoProcesso(nameof(Divulgacao_SobreviveASaveChangesEReload));
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome"], "Transparência do resultado — decisão CEPS 12/2026.").Value!).IsSuccess.Should().BeTrue();

        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(writeContext);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(readContext);
        ProcessoSeletivo relido = (await leitura.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

        relido.ConfiguracaoDivulgacao.Should().NotBeNull(
            "a configuração sobrevive ao SaveChanges e ao reload — sem o Include em ComConfiguracao ela nasceria " +
            "null em todo carregamento novo do agregado");
        relido.ConfiguracaoDivulgacao!.ProcessoSeletivoId.Should().Be(processo.Id);
        relido.ConfiguracaoDivulgacao.CamposPublicos.Should().Equal("nome", "numero_inscricao");
        relido.ConfiguracaoDivulgacao.Justificativa.Should().Be("Transparência do resultado — decisão CEPS 12/2026.");
    }

    [Fact(DisplayName = "CA-05: DefinirConfiguracaoDivulgacao duas vezes deixa UMA linha em configuracoes_divulgacao, com os valores da segunda")]
    public async Task Divulgacao_SubstituidaDuasVezes_UmaLinhaComOsValoresDaSegunda()
    {
        ProcessoSeletivo processo = NovoProcesso(nameof(Divulgacao_SubstituidaDuasVezes_UmaLinhaComOsValoresDaSegunda));
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome_abreviado"], null).Value!).IsSuccess.Should().BeTrue();
        Guid idPrimeiraVersao = processo.ConfiguracaoDivulgacao!.Id;

        Guid processoId = processo.Id;
        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(writeContext);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        // Segunda gravação: substitui a configuração inteira por OUTRA — conteúdo
        // diferente (agora com "nome" e justificativa, sem "nome_abreviado").
        await using (UniPlusDbContext sessao = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(sessao);
            ProcessoSeletivo tracked = (await repository.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
            tracked.DefinirConfiguracaoDivulgacao(
                ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome"], "Justificativa da segunda versão.").Value!).IsSuccess.Should().BeTrue();
            await sessao.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(readContext);
        ProcessoSeletivo relido = (await leitura.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;

        relido.ConfiguracaoDivulgacao.Should().NotBeNull();
        relido.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal("nome", "numero_inscricao");
        relido.ConfiguracaoDivulgacao.Justificativa.Should().Be("Justificativa da segunda versão.");

        int linhas = await readContext.Set<ConfiguracaoDivulgacao>()
            .CountAsync(c => c.ProcessoSeletivoId == processoId, CancellationToken.None);
        linhas.Should().Be(1, "substituir a configuração inteira via DefinirConfiguracaoDivulgacao não pode deixar a primeira versão como resíduo — é UMA linha, com os valores da mutação mais recente");

        bool primeiraVersaoAindaExiste = await readContext.Set<ConfiguracaoDivulgacao>()
            .AnyAsync(c => c.Id == idPrimeiraVersao, CancellationToken.None);
        primeiraVersaoAindaExiste.Should().BeFalse("a primeira versão não pode sobreviver à substituição");
    }

    [Fact(DisplayName = "Divulgacao_Removida_SomeDaTabela — DefinirConfiguracaoDivulgacao(null) remove a linha, sem resíduo")]
    public async Task Divulgacao_Removida_SomeDaTabela()
    {
        ProcessoSeletivo processo = NovoProcesso(nameof(Divulgacao_Removida_SomeDaTabela));
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome_abreviado"], null).Value!).IsSuccess.Should().BeTrue();
        Guid idConfiguracao = processo.ConfiguracaoDivulgacao!.Id;

        Guid processoId = processo.Id;
        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(writeContext);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (UniPlusDbContext sessao = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(sessao);
            ProcessoSeletivo tracked = (await repository.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
            tracked.ConfiguracaoDivulgacao.Should().NotBeNull("pré-condição: a configuração persistiu na sessão anterior");
            tracked.DefinirConfiguracaoDivulgacao(null).IsSuccess.Should().BeTrue();
            await sessao.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(readContext);
        ProcessoSeletivo relido = (await leitura.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;

        relido.ConfiguracaoDivulgacao.Should().BeNull(
            "DefinirConfiguracaoDivulgacao(null) remove a configuração — o toggle 'sem configuração explícita' é a própria ausência da entidade");

        bool aindaExiste = await readContext.Set<ConfiguracaoDivulgacao>()
            .AnyAsync(c => c.Id == idConfiguracao, CancellationToken.None);
        aindaExiste.Should().BeFalse("a linha não pode sobrar em configuracoes_divulgacao");
    }

    [Fact(DisplayName = "Duas requisições equivalentes em ordem diferente persistem os campos públicos na MESMA ordem canônica")]
    public async Task Divulgacao_RequisicoesEquivalentes_PersistemNaMesmaOrdem()
    {
        // DefinirConfiguracaoDivulgacao substitui a entidade inteira a cada chamada (mesmo molde
        // de DefinirBonusRegional/DefinirCascataRemanejamento: Id novo por Criar, nunca mutação
        // em local da mesma linha) — então duas chamadas nunca geram um UPDATE na MESMA linha, e
        // sim delete-e-insere. O que a ordem canônica garante aqui não é evitar um UPDATE
        // espúrio (não há UPDATE possível neste desenho), e sim que os BYTES persistidos nunca
        // divirjam do que o envelope emitiria para o mesmo conjunto — não importa em que ordem o
        // cliente o enviou.
        ProcessoSeletivo processoA = NovoProcesso($"{nameof(Divulgacao_RequisicoesEquivalentes_PersistemNaMesmaOrdem)}-A");
        processoA.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["nome_abreviado", "numero_inscricao"], null).Value!).IsSuccess.Should().BeTrue();

        ProcessoSeletivo processoB = NovoProcesso($"{nameof(Divulgacao_RequisicoesEquivalentes_PersistemNaMesmaOrdem)}-B");
        processoB.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome_abreviado"], null).Value!).IsSuccess.Should().BeTrue();

        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(writeContext);
            await repository.AdicionarAsync(processoA, CancellationToken.None);
            await repository.AdicionarAsync(processoB, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(readContext);
        ProcessoSeletivo relidoA = (await leitura.ObterComConfiguracaoAsync(processoA.Id, CancellationToken.None))!;
        ProcessoSeletivo relidoB = (await leitura.ObterComConfiguracaoAsync(processoB.Id, CancellationToken.None))!;

        relidoA.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal(relidoB.ConfiguracaoDivulgacao!.CamposPublicos);
        relidoA.ConfiguracaoDivulgacao.CamposPublicos.Should().Equal("nome_abreviado", "numero_inscricao");
    }

    // ── D5 — o colapso "linha explícita igual ao default" → ausência precisa de teste de
    // persistência próprio: sem ele, o efeito destrutivo do Cascade fica documentado no plano
    // e não verificado no código. ──

    private static readonly string HashFixo = new('a', 64);
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    private static FaseCronograma FaseDeResultadoFinal() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static ProcessoSeletivo NovoProcessoConforme(string nome)
    {
        ProcessoSeletivo processo = NovoProcesso(nome);

        processo.DefinirCronogramaFases([FaseDeResultadoFinal()],
            []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();
        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas([ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo), Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo),
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", HashFixo),
        }).Value!).IsSuccess.Should().BeTrue();

        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        return processo;
    }

    private static DadosEdital DadosDoEdital() => DadosEdital.Criar(
        "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

    private static VersaoConfiguracao PublicarCanonico(ProcessoSeletivo processo, DadosEdital dados)
    {
        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(
                processo, dados, HashFixo, FusoInstitucional.ZoneId,
                CalendarioDiasUteis: CorpusEnvelopeDadosDoEdital.CalendarioRico(), ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(),
                TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            new CenarioDeCongelamento { Dados = dados, Documento = new DocumentoCongelado(canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash, HashFixo), AtorUsuarioContaId = "integration-test-user", Contagem = CorpusEnvelopeDadosDoEdital.ContextoRico() });
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        VersaoConfiguracao versaoAbertura = publicar.Value!;

        return publicar.Value!;
    }

    private async Task GravarAsync(ProcessoSeletivo processo, VersaoConfiguracao versaoAbertura)
    {
        await using UniPlusDbContext writeContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(writeContext);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(versaoAbertura, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);
    }
}

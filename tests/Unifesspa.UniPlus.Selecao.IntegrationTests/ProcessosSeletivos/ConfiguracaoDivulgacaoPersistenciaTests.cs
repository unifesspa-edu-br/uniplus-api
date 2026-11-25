namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
/// <see cref="ConfiguracaoDivulgacao"/> (UNI-REQ-0050) — o que só o banco real
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
        nome, TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

    [Fact(DisplayName = "Divulgacao_SobreviveASaveChangesEReload — a configuração persiste via Include e sobrevive a um reload")]
    public async Task Divulgacao_SobreviveASaveChangesEReload()
    {
        ProcessoSeletivo processo = NovoProcesso(nameof(Divulgacao_SobreviveASaveChangesEReload));
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO", "NOME"], "Transparência do resultado — decisão CEPS 12/2026.").Value!).IsSuccess.Should().BeTrue();

        await GravarProcessoAsync(processo);

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(readContext);
        ProcessoSeletivo relido = (await leitura.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

        ConferirConfiguracaoRelida(relido, processo.Id);
    }

    [Fact(DisplayName = "Campos públicos com JSON nulo são recusados ao carregar")]
    public async Task Divulgacao_CamposPublicosComJsonNulo_RecusaLeitura()
    {
        ProcessoSeletivo processo = NovoProcesso(nameof(Divulgacao_CamposPublicosComJsonNulo_RecusaLeitura));
        processo.DefinirConfiguracaoDivulgacao(ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO"], null).Value!).IsSuccess.Should().BeTrue();
        await GravarProcessoAsync(processo);
        await using (UniPlusDbContext escrita = _fixture.CreateDbContext())
        {
            await escrita.Database.ExecuteSqlInterpolatedAsync($"UPDATE selecao.configuracoes_divulgacao SET campos_publicos = 'null'::jsonb WHERE processo_seletivo_id = {processo.Id}");
        }

        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        Func<Task> carregar = async () => await ProcessoSeletivoRepositoryDeTeste.Criar(leitura).ObterComConfiguracaoAsync(processo.Id, CancellationToken.None);
        await carregar.Should().ThrowAsync<InvalidOperationException>().WithMessage("*campos_publicos*");
    }

    [Fact(DisplayName = "CA-05: DefinirConfiguracaoDivulgacao duas vezes deixa UMA linha em configuracoes_divulgacao, com os valores da segunda")]
    public async Task Divulgacao_SubstituidaDuasVezes_UmaLinhaComOsValoresDaSegunda()
    {
        ProcessoSeletivo processo = NovoProcesso(nameof(Divulgacao_SubstituidaDuasVezes_UmaLinhaComOsValoresDaSegunda));
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO", "NOME_ABREVIADO"], null).Value!).IsSuccess.Should().BeTrue();
        Guid idPrimeiraVersao = processo.ConfiguracaoDivulgacao!.Id;

        Guid processoId = processo.Id;
        await GravarProcessoAsync(processo);

        await SubstituirConfiguracaoAsync(processoId);

        await ConferirConfiguracaoSubstituidaAsync(processoId, idPrimeiraVersao);
    }

    [Fact(DisplayName = "Divulgacao_Removida_SomeDaTabela — DefinirConfiguracaoDivulgacao(null) remove a linha, sem resíduo")]
    public async Task Divulgacao_Removida_SomeDaTabela()
    {
        ProcessoSeletivo processo = NovoProcesso(nameof(Divulgacao_Removida_SomeDaTabela));
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO", "NOME_ABREVIADO"], null).Value!).IsSuccess.Should().BeTrue();
        Guid idConfiguracao = processo.ConfiguracaoDivulgacao!.Id;

        Guid processoId = processo.Id;
        await GravarProcessoAsync(processo);

        await RemoverConfiguracaoAsync(processoId);

        await ConferirRemocaoAsync(processoId, idConfiguracao);
    }

    [Fact(DisplayName = "Duas requisições equivalentes em ordem diferente persistem os campos públicos na MESMA ordem canônica")]
    public async Task Divulgacao_RequisicoesEquivalentes_PersistemNaMesmaOrdem()
    {
        ProcessoSeletivo processoA = NovoProcesso($"{nameof(Divulgacao_RequisicoesEquivalentes_PersistemNaMesmaOrdem)}-A");
        processoA.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["NOME_ABREVIADO", "NUMERO_INSCRICAO"], null).Value!).IsSuccess.Should().BeTrue();

        ProcessoSeletivo processoB = NovoProcesso($"{nameof(Divulgacao_RequisicoesEquivalentes_PersistemNaMesmaOrdem)}-B");
        processoB.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO", "NOME_ABREVIADO"], null).Value!).IsSuccess.Should().BeTrue();

        await GravarProcessosAsync(processoA, processoB);

        await ConferirOrdemAsync(processoA.Id, processoB.Id);
    }

    // ── D5 — o colapso "linha explícita igual ao default" → ausência precisa de teste de
    // persistência próprio: sem ele, o efeito destrutivo do Cascade fica documentado no plano
    // e não verificado no código. ──

    private static readonly string HashFixo = new('a', 64);
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    private static FaseCronograma FaseDeResultadoFinal() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(resultado: "RESULTADO_FINAL"),
        bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]).Value!;

    private static ProcessoSeletivo NovoProcessoConforme(string nome)
    {
        ProcessoSeletivo processo = NovoProcesso(nome);

        DefinirCronogramaEEtapas(processo);
        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();

        DefinirDistribuicao(processo);
        DefinirClassificacao(processo);
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);

        DefinirTaxaSemCobranca(processo);
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        return processo;
    }

    private async Task ConferirConfiguracaoSubstituidaAsync(Guid processoId, Guid idPrimeiraVersao)
    {
        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(readContext);
        ProcessoSeletivo relido = (await leitura.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;

        relido.ConfiguracaoDivulgacao.Should().NotBeNull();
        relido.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal("NOME", "NUMERO_INSCRICAO");
        relido.ConfiguracaoDivulgacao.Justificativa.Should().Be("Justificativa da segunda versão.");

        await ConferirSubstituicaoAsync(readContext, processoId, idPrimeiraVersao);
    }

    private static void ConferirConfiguracaoRelida(ProcessoSeletivo relido, Guid processoId)
    {
        relido.ConfiguracaoDivulgacao.Should().NotBeNull(
            "a configuração sobrevive ao SaveChanges e ao reload — sem o Include em ComConfiguracao ela nasceria " +
            "null em todo carregamento novo do agregado");
        relido.ConfiguracaoDivulgacao!.ProcessoSeletivoId.Should().Be(processoId);
        relido.ConfiguracaoDivulgacao.CamposPublicos.Should().Equal("NOME", "NUMERO_INSCRICAO");
        relido.ConfiguracaoDivulgacao.Justificativa.Should().Be("Transparência do resultado — decisão CEPS 12/2026.");
    }

    private static async Task ConferirSubstituicaoAsync(UniPlusDbContext readContext, Guid processoId, Guid idPrimeiraVersao)
    {
        int linhas = await readContext.Set<ConfiguracaoDivulgacao>()
            .CountAsync(c => c.ProcessoSeletivoId == processoId, CancellationToken.None);
        linhas.Should().Be(1, "substituir a configuração inteira via DefinirConfiguracaoDivulgacao não pode deixar a primeira versão como resíduo — é UMA linha, com os valores da mutação mais recente");

        bool primeiraVersaoAindaExiste = await readContext.Set<ConfiguracaoDivulgacao>()
            .AnyAsync(c => c.Id == idPrimeiraVersao, CancellationToken.None);
        primeiraVersaoAindaExiste.Should().BeFalse("a primeira versão não pode sobreviver à substituição");
    }

    private async Task ConferirRemocaoAsync(Guid processoId, Guid idConfiguracao)
    {
        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(readContext);
        ProcessoSeletivo relido = (await leitura.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;

        relido.ConfiguracaoDivulgacao.Should().BeNull(
            "DefinirConfiguracaoDivulgacao(null) remove a configuração — o toggle 'sem configuração explícita' é a própria ausência da entidade");

        bool aindaExiste = await readContext.Set<ConfiguracaoDivulgacao>()
            .AnyAsync(c => c.Id == idConfiguracao, CancellationToken.None);
        aindaExiste.Should().BeFalse("a linha não pode sobrar em configuracoes_divulgacao");
    }

    private async Task ConferirOrdemAsync(Guid primeiroId, Guid segundoId)
    {
        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(readContext);
        ProcessoSeletivo relidoA = (await leitura.ObterComConfiguracaoAsync(primeiroId, CancellationToken.None))!;
        ProcessoSeletivo relidoB = (await leitura.ObterComConfiguracaoAsync(segundoId, CancellationToken.None))!;

        relidoA.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal(relidoB.ConfiguracaoDivulgacao!.CamposPublicos);
        relidoA.ConfiguracaoDivulgacao.CamposPublicos.Should().Equal("NOME_ABREVIADO", "NUMERO_INSCRICAO");
    }

    private static void DefinirTaxaSemCobranca(ProcessoSeletivo processo) =>
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();

    private static void DefinirClassificacao(ProcessoSeletivo processo)
    {
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), "v1", HashFixo),
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", HashFixo),
        }).Value!).IsSuccess.Should().BeTrue();
    }

    private static void DefinirCronogramaEEtapas(ProcessoSeletivo processo)
    {
        processo.DefinirCronogramaFases([FaseDeResultadoFinal()],
            []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();
    }

    private static void DefinirDistribuicao(ProcessoSeletivo processo)
    {
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { RegraRemanejamento = null, BaseLegal = "Res. Unifesspa 532/2021" }, 40).Value!;
        processo.DefinirDistribuicaoVagas([ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", HashFixo), Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }

    private async Task GravarProcessoAsync(ProcessoSeletivo processo)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(contexto);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private async Task GravarProcessosAsync(ProcessoSeletivo primeiro, ProcessoSeletivo segundo)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(contexto);
        await repository.AdicionarAsync(primeiro, CancellationToken.None);
        await repository.AdicionarAsync(segundo, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private async Task SubstituirConfiguracaoAsync(Guid processoId)
    {
        await using UniPlusDbContext sessao = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(sessao);
        ProcessoSeletivo tracked = (await repository.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
        tracked.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO", "NOME"], "Justificativa da segunda versão.").Value!).IsSuccess.Should().BeTrue();
        await sessao.SaveChangesAsync(CancellationToken.None);
    }

    private async Task RemoverConfiguracaoAsync(Guid processoId)
    {
        await using UniPlusDbContext sessao = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(sessao);
        ProcessoSeletivo tracked = (await repository.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
        tracked.ConfiguracaoDivulgacao.Should().NotBeNull("pré-condição: a configuração persistiu na sessão anterior");
        tracked.DefinirConfiguracaoDivulgacao(null).IsSuccess.Should().BeTrue();
        await sessao.SaveChangesAsync(CancellationToken.None);
    }

    private static DadosEdital DadosDoEdital() => DadosEdital.Criar(
        "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

    private static VersaoConfiguracao PublicarCanonico(ProcessoSeletivo processo, DadosEdital dados)
    {
        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(processo, dados, HashFixo, FusoInstitucional.ZoneId, Retificacao: null, Conformidade: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(CalendarioDiasUteis: ExemploDeEnvelopeDadosDoEdital.CalendarioRico(), ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)));
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            new CenarioDeCongelamento { Dados = dados, Documento = new DocumentoCongelado(canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash, HashFixo), AtorUsuarioContaId = "integration-test-user", Contagem = ExemploDeEnvelopeDadosDoEdital.ContextoRico() });
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

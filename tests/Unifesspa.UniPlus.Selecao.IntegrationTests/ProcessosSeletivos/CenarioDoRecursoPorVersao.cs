namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Um processo publicado com recurso na fase de resultado e na etapa dela, e duas versões da configuração, ambas com instância
/// superior. No recurso da fase só o prazo da 2ª instância muda de uma versão para a outra (72 horas na primeira, 48 horas na
/// segunda). No recurso da etapa mudam o prazo e o prazo da 2ª instância (120 e 96 horas na primeira; 72 e 24 horas na segunda);
/// a guarda da vaga é a mesma nas duas, porque a instância superior exige guardar a vaga até o resultado definitivo, e o efeito
/// suspensivo não muda, porque só existe um
/// valor. Os valores da etapa diferem dos da fase, para que uma leitura trocada de lugar seja percebida. A leitura é a que os
/// consumidores do recurso usam: a configuração congelada na versão vigente num instante.
/// </summary>
internal sealed class CenarioDoRecursoPorVersao
{
    /// <summary>O instante em que a primeira versão, a da abertura, passa a valer.</summary>
    public static readonly DateTimeOffset VigenciaDaPrimeira = CenarioDeVersaoVigentePersistida.T0;

    /// <summary>O instante em que a segunda versão passa a valer.</summary>
    public static readonly DateTimeOffset VigenciaDaSegunda = VigenciaDaPrimeira.AddDays(2);

    public static readonly ParametrosRegraPrazoRecurso CongeladosNaPrimeira = ParametrosComSegundaInstanciaEm(new PrazoDoRecurso(72m, UnidadePrazo.Horas));

    public static readonly ParametrosRegraPrazoRecurso CongeladosNaSegunda = ParametrosComSegundaInstanciaEm(new PrazoDoRecurso(48m, UnidadePrazo.Horas));

    public static readonly ParametrosRegraPrazoRecurso CongeladosDaEtapaNaPrimeira = ParametrosDaEtapa(
        new PrazoDoRecurso(120m, UnidadePrazo.Horas), new PrazoDoRecurso(96m, UnidadePrazo.Horas), GuardaDaVaga.AteOResultadoDefinitivo);

    public static readonly ParametrosRegraPrazoRecurso CongeladosDaEtapaNaSegunda = ParametrosDaEtapa(
        new PrazoDoRecurso(72m, UnidadePrazo.Horas), new PrazoDoRecurso(24m, UnidadePrazo.Horas), GuardaDaVaga.AteOResultadoDefinitivo);

    private readonly ProcessoSeletivoDbFixture _fixture;
    private readonly CenarioDeVersaoVigentePersistida _persistencia;

    public CenarioDoRecursoPorVersao(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
        _persistencia = new CenarioDeVersaoVigentePersistida(fixture);
    }

    /// <summary>Publica a abertura em <see cref="VigenciaDaPrimeira"/> e grava a segunda versão, vigente a partir de <see cref="VigenciaDaSegunda"/>.</summary>
    public async Task<Guid> PublicarAberturaEGravarSegundaVersaoAsync(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoConforme(nome, fase: FaseComRecursoEmDuasInstancias());
        DeclararORecursoDaEtapa(processo);
        // Prazo em horas conta em dia útil: o processo declara a convenção de contagem que usa.
        processo.DefinirAlgoritmoContagemPrazo(
            ReferenciaRegra.De(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, "v1", new string('d', 64))).IsSuccess.Should().BeTrue();
        await _persistencia.PublicarEPersistirAsync(processo, new RelogioManualDaVigencia(VigenciaDaPrimeira));
        await GravarSegundaVersaoComOsParametrosTrocadosAsync(processo.Id);

        return processo.Id;
    }

    /// <summary>O número da versão vigente no instante e os parâmetros do recurso da fase congelados nela.</summary>
    public async Task<(int NumeroDaVersao, ParametrosRegraPrazoRecurso Parametros)> LerOsParametrosAsync(Guid processoId, DateTimeOffset instante)
    {
        ConfiguracaoVigente vigente = await LerQueExisteAsync(processoId, instante);

        return (vigente.NumeroDaVersao, vigente.Configuracao.Cronograma.CronogramaFases.Single().RegraRecurso!.Parametros);
    }

    /// <summary>O número da versão vigente no instante e os parâmetros do recurso da etapa congelados nela.</summary>
    public async Task<(int NumeroDaVersao, ParametrosRegraPrazoRecurso Parametros)> LerOsParametrosDaEtapaAsync(Guid processoId, DateTimeOffset instante)
    {
        ConfiguracaoVigente vigente = await LerQueExisteAsync(processoId, instante);

        return (vigente.NumeroDaVersao, vigente.Configuracao.Avaliacao.Etapas.Single().Recursos.Single().Parametros);
    }

    public async Task<Result<ConfiguracaoVigente>> LerAsync(Guid processoId, DateTimeOffset instante)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        ConfiguracaoVigenteDoProcesso leitor = new(ProcessoSeletivoRepositoryDeTeste.Criar(context), CorpusEnvelope.Registro);

        return await leitor.LerAsync(processoId, instante, CancellationToken.None);
    }

    private async Task<ConfiguracaoVigente> LerQueExisteAsync(Guid processoId, DateTimeOffset instante)
    {
        Result<ConfiguracaoVigente> lida = await LerAsync(processoId, instante);
        lida.IsSuccess.Should().BeTrue(lida.Error?.Message);

        return lida.Value!;
    }

    private static ParametrosRegraPrazoRecurso ParametrosDaEtapa(PrazoDoRecurso interposicao, PrazoDoRecurso segunda, GuardaDaVaga guarda) =>
        new(interposicao, AdmiteInstanciaSuperior: true, segunda, EfeitoSuspensivo.SemEfeitoSuspensivo, guarda);

    private static ParametrosRegraPrazoRecurso ParametrosComSegundaInstanciaEm(PrazoDoRecurso segunda) =>
        new(new PrazoDoRecurso(48m, UnidadePrazo.Horas), AdmiteInstanciaSuperior: true, segunda, EfeitoSuspensivo.SemEfeitoSuspensivo, GuardaDaVaga.AteOResultadoDefinitivo);

    // O registro congelado da segunda versão é o da primeira com os parâmetros do recurso da fase e da etapa trocados, serializado
    // pelo perfil canônico, o mesmo que o codec exige na leitura.
    private async Task GravarSegundaVersaoComOsParametrosTrocadosAsync(Guid processoId)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository processos = ProcessoSeletivoRepositoryDeTeste.Criar(context);
        VersaoConfiguracao primeira = (await processos.ObterVersoesAsync(processoId, CancellationToken.None)).Single();
        JsonObject envelope = VersoesDoEnvelopeDeTeste.EnvelopeDosBytes(primeira.ConfiguracaoCongeladaCanonica);
        TrocarOPrazoDaSegundaInstanciaDaFase(envelope);
        TrocarOsParametrosDaEtapa(envelope);
        envelope["retificacao"] = new JsonObject { ["editalRetificadoId"] = primeira.AtoCriadorId.ToString(), ["motivo"] = "Prazos do recurso revistos" };
        DocumentoCongelado documento = new(PerfilCanonicoV1.Instancia.Serializar(envelope), primeira.SchemaVersion, primeira.AlgoritmoHash, primeira.AtoCriadorHash);

        VersaoConfiguracao segunda = VersaoConfiguracao.Suceder(primeira, documento, Guid.CreateVersion7(), "plataforma-admin", VigenciaDaSegunda);
        await processos.AdicionarVersaoConfiguracaoAsync(segunda, CancellationToken.None);
        await context.SaveChangesAsync();
    }

    private static void TrocarOPrazoDaSegundaInstanciaDaFase(JsonObject envelope)
    {
        JsonObject parametros = envelope["cronogramaFases"]!["fases"]!.AsArray().Single()!["regraRecurso"]!["parametros"]!.AsObject();
        parametros["prazoSegundaInstanciaValor"] = "48.0000";
        parametros["prazoSegundaInstanciaUnidade"] = UnidadePrazo.Horas.Codigo();
    }

    private static void TrocarOsParametrosDaEtapa(JsonObject envelope)
    {
        JsonObject parametros = envelope["etapas"]!.AsArray().Single()!["recursos"]!.AsArray().Single()!["parametros"]!.AsObject();
        parametros["prazoValor"] = "72.0000";
        parametros["prazoUnidade"] = UnidadePrazo.Horas.Codigo();
        parametros["prazoSegundaInstanciaValor"] = "24.0000";
        parametros["prazoSegundaInstanciaUnidade"] = UnidadePrazo.Horas.Codigo();
    }

    // A etapa da fase de resultado abre também um recurso por ciência individual do candidato, com os parâmetros da primeira versão;
    // a ciência individual exige que a etapa prometa parecer individual e não tem produto em que ancorar.
    private static void DeclararORecursoDaEtapa(ProcessoSeletivo processo)
    {
        EtapaProcesso etapa = processo.Etapas.Single();
        etapa.DefinirJanelaEParecer(null, null, emiteParecerIndividual: true).IsSuccess.Should().BeTrue();
        Result<RecursoDaEtapa> criado = RecursoDaEtapa.Criar(
            AncoraDoRecurso.CienciaIndividual,
            ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('d', 64)),
            ParametrosDoRecursoDeTeste.DeclaradosDe(CongeladosDaEtapaNaPrimeira),
            Guid.Empty);
        criado.IsSuccess.Should().BeTrue(string.Join("; ", criado.Errors.Select(static e => $"{e.Field}: {e.Error.Code}")));

        etapa.DefinirRecursos([criado.Value!]).IsSuccess.Should().BeTrue();
    }

    // A fase de resultado com recurso em duas instâncias e a banca de análise de recursos que todo recurso exige.
    private static FaseCronograma FaseComRecursoEmDuasInstancias()
    {
        Guid preliminarId = Guid.CreateVersion7();
        PublicacoesDaFase publicacoes = HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(
            ProdutosDoResultado(preliminarId), RecursoEmDuasInstancias(preliminarId));
        OrigemCanonicaDaFase origem = new(
            Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false,
            ColetaInscricao: true, ColetaSolicitacaoIsencao: false);
        DecisoesDeCronogramaDaFase periodo = new(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            UnidadeResponsavel: UnidadesDeTeste.Ceps);

        return FaseCronograma.Criar(origem, periodo, publicacoes, [BancasDeTeste.DeRecursosDaFase()]).Value!;
    }

    private static ProdutoDaFase[] ProdutosDoResultado(Guid preliminarId) =>
    [
        ProdutoDaFase.Reidratar(preliminarId, HomologacaoDasInscricoesDeTeste.ResultadoDaFase, PapelProdutoFase.Preliminar),
        ProdutoDaFase.Criar(HomologacaoDasInscricoesDeTeste.ResultadoDaFase, PapelProdutoFase.Definitivo),
    ];

    // O prazo da 2ª instância declarado é o congelado na primeira versão; o prazo ancora no resultado preliminar da própria fase.
    private static RegraRecursoFase RecursoEmDuasInstancias(Guid preliminarId) => RegraRecursoFase.Criar(
        ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('d', 64)),
        ParametrosDoRecursoDeTeste.Declarados(
            48m, UnidadePrazo.Horas, admiteInstanciaSuperior: true, segundaInstancia: CongeladosNaPrimeira.PrazoDaSegundaInstancia),
        preliminarId,
        HomologacaoDasInscricoesDeTeste.ProcedimentosDaHomologacao()).Value!;
}

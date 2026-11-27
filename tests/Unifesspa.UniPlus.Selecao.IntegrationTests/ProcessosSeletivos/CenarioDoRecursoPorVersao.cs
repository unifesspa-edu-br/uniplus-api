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
/// superior. No recurso da fase mudam o prazo da 2ª instância (72 horas na primeira, 48 horas na segunda) e o efeito suspensivo
/// (sem efeito suspensivo na primeira, para todos os recursos na segunda). No recurso da etapa mudam o prazo e o prazo da 2ª
/// instância (120 e 96 horas na primeira; 72 e 24 horas na segunda) e o efeito suspensivo (sem efeito suspensivo na primeira,
/// a unidade decidindo em cada recurso na segunda). O envelope grava o rótulo do efeito ao lado do código, e os dois trocam
/// juntos. A guarda da vaga é a mesma nas duas versões, porque a instância superior exige guardar a vaga até o resultado
/// definitivo. Os valores da etapa diferem dos da fase, para que uma leitura trocada de lugar seja percebida. A leitura é a que
/// os consumidores do recurso usam: a configuração congelada na versão vigente num instante.
/// </summary>
internal sealed class CenarioDoRecursoPorVersao
{
    /// <summary>O instante em que a primeira versão, a da abertura, passa a valer.</summary>
    public static readonly DateTimeOffset VigenciaDaPrimeira = CenarioDeVersaoVigentePersistida.T0;

    /// <summary>O instante em que a segunda versão passa a valer.</summary>
    public static readonly DateTimeOffset VigenciaDaSegunda = VigenciaDaPrimeira.AddDays(2);

    public static readonly ParametrosRegraPrazoRecurso CongeladosNaPrimeira = ParametrosComSegundaInstanciaEm(
        new PrazoDoRecurso(72m, UnidadePrazo.Horas), EfeitoSuspensivo.SemEfeitoSuspensivo);

    public static readonly ParametrosRegraPrazoRecurso CongeladosNaSegunda = ParametrosComSegundaInstanciaEm(
        new PrazoDoRecurso(48m, UnidadePrazo.Horas), EfeitoSuspensivo.ParaTodosOsRecursos);

    public static readonly ParametrosRegraPrazoRecurso CongeladosDaEtapaNaPrimeira = ParametrosDaEtapa(
        new PrazoDoRecurso(120m, UnidadePrazo.Horas), new PrazoDoRecurso(96m, UnidadePrazo.Horas), EfeitoSuspensivo.SemEfeitoSuspensivo);

    public static readonly ParametrosRegraPrazoRecurso CongeladosDaEtapaNaSegunda = ParametrosDaEtapa(
        new PrazoDoRecurso(72m, UnidadePrazo.Horas), new PrazoDoRecurso(24m, UnidadePrazo.Horas), EfeitoSuspensivo.UnidadeDecideEmCadaRecurso);

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
            ReferenciaRegra.De(ConvencaoDeContagemDoPrazo.ExcluiDiaInicial.Codigo(), "v1", new string('d', 64))).IsSuccess.Should().BeTrue();
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

    /// <summary>O código e o rótulo do efeito suspensivo gravados no envelope da versão vigente no instante, no recurso da fase.</summary>
    public async Task<(string Codigo, string Rotulo)> LerOEfeitoGravadoDaFaseAsync(Guid processoId, DateTimeOffset instante) =>
        EfeitoGravadoEm(ParametrosDoRecursoDaFase(await LerOEnvelopeVigenteAsync(processoId, instante)));

    /// <summary>O código e o rótulo do efeito suspensivo gravados no envelope da versão vigente no instante, no recurso da etapa.</summary>
    public async Task<(string Codigo, string Rotulo)> LerOEfeitoGravadoDaEtapaAsync(Guid processoId, DateTimeOffset instante) =>
        EfeitoGravadoEm(ParametrosDoRecursoDaEtapa(await LerOEnvelopeVigenteAsync(processoId, instante)));

    public async Task<Result<ConfiguracaoVigente>> LerAsync(Guid processoId, DateTimeOffset instante)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        ConfiguracaoVigenteDoProcesso leitor = new(ProcessoSeletivoRepositoryDeTeste.Criar(context), ExemploDeProcessoSeletivo.Registro);

        return await leitor.LerAsync(processoId, instante, CancellationToken.None);
    }

    private async Task<JsonObject> LerOEnvelopeVigenteAsync(Guid processoId, DateTimeOffset instante)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository processos = ProcessoSeletivoRepositoryDeTeste.Criar(context);
        VersaoConfiguracao vigente = (await processos.ObterVersaoVigenteAsync(processoId, instante, CancellationToken.None))!;

        return VersoesDoEnvelopeDeTeste.EnvelopeDosBytes(vigente.ConfiguracaoCongeladaCanonica);
    }

    private static (string Codigo, string Rotulo) EfeitoGravadoEm(JsonObject parametros) =>
        (parametros["efeitoSuspensivo"]!.GetValue<string>(), parametros["contexto"]!["efeitoSuspensivoRotulo"]!.GetValue<string>());

    private async Task<ConfiguracaoVigente> LerQueExisteAsync(Guid processoId, DateTimeOffset instante)
    {
        Result<ConfiguracaoVigente> lida = await LerAsync(processoId, instante);
        lida.IsSuccess.Should().BeTrue(lida.Error?.Message);

        return lida.Value!;
    }

    private static ParametrosRegraPrazoRecurso ParametrosDaEtapa(PrazoDoRecurso interposicao, PrazoDoRecurso segunda, EfeitoSuspensivo efeito) =>
        new(interposicao, AdmiteInstanciaSuperior: true, segunda, efeito, GuardaDaVaga.AteOResultadoDefinitivo);

    private static ParametrosRegraPrazoRecurso ParametrosComSegundaInstanciaEm(PrazoDoRecurso segunda, EfeitoSuspensivo efeito) =>
        new(new PrazoDoRecurso(48m, UnidadePrazo.Horas), AdmiteInstanciaSuperior: true, segunda, efeito, GuardaDaVaga.AteOResultadoDefinitivo);

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
        JsonObject parametros = ParametrosDoRecursoDaFase(envelope);
        parametros["prazoSegundaInstanciaValor"] = "48.0000";
        parametros["prazoSegundaInstanciaUnidade"] = UnidadePrazo.Horas.Codigo();
        TrocarOEfeitoSuspensivo(parametros, CongeladosNaSegunda.EfeitoSuspensivo);
    }

    private static void TrocarOsParametrosDaEtapa(JsonObject envelope)
    {
        JsonObject parametros = ParametrosDoRecursoDaEtapa(envelope);
        parametros["prazoValor"] = "72.0000";
        parametros["prazoUnidade"] = UnidadePrazo.Horas.Codigo();
        parametros["prazoSegundaInstanciaValor"] = "24.0000";
        parametros["prazoSegundaInstanciaUnidade"] = UnidadePrazo.Horas.Codigo();
        TrocarOEfeitoSuspensivo(parametros, CongeladosDaEtapaNaSegunda.EfeitoSuspensivo);
    }

    // O envelope grava o código do efeito e, ao lado, o rótulo dele no contexto: os dois trocam juntos.
    private static void TrocarOEfeitoSuspensivo(JsonObject parametros, EfeitoSuspensivo efeito)
    {
        parametros["efeitoSuspensivo"] = efeito.Codigo();
        parametros["contexto"]!["efeitoSuspensivoRotulo"] = efeito.Rotulo();
    }

    private static JsonObject ParametrosDoRecursoDaFase(JsonObject envelope) =>
        envelope["cronogramaFases"]!["fases"]!.AsArray().Single()!["regraRecurso"]!["parametros"]!.AsObject();

    private static JsonObject ParametrosDoRecursoDaEtapa(JsonObject envelope) =>
        envelope["etapas"]!.AsArray().Single()!["recursos"]!.AsArray().Single()!["parametros"]!.AsObject();

    // A etapa da fase de resultado abre também um recurso por ciência individual do candidato, com os parâmetros da primeira versão;
    // a ciência individual exige que a etapa prometa parecer individual e não tem produto em que ancorar.
    private static void DeclararORecursoDaEtapa(ProcessoSeletivo processo)
    {
        EtapaProcesso etapa = processo.Etapas.Single();
        etapa.DefinirJanelaEParecer(null, null, emiteParecerIndividual: true).IsSuccess.Should().BeTrue();
        Result<RecursoDaEtapa> criado = RecursoDaEtapa.Criar(
            EventoInicialDoPrazo.CienciaIndividual,
            ReferenciaRegra.De(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), "v1", new string('d', 64)),
            ParametrosDoRecursoDeTeste.DeclaradosDe(CongeladosDaEtapaNaPrimeira),
            Guid.Empty);
        criado.IsSuccess.Should().BeTrue(string.Join("; ", criado.Errors.Select(static e => $"{e.Field}: {e.Error.Code}")));

        etapa.DefinirRecursos([criado.Value!]).IsSuccess.Should().BeTrue();
    }

    // A fase de resultado com recurso em duas instâncias, a banca de análise de recursos que todo recurso exige e a banca que julga a 2ª instância.
    private static FaseCronograma FaseComRecursoEmDuasInstancias()
    {
        Guid preliminarId = Guid.CreateVersion7();
        PublicacoesDaFase publicacoes = HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(
            ProdutosDoResultado(preliminarId), RecursoEmDuasInstancias(preliminarId));
        OrigemCanonicaDaFase origem = OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false,
            ColetaInscricao: true, ColetaSolicitacaoIsencao: false));
        DecisoesDeCronogramaDaFase periodo = new(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            UnidadeResponsavel: UnidadesDeTeste.Ceps);

        return FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!, origem, periodo, publicacoes, [BancasDeTeste.DaHomologacaoDasInscricoesDaFase(), BancasDeTeste.DeRecursosDaFase(), BancasDeTeste.DeSegundaInstanciaDaFase()]).Value!;
    }

    private static ProdutoDaFase[] ProdutosDoResultado(Guid preliminarId) =>
    [
        ProdutoDaFase.Reidratar(preliminarId, HomologacaoDasInscricoesDeTeste.ResultadoDaFase, PapelDaPublicacaoPrevista.Preliminar),
        ProdutoDaFase.Criar(HomologacaoDasInscricoesDeTeste.ResultadoDaFase, PapelDaPublicacaoPrevista.Definitivo),
    ];

    // O prazo da 2ª instância declarado é o congelado na primeira versão; o prazo ancora no resultado preliminar da própria fase.
    private static RegraRecursoFase RecursoEmDuasInstancias(Guid preliminarId) => RegraRecursoFase.Criar(
        ReferenciaRegra.De(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), "v1", new string('d', 64)),
        ParametrosDoRecursoDeTeste.Declarados(
            48m, UnidadePrazo.Horas, admiteInstanciaSuperior: true, segundaInstancia: CongeladosNaPrimeira.PrazoDaSegundaInstancia),
        preliminarId,
        HomologacaoDasInscricoesDeTeste.ProcedimentosDaHomologacao()).Value!;
}

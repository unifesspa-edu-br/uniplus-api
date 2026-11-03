namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Publicacao;

using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As três transições que congelam uma versão recusam os onze portões na mesma ordem. A ordem
/// decide qual código o cliente vê quando há mais de uma pendência, e cada portão chama um método
/// diferente da raiz com os argumentos do pedido: trocar dois slots na composição dos portões
/// deixaria o cliente vendo a pendência errada.
/// </summary>
/// <remarks>
/// O teste liga as onze pendências de uma vez e corrige uma por vez, da primeira à última: a cada
/// passo a primeira recusa tem de ser a do portão seguinte da ordem, e sem nenhuma pendência a
/// transição tem sucesso.
/// </remarks>
public sealed class OrdemDosPortoesDeCongelamentoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Bytes = Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString());
    private static readonly string Hash = new('a', 64);
    private const string ResultadoClassificacao = "RESULTADO_CLASSIFICACAO";
    private const string AtoSemPapel = "ATO_SEM_PAPEL";

    private static readonly string[] CodigosEmOrdem =
    [
        ProcessoSeletivoErrorCodes.IdentificadorLegivelAusente,
        "ProcessoSeletivo.LocalidadeAusente",
        "ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado",
        "ProcessoSeletivo.CalendarioVigenteAusente",
        "ProcessoSeletivo.ConformidadeInsuficiente",
        "ProcessoSeletivo.FaseSemUnidadeResponsavel",
        ProcessoSeletivoErrorCodes.TipoDeAtoUnicoDeclaradoMaisDeUmaVez,
        ProcessoSeletivoErrorCodes.ResultadoDeclaradoSemPapel,
        ProcessoSeletivoErrorCodes.TipoQueCongelaDeclaradoComoResultado,
        "ProcessoSeletivo.CascataForaDoRegimeFederal",
        "DocumentoExigido.CondicionalVaziaDeterminaResultado",
    ];

    public static TheoryData<string> Transicoes => ["Publicar", "Retificar", "FecharRetificacao"];

    [Theory(DisplayName = "Com as onze pendências ligadas, cada correção revela a pendência do portão seguinte, na ordem")]
    [MemberData(nameof(Transicoes))]
    public void PrimeiraRecusa_SegueAOrdemDosOnzePortoes(string transicao)
    {
        Cenario cenario = Cenario.Para(transicao);

        for (int primeiraAtiva = 0; primeiraAtiva < CodigosEmOrdem.Length; primeiraAtiva++)
        {
            bool[] ativas = [.. Enumerable.Range(0, CodigosEmOrdem.Length).Select(i => i >= primeiraAtiva)];

            Result<VersaoConfiguracao> resultado = cenario.Congelar(ativas);

            resultado.IsFailure.Should().BeTrue($"o portão {primeiraAtiva + 1} deve recusar");
            resultado.Error!.Code.Should().Be(
                CodigosEmOrdem[primeiraAtiva],
                $"com os portões {primeiraAtiva + 1} a {CodigosEmOrdem.Length} recusando, o primeiro da ordem é o {primeiraAtiva + 1}");
        }

        Result<VersaoConfiguracao> semPendencia = cenario.Congelar(new bool[CodigosEmOrdem.Length]);

        semPendencia.IsSuccess.Should().BeTrue(semPendencia.Error?.Message);
    }

    private static void Definir(object alvo, string propriedade, object? valor) =>
        alvo.GetType()
            .GetProperty(propriedade, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(alvo, valor);

    private static ContextoDeContagemDePrazos ComCalendario() => new(
        CalendarioDiasUteisCongelado.Criar(
            Guid.CreateVersion7(),
            "2026",
            [DiaNaoUtilCongelado.Criar(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null).Value!]).Value,
        FusoInstitucional: TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId));

    private static TiposDeAtoUnicosPorObjeto Tipos(bool unico, bool resultadoSemPapel, bool congelaComPapel)
    {
        Dictionary<string, string> unicos = unico ? new(StringComparer.Ordinal) { [ResultadoClassificacao] = "Resultado" } : new(StringComparer.Ordinal);
        Dictionary<string, ClassificacaoDoTipoDeAto> classificacoes = new(StringComparer.Ordinal);
        if (resultadoSemPapel)
        {
            classificacoes[AtoSemPapel] = new ClassificacaoDoTipoDeAto(EhResultado: true, CongelaConfiguracao: false);
        }

        if (congelaComPapel)
        {
            classificacoes[ResultadoClassificacao] = new ClassificacaoDoTipoDeAto(EhResultado: false, CongelaConfiguracao: true);
        }

        return TiposDeAtoUnicosPorObjeto.ParaChecklist(unicos, [], classificacoes);
    }

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static Guid ProdutoPreliminar { get; } = new("77770000-0000-4000-8000-000000000003");

    private static FaseCronograma[] Cronograma() =>
    [
        FaseCronograma.Criar(
            1, Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NaExecucao,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: true, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            fim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
            produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!,
        FaseCronograma.Criar(
            2, Guid.CreateVersion7(), "RESULTADO_PRELIMINAR", OrigemDataFase.NaExecucao,
            agrupaEtapas: true, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: null, fim: null,
            produtos: [ProdutoDaFase.Reidratar(ProdutoPreliminar, ResultadoClassificacao, PapelProdutoFase.Preliminar)],
            faseConcluinteCodigo: "RESULTADO_FINAL", emiteParecerIndividual: false,
            bancasRequeridas: [],
            regraRecurso: RegraRecursoFase.Criar(
                Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, 'd'),
                new ArgsRegraPrazoRecurso(2m, UnidadePrazo.DiasUteis, null, null, false, null, null),
                ProdutoPreliminar).Value!,
            unidadeResponsavel: UnidadesDeTeste.Ceps).Value!,
        FaseCronograma.Criar(
            3, Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NaExecucao,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: null, fim: null,
            produtos:
            [
                ProdutoDaFase.Criar(ResultadoClassificacao, PapelProdutoFase.Definitivo),
                ProdutoDaFase.Criar(AtoSemPapel, null),
            ],
            faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!,
    ];

    private static DocumentoExigido ExigenciaGeral(Guid faseId) => DocumentoExigido.Criar(
        faseId,
        tipoDocumentoOrigemId: Guid.CreateVersion7(),
        tipoDocumentoCodigo: "IDENTIDADE",
        tipoDocumentoNome: "Documento de identidade",
        tipoDocumentoCategoria: "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
        aplicabilidade: Aplicabilidade.Geral,
        obrigatorio: true,
        consequenciaIndeferimento: null,
        condicoes: [],
        basesLegais: [DocumentoExigidoBaseLegal.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!],
        idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null,
        finalidade: FinalidadeFormulario.Inscricao).Value!;

    private static ProcessoSeletivo ProcessoBase()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        FormularioDeTeste.TrocarCronograma(processo, Cronograma(), PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirAlgoritmoContagemPrazo(Regra(AlgoritmoContagemPrazoCodigo.AvancaDataUtil, 'e'), PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        Guid faseDoFormulario = processo.CronogramaFases.First().Id;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaGeral(faseDoFormulario), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    private sealed class Cenario
    {
        private readonly ProcessoSeletivo _processo;
        private readonly Func<ContextoDeContagemDePrazos, TiposDeAtoUnicosPorObjeto, Result<VersaoConfiguracao>> _transicao;
        private readonly IdentificadorLegivel? _identificador;
        private readonly LocalidadeRegente _localidade;
        private readonly ReferenciaRegra? _algoritmo;
        private readonly ConfiguracaoTaxaInscricao? _taxa;
        private readonly FaseCronograma _fase;
        private readonly Guid? _unidadeDaFase;
        private readonly DocumentoExigido _exigencia;
        private readonly ModalidadeSelecionada _modalidade;
        private readonly RegraRemanejamentoModalidade _remanejamento;

        private Cenario(
            ProcessoSeletivo processo,
            Func<ContextoDeContagemDePrazos, TiposDeAtoUnicosPorObjeto, Result<VersaoConfiguracao>> transicao)
        {
            _processo = processo;
            _transicao = transicao;
            _identificador = processo.IdentificadorLegivel;
            _localidade = processo.Localidade;
            _algoritmo = processo.AlgoritmoContagemPrazo;
            _taxa = processo.ConfiguracaoTaxaInscricao;
            _fase = processo.CronogramaFases.OrderBy(static f => f.Ordem).First();
            _unidadeDaFase = _fase.UnidadeResponsavelOrigemId;
            _exigencia = processo.DocumentosExigidos.Single();
            _modalidade = processo.DistribuicaoVagas.First().Modalidades.First();
            _remanejamento = _modalidade.RegraRemanejamento;
        }

        internal Result<VersaoConfiguracao> Congelar(bool[] ativas)
        {
            Definir(_processo, nameof(ProcessoSeletivo.IdentificadorLegivel), ativas[0] ? null : _identificador);
            Definir(_processo, nameof(ProcessoSeletivo.Localidade), ativas[1] ? null : _localidade);
            Definir(_processo, nameof(ProcessoSeletivo.AlgoritmoContagemPrazo), ativas[2] ? null : _algoritmo);
            Definir(_processo, nameof(ProcessoSeletivo.ConfiguracaoTaxaInscricao), ativas[4] ? null : _taxa);
            Definir(_fase, nameof(FaseCronograma.UnidadeResponsavelOrigemId), ativas[5] ? null : _unidadeDaFase);
            Definir(_modalidade, nameof(ModalidadeSelecionada.RegraRemanejamento), ativas[9] ? RegraRemanejamentoModalidade.SegueCascata : _remanejamento);
            Definir(_exigencia, nameof(DocumentoExigido.Aplicabilidade), ativas[10] ? Aplicabilidade.Condicional : Aplicabilidade.Geral);

            return _transicao(ativas[3] ? ContextoDeContagemDePrazos.SemCalendario : ComCalendario(), Tipos(ativas[6], ativas[7], ativas[8]));
        }

        internal static Cenario Para(string transicao)
        {
            ProcessoSeletivo processo = ProcessoBase();
            DadosEdital dados = DadosEdital.Criar(
                "001/2026",
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
                new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
                Guid.CreateVersion7()).Value!;

            PedidoDeCongelamento Pedido(ContextoDeContagemDePrazos contexto, TiposDeAtoUnicosPorObjeto tipos) => new()
            {
                Dados = dados,
                Documento = new DocumentoCongelado(Bytes, "1.1", "canonical-json/sha256@v1", Hash),
                AtorUsuarioSub = "user-sub-123",
                Clock = new RelogioFixo(Agora),
                Contexto = contexto,
                FatosDeModalidade = FatosDeModalidadeDeTeste.DoCatalogo,
                TiposDeAtoUnicos = tipos,
            };

            if (transicao == "Publicar")
            {
                return new(processo, (contexto, tipos) => processo.Publicar(Pedido(contexto, tipos)));
            }

            Result<VersaoConfiguracao> publicada = processo.Publicar(Pedido(ComCalendario(), TiposDeAtoUnicosDeTeste.Nenhum));
            publicada.IsSuccess.Should().BeTrue(publicada.Error?.Message);
            VersaoConfiguracao versao = publicada.Value!;

            if (transicao == "Retificar")
            {
                return new(processo, (contexto, tipos) => processo.Retificar(Pedido(contexto, tipos), versao, "Correção do prazo"));
            }

            processo.AbrirRetificacao(
                "Correção do prazo", versao, identificadorDaVersaoBase: null,
                VersoesPublicadasDeTeste.SoAVigente(processo), "user-sub-1", Agora).IsSuccess.Should().BeTrue();
            return new(processo, (contexto, tipos) => processo.FecharRetificacao(
                Pedido(contexto, tipos), versao, PrecondicaoIfMatch.DeTags([processo.ETagDaSessaoEditorial!])));
        }
    }
}

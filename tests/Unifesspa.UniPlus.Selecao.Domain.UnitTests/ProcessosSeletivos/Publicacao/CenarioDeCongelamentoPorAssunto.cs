namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Publicacao;

using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Um processo conforme, em qualquer das duas transições que congelam, ao qual se aplica o defeito característico de cada
/// assunto da conferência, pelo nome do assunto. O defeito de um assunto não toca o dos outros.
/// </summary>
internal sealed class CenarioDeCongelamentoPorAssunto
{
    private const string Algoritmo = "canonical-json/sha256@v1";
    private const string ResultadoClassificacao = "RESULTADO_CLASSIFICACAO";
    private const string AtoSemPapel = "ATO_SEM_PAPEL";
    private const string CodigoDaFaseDoRecurso = "RESULTADO_PRELIMINAR";

    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Bytes = Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString());
    private static readonly string Hash = new('a', 64);
    private static readonly Guid ProdutoPreliminar = new("77770000-0000-4000-8000-000000000003");
    private static readonly DadosEdital DadosDoEdital = DadosEdital.Criar(
        "001/2026",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        Guid.CreateVersion7()).Value!;

    private readonly ProcessoSeletivo _processo;
    private readonly int _versaoQueNasce;
    private readonly Func<ContextoDeContagemDePrazos, TiposDeAtoUnicosPorObjeto, CorretoresDasEtapas?, MotivosEscolhidos?, Result<VersaoConfiguracao>> _transicao;
    private ContextoDeContagemDePrazos _contagem = ComCalendario();
    private TiposDeAtoUnicosPorObjeto _tipos = TiposDeAtoUnicosDeTeste.Nenhum;
    private CorretoresDasEtapas? _corretores;
    private MotivosEscolhidos? _motivos;

    private CenarioDeCongelamentoPorAssunto(
        ProcessoSeletivo processo,
        Func<ContextoDeContagemDePrazos, TiposDeAtoUnicosPorObjeto, CorretoresDasEtapas?, MotivosEscolhidos?, Result<VersaoConfiguracao>> transicao,
        int versaoQueNasce)
    {
        _processo = processo;
        _versaoQueNasce = versaoQueNasce;
        _transicao = transicao;
    }

    /// <summary>O nome de cada assunto que o cenário sabe quebrar: tem de ser os mesmos da conferência.</summary>
    internal static IEnumerable<string> AssuntosQueSabeQuebrar => Defeitos.Keys;

    internal static CenarioDeCongelamentoPorAssunto Para(string transicao)
    {
        ProcessoSeletivo processo = ProcessoBase();

        return transicao switch
        {
            "Publicar" => new(processo, (contagem, tipos, corretores, motivos) => PublicarProcesso(processo, contagem, tipos, corretores, motivos), versaoQueNasce: 1),
            _ => Retificando(processo),
        };
    }

    /// <summary>Quebra os assuntos e congela: o resultado é o que a transição devolve.</summary>
    internal Result<VersaoConfiguracao> Congelar(IReadOnlySet<string> assuntosComDefeito)
    {
        Quebrar(assuntosComDefeito);

        return _transicao(_contagem, _tipos, _corretores, _motivos);
    }

    /// <summary>Aplica o defeito característico de cada assunto informado, sem congelar.</summary>
    internal void Quebrar(IReadOnlySet<string> assuntos)
    {
        foreach (string assunto in assuntos)
        {
            Defeitos[assunto].Aplicar(this);
        }
    }

    /// <summary>Os itens do painel de conformidade sobre o processo como ele está.</summary>
    internal IReadOnlyList<ItemConformidade> Painel() =>
        ChecklistDeConformidade.AvaliarConformidade(_processo.Declaracao(), Cadastros());

    /// <summary>As causas de cada assunto, na ordem da conferência e com o campo como a regra o emite, sobre o processo como ele está.</summary>
    internal IReadOnlyList<(string Assunto, IReadOnlyList<FieldError> Causas)> CausasPorAssunto()
    {
        ContextoDaConferencia contexto = ContextoDaConferencia.De(_processo.Declaracao(), Cadastros());

        return [.. CausasQueImpedemCongelar.Assuntos.Select(assunto => (assunto.Nome, (IReadOnlyList<FieldError>)[.. assunto.Causas(contexto)]))];
    }

    /// <summary>As causas que a conferência devolve ao cliente, com o campo no ponteiro do documento, sobre o processo como ele está, depois de aplicar os defeitos dos assuntos.</summary>
    internal IReadOnlyList<FieldError> CausasDaConferencia(IReadOnlySet<string> assuntosComDefeito)
    {
        Quebrar(assuntosComDefeito);

        return PonteiroNoDocumento.Converter(ConfiguracaoAptaACongelar.Conferir(_processo.Id, _processo.Declaracao(), Cadastros()).Errors, OrigemNoDocumento.Identidade);
    }

    private CadastrosDaConfiguracao Cadastros() =>
        CadastrosDeTeste.Com(contagemDePrazos: _contagem, tiposDeAtoUnicos: _tipos, corretores: _corretores ?? CorretoresDeTeste.DoProcesso(_processo), motivosEscolhidos: _motivos);

    private sealed record Defeito(Action<CenarioDeCongelamentoPorAssunto> Aplicar);

    private static readonly Dictionary<string, Defeito> Defeitos = new(StringComparer.Ordinal)
    {
        [NomeDoAssunto.Identificacao] = new(static c =>
        {
            Definir(c._processo, nameof(ProcessoSeletivo.IdentificadorLegivel), null);
            Definir(c._processo, nameof(ProcessoSeletivo.Localidade), null);
            Definir(c._processo, nameof(ProcessoSeletivo.AlgoritmoContagemPrazo), null);
            c._contagem = ContextoDeContagemDePrazos.SemCalendario;
        }),
        [NomeDoAssunto.Conformidade] = new(static c => Definir(c._processo, nameof(ProcessoSeletivo.ConfiguracaoTaxaInscricao), null)),
        [NomeDoAssunto.Declaracoes] = new(static c => ProcessoCarregadoDoBanco.Fase(c._processo, FaseComJanelaInvertida())),
        [NomeDoAssunto.Conjunto] = new(static c => ProcessoCarregadoDoBanco.Etapa(
            c._processo, EtapaDeProva("Outra prova", c._processo.Etapas.First().Ordem ?? 1, c._processo.Etapas.First().FaseCodigo))),
        [NomeDoAssunto.Completude] = new(static c => ProcessoCarregadoDoBanco.Etapa(c._processo, EtapaDeProva("Prova sem fase", 60, null))),
        [NomeDoAssunto.Recursos] = new(static c => ProcessoCarregadoDoBanco.ComTipoDeDecisao(c.FaseDoRecurso(), TipoMotivoDeDecisaoCodigo.Classificacao)),
        [NomeDoAssunto.Bancas] = new(static c => ProcessoCarregadoDoBanco.SemBancasRequeridas(c.FaseDoRecurso())),
        [NomeDoAssunto.CorrecaoIndependente] = new(static c => c.ComEtapaDeUmSoCorretor()),
        [NomeDoAssunto.TiposDeAto] = new(static c => c._tipos = Tipos(unico: true, resultadoSemPapel: true, congelaComPapel: true)),
        [NomeDoAssunto.Cascata] = new(static c => c.ComModalidadeQueSegueACascataSemCascata()),
        [NomeDoAssunto.CondicoesDaCanonicalizacao] = new(static c => Definir(c._processo.DocumentosExigidos.Single(), nameof(DocumentoExigido.Aplicabilidade), Aplicabilidade.Condicional)),
        [NomeDoAssunto.MotivosDeDecisao] = new(static c => c.ComMotivoDesativadoNaInscricao()),
    };

    // Sem natureza legal declarada, o remanejamento que segue a cascata não destoa dela: só a cobertura da cascata recusa.
    private void ComModalidadeQueSegueACascataSemCascata()
    {
        ModalidadeSelecionada modalidade = _processo.DistribuicaoVagas.SelectMany(static o => o.Modalidades).First();
        Definir(modalidade, nameof(ModalidadeSelecionada.NaturezaLegal), NaturezaLegalModalidade.Nenhuma);
        Definir(modalidade, "RegraRemanejamentoCodigo", RegraRemanejamentoModalidade.SegueCascata.ToCodigo());
    }

    private void ComEtapaDeUmSoCorretor()
    {
        EtapaProcesso etapa = EtapaQueExigeCorrecaoIndependente();
        ProcessoCarregadoDoBanco.Etapa(_processo, etapa);
        _corretores = new CorretoresDasEtapas(new Dictionary<ReferenciaRegra, CorretoresDaRegra> { [etapa.RegraConsolidacao!] = CorretoresDaRegra.SemNovoCorretor(1) });
    }

    // O motivo entra na versão que nasce, que a retificação numera depois da publicada, e foi desativado no cadastro.
    private void ComMotivoDesativadoNaInscricao()
    {
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);
        LugarDaDecisao lugar = ProcessoParaMotivos.LugarDaFase(_processo, "INSCRICAO");
        MotivoDoProcesso escolhido = ProcessoParaMotivos.Escolha(_processo, lugar, origem, _versaoQueNasce);
        origem.Desativar();
        _motivos = new MotivosEscolhidos([new MotivoEscolhidoComOrigem(escolhido, origem)], _versaoQueNasce);
    }

    private FaseCronograma FaseDoRecurso() => _processo.CronogramaFases.Single(static f => f.Codigo == CodigoDaFaseDoRecurso);

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
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    private static EtapaProcesso EtapaDeProva(string nome, int ordem, string? faseCodigo) =>
        ApoioDeEtapaReidratada.DeProvaObjetiva(Guid.CreateVersion7(), nome, ordem, faseCodigo, 1m);

    // Humana, com a banca que a conformidade por item exige: só a quantidade de corretores, lida do cadastro, impede congelar.
    private static EtapaProcesso EtapaQueExigeCorrecaoIndependente()
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(
                Nome: "Redação", Carater: CaraterEtapa.Classificatoria,
                TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "REDACAO", "Redação", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false, exigeCorrecaoIndependente: true).Value!,
                Ordem: 50, FaseCodigo: "RESULTADO_FINAL"),
            new DeclaracaoDaAvaliacaoDaEtapa(
                Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: 0.5m, FormaDeAvaliacao: FormaDeAvaliacao.Humana,
                RegraConsolidacao: Regra(RegraConsolidacaoCodigo.CorrecaoIndependente, 'c'), EscalaDeAtendimento: null)).Value!;
        etapa.DefinirBancas([BancasDeTeste.DeRecursosDaEtapa()]).IsSuccess.Should().BeTrue();

        return etapa;
    }

    private static FaseCronograma FaseComJanelaInvertida() => FaseCronograma.Reidratar(
        Guid.CreateVersion7(),
        new OrigemCanonicaDaFase(Guid.CreateVersion7(), "FASE_DA_JANELA", OrigemDataFase.NoEdital, false, false, false, false),
        new DecisoesDeCronogramaDaFase(99, Agora, Agora.AddDays(-1), null, UnidadesDeTeste.Ceps),
        new PublicacoesDaFase([], false, null),
        []);

    private static FaseCronograma[] Cronograma() => [FaseDeInscricao(), FasePreliminarComRecurso(), FaseDoResultadoFinal()];

    private static FaseCronograma FaseDeInscricao() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: true, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), Fim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static FaseCronograma FasePreliminarComRecurso() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), CodigoDaFaseDoRecurso, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(2, Inicio: null, Fim: null, FaseConcluinteCodigo: "RESULTADO_FINAL", UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(
            Produtos: [ProdutoDaFase.Reidratar(ProdutoPreliminar, ResultadoClassificacao, PapelProdutoFase.Preliminar)],
            EmiteParecerIndividual: false,
            RegraRecurso: RegraRecursoFase.Criar(
                Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, 'd'),
                new ParametrosRegraPrazoRecurso(2m, UnidadePrazo.DiasUteis, null, null, false, null, null),
                ProdutoPreliminar).Value!),
        bancasRequeridas: [BancasDeTeste.DeRecursosDaFase()]).Value!;

    private static FaseCronograma FaseDoResultadoFinal() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(3, Inicio: null, Fim: null, FaseConcluinteCodigo: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(
            Produtos: [
                ProdutoDaFase.Criar(ResultadoClassificacao, PapelProdutoFase.Definitivo),
                ProdutoDaFase.Criar(AtoSemPapel, null),
            ],
            EmiteParecerIndividual: false,
            RegraRecurso: null),
        bancasRequeridas: []).Value!;

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
        FormularioDeTeste.TrocarCronograma(processo, Cronograma()).IsSuccess.Should().BeTrue();
        processo.DefinirAlgoritmoContagemPrazo(Regra(AlgoritmoContagemPrazoCodigo.AvancaDataUtil, 'e'))
            .IsSuccess.Should().BeTrue();
        Guid faseDoFormulario = processo.CronogramaFases.First().Id;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaGeral(faseDoFormulario), 0).Value!])
            .IsSuccess.Should().BeTrue();

        return processo;
    }

    private static CenarioDeCongelamentoPorAssunto Retificando(ProcessoSeletivo processo)
    {
        VersaoConfiguracao versao = Publicada(processo);

        return new(processo, (contagem, tipos, corretores, motivos) => processo.Retificar(
            new CenarioDeCongelamento { Dados = DadosDoEdital, Documento = new DocumentoCongelado(Bytes, "1.1", Algoritmo, Hash), Relogio = new RelogioFixo(Agora), Contagem = contagem, TiposDeAtoUnicos = tipos, Corretores = corretores, MotivosEscolhidos = motivos }, versao, "Correção do prazo"), versaoQueNasce: 2);
    }

    private static VersaoConfiguracao Publicada(ProcessoSeletivo processo)
    {
        Result<VersaoConfiguracao> publicada = PublicarProcesso(processo, ComCalendario(), TiposDeAtoUnicosDeTeste.Nenhum, null);
        publicada.IsSuccess.Should().BeTrue(publicada.Error?.Message);

        return publicada.Value!;
    }

    private static Result<VersaoConfiguracao> PublicarProcesso(
        ProcessoSeletivo processo, ContextoDeContagemDePrazos contagem, TiposDeAtoUnicosPorObjeto tipos, CorretoresDasEtapas? corretores, MotivosEscolhidos? motivos = null) => processo.Publicar(
            new CenarioDeCongelamento { Dados = DadosDoEdital, Documento = new DocumentoCongelado(Bytes, "1.1", Algoritmo, Hash), Relogio = new RelogioFixo(Agora), Contagem = contagem, TiposDeAtoUnicos = tipos, Corretores = corretores, MotivosEscolhidos = motivos });
}

namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O processo conforme cujas etapas são de avaliação humana, com recurso, e a banca de recursos declarada com o número de
/// integrantes e o quórum dados. A referência de cada regra é a do catálogo de teste, a que o leitor confirma.
/// </summary>
internal static class ProcessoComCorrecaoIndependente
{
    public static ReferenciaRegra CorrecaoIndependente => RegrasDeConsolidacaoDeTeste.CorrecaoIndependente.Referencia();

    public static ReferenciaRegra AvaliacaoUnica => RegrasDeConsolidacaoDeTeste.AvaliacaoUnica.Referencia();

    public static ReferenciaRegra MaioriaSimples => RegrasDeConsolidacaoDeTeste.ConceitoMaioriaSimples.Referencia();

    public static ReferenciaRegra Unanimidade => RegrasDeConsolidacaoDeTeste.ConceitoUnanimidade.Referencia();

    /// <summary>Uma etapa de avaliação humana para cada regra dada, na ordem, sem banca declarada.</summary>
    public static ProcessoSeletivo ComRegras(params ReferenciaRegra[] consolidacoes) =>
        ComEtapas(ProcessoSeletivoConformeBuilder.Criar("PS regras de consolidação"), [.. consolidacoes.Select(static (regra, indice) => Entrevista($"Etapa {indice + 1}", indice + 1, regra))]);

    /// <summary>
    /// Uma etapa que referencia a regra de consolidação dada, a correção independente quando não se informa, cuja banca de
    /// recursos é a da própria etapa.
    /// </summary>
    public static ProcessoSeletivo ComBancaDaEtapa(int integrantes, int quorum = 2, ReferenciaRegra? consolidacao = null)
    {
        ProcessoSeletivo processo = ComEtapas(
            ProcessoSeletivoConformeBuilder.Criar("PS banca da etapa"), Entrevista("Redação", 1, consolidacao ?? CorrecaoIndependente));
        DeclararBancaDaEtapa(processo.Etapas.Single(), integrantes, quorum);

        return processo;
    }

    /// <summary>Uma etapa de correção independente e outra de avaliação única, julgadas pela banca de recursos da fase.</summary>
    public static ProcessoSeletivo ComBancaDaFase(int integrantes, int quorum = 2)
    {
        BancaRequerida banca = BancaRequerida.Criar(
            Guid.CreateVersion7(), TipoBancaCodigo.AnaliseDeRecursos, "Banca de análise de recursos", [], BancasDeTeste.Regra(integrantes, quorum));

        ProcessoSeletivo processo = ComEtapas(
            ProcessoSeletivoConformeBuilder.Criar("PS banca da fase", [banca], out _),
            Entrevista("Redação", 1, CorrecaoIndependente), Entrevista("Entrevista", 2, AvaliacaoUnica));
        foreach (EtapaProcesso etapa in processo.Etapas)
        {
            etapa.DefinirBancas([BancaDaEtapa.Criar(Guid.CreateVersion7(), "BANCA_EXAMINADORA", "Banca examinadora", BancasDeTeste.Regra())])
                .IsSuccess.Should().BeTrue();
        }

        return processo;
    }

    /// <summary>
    /// O processo de uma etapa de correção independente publicado com a banca no mínimo que a regra exige, e depois com a banca
    /// redeclarada no número de integrantes dado, como o da versão que a retificação e o fechamento da sessão congelam.
    /// </summary>
    public static (ProcessoSeletivo Processo, VersaoConfiguracao Versao) PublicadoComBancaRedeclarada(int integrantes)
    {
        ProcessoSeletivo processo = ComBancaDaEtapa(integrantes: 3);
        VersaoConfiguracao versao = processo.Publicar(Pedido(processo, ComOsNumerosDoSeed())).Value!;
        processo.DequeueDomainEvents();
        DeclararBancaDaEtapa(processo.Etapas.Single(), integrantes, quorum: 2);

        return (processo, versao);
    }

    /// <summary>Os serviços do congelamento sobre o processo, com o calendário de dias úteis que o prazo do recurso exige.</summary>
    public static ServicosDoCongelamentoDeTeste Servicos(ProcessoSeletivo processo) =>
        ServicosSemCalendario(processo).ComCalendario(CalendarioVigenteReaderDeTeste.ComVigente());

    /// <summary>Os serviços do congelamento sobre o processo, sem calendário de dias úteis vigente no cadastro.</summary>
    public static ServicosDoCongelamentoDeTeste ServicosSemCalendario(ProcessoSeletivo processo)
    {
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);
        servicos.Repositorio.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        return servicos;
    }

    /// <summary>O pedido que o agregado congela quando o catálogo dá a cada regra o número dado, com a configuração apta.</summary>
    public static PedidoDeCongelamento Pedido(ProcessoSeletivo processo, CorretoresDasEtapas corretores) => new()
    {
        Dados = PublicacaoDeTeste.DadosDoEdital(),
        Documento = new DocumentoCongelado(EnvelopeCongeladoDeTeste.SemAnexos(), "1.1", "canonical-json/sha256@v1", ProcessoSeletivoConformeBuilder.HashFixo),
        AtorUsuarioContaId = "user-conta-1",
        Instante = PublicacaoDeTeste.Agora,
        ConfiguracaoApta = Apta(processo, corretores),
    };

    private static ConfiguracaoAptaACongelar Apta(ProcessoSeletivo processo, CorretoresDasEtapas corretores)
    {
        ContextoDeContagemDePrazos contexto = new(
            CalendarioDiasUteisCongelado.Criar(
                Guid.CreateVersion7(), "2026", [DiaNaoUtilCongelado.Criar(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null).Value!]).Value,
            FusoInstitucional: TimeZoneInfo.Utc);
        Result<ConfiguracaoAptaACongelar> conferida = ConfiguracaoAptaACongelar.Conferir(
            processo.Id, processo.Declaracao(), CadastrosDeTeste.Com(contexto, corretores: corretores));
        conferida.IsSuccess.Should().BeTrue(conferida.Error?.Message);

        return conferida.Value!;
    }

    /// <summary>Os números que o seed declara para as duas regras.</summary>
    public static CorretoresDasEtapas ComOsNumerosDoSeed() =>
        new(new Dictionary<ReferenciaRegra, CorretoresDaRegra> { [CorrecaoIndependente] = CorretoresDaRegra.SemNovoCorretor(3), [AvaliacaoUnica] = CorretoresDaRegra.SemNovoCorretor(1) });

    private static void DeclararBancaDaEtapa(EtapaProcesso etapa, int integrantes, int quorum) =>
        etapa.DefinirBancas([BancasDeTeste.DeRecursosDaEtapa(BancasDeTeste.Regra(integrantes, quorum))])
            .IsSuccess.Should().BeTrue();

    private static ProcessoSeletivo ComEtapas(ProcessoSeletivo processo, params EtapaProcesso[] etapas)
    {
        processo.DefinirEtapas(etapas).IsSuccess.Should().BeTrue();
        processo.DefinirAlgoritmoContagemPrazo(
            ReferenciaRegra.De(AlgoritmoContagemPrazoCodigo.AvancaDataUtil, "v1", ProcessoSeletivoConformeBuilder.HashFixo)).IsSuccess.Should().BeTrue();
        foreach (EtapaProcesso etapa in processo.Etapas)
        {
            ComRecurso(etapa);
        }

        return processo;
    }

    private static EtapaProcesso Entrevista(string nome, int ordem, ReferenciaRegra consolidacao) => EtapaProcesso.Criar(
        new IdentificacaoDaEtapa(
            Nome: nome, Carater: CaraterEtapa.Classificatoria,
            TipoEtapa: TipoEtapaSnapshot.Criar(CadastrosVivos.IdentidadeDe("ENTREVISTA"), "ENTREVISTA", "Entrevista", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
            Ordem: ordem, FaseCodigo: "RESULTADO_FINAL"),
        new DeclaracaoDaAvaliacaoDaEtapa(
            Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: 0.5m,
            FormaDeAvaliacao: FormaDeAvaliacao.Humana, RegraConsolidacao: consolidacao, EscalaDeAtendimento: null)).Value!;

    private static void ComRecurso(EtapaProcesso etapa)
    {
        etapa.DefinirProdutos(
        [
            ProdutoDaEtapa.Criar("RESULTADO_PROVA", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Criar("RESULTADO_PROVA", PapelProdutoFase.Definitivo),
        ]).IsSuccess.Should().BeTrue();
        Guid preliminar = etapa.Produtos.Single(produto => produto.Papel == PapelProdutoFase.Preliminar).Id;
        RegraRecursoFase prazo = RegraRecursoFase.Criar(
            ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", ProcessoSeletivoConformeBuilder.HashFixo),
            new ArgsRegraPrazoRecurso(48m, UnidadePrazo.Horas, null, null, false, null, null),
            preliminar).Value!;
        etapa.DefinirRecursos([RecursoDaEtapa.Criar(AncoraDoRecurso.AtoPublicado, prazo.Regra, prazo.Args, preliminar).Value!])
            .IsSuccess.Should().BeTrue();
    }
}

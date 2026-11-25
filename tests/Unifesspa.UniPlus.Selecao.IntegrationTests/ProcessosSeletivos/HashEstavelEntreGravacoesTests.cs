namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// O hash canônico responde uma pergunta: <i>esta configuração é a mesma que foi publicada?</i>
/// Ele é a base para detectar divergência entre o gravado e o publicado, para decidir se uma
/// retificação mudou algo, e para a chave de substituição que o editor usa. Um hash que oscila
/// sozinho torna as três respostas ruído.
/// </summary>
public sealed class HashEstavelEntreGravacoesTests
{
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();
    private static readonly string HashFixo = new('a', 64);
    private static readonly Guid TipoEtapaOrigem = Guid.CreateVersion7();
    private static readonly Guid TipoBancaOrigem = Guid.CreateVersion7();
    private static readonly Guid UnidadeOrigem = Guid.CreateVersion7();
    private static readonly Guid DocumentoAPublicar = Guid.CreateVersion7();
    private static readonly Guid DistribuicaoOrigem = Guid.CreateVersion7();
    private static readonly Guid ModalidadeOrigem = Guid.CreateVersion7();
    private static readonly Guid FaseOrigem = Guid.CreateVersion7();

    [Fact(DisplayName = "Regravar a etapa com o mesmo conteúdo produz os mesmos bytes canônicos")]
    public void RegravarComOMesmoConteudo_ProduzOsMesmosBytes()
    {
        ProcessoSeletivo processo = ProcessoComEtapaCompleta();
        byte[] antes = Canonicalizar(processo);

        // O cliente devolve exatamente o que leu, inclusive o id da banca — é o que o editor faz
        // ao salvar um passo sem ter tocado neste. Instâncias novas, mesmo conteúdo.
        RedeclararEtapaIdentica(processo);
        byte[] depois = Canonicalizar(processo);

        depois.Should().Equal(antes,
            "nada da configuração mudou, então o documento que a representa não pode mudar — "
            + "se muda, o hash deixa de responder se o gravado ainda é o que foi publicado");
    }

    [Fact(DisplayName = "A unidade responsável da fase entra nos bytes canônicos: regravá-la igual mantém, trocá-la muda")]
    public void UnidadeResponsavelDaFase_EntraNosBytesCanonicos()
    {
        ProcessoSeletivo processo = ProcessoComEtapaCompleta();
        byte[] comCeps = Canonicalizar(processo);

        processo.DefinirCronogramaFases([FaseDoCertame(UnidadesDeTeste.Ceps)], [])
            .IsSuccess.Should().BeTrue();
        Canonicalizar(processo).Should().Equal(comCeps,
            "regravar a fase com a mesma unidade não muda o documento");

        processo.DefinirCronogramaFases([FaseDoCertame(UnidadesDeTeste.Cga)], [])
            .IsSuccess.Should().BeTrue();
        byte[] comCga = Canonicalizar(processo);

        comCga.Should().NotEqual(comCeps, "a unidade responsável é conteúdo congelado: trocá-la muda o hash");
        ConferirUnidadeNoEnvelope(comCga);
    }

    private static byte[] Canonicalizar(ProcessoSeletivo processo) =>
        Canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(processo, DadosDeReferencia(), HashFixo, FusoInstitucional.ZoneId, Retificacao: null, Conformidade: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao))).Bytes;

    /// <summary>Certame publicável com uma etapa que declara produtos, banca e janelas recursais.</summary>
    private static ProcessoSeletivo ProcessoComEtapaCompleta()
    {
        ProcessoSeletivo processo = ProcessoBase();
        RedeclararEtapaIdentica(processo);
        return processo;
    }

    /// <summary>
    /// Declara a etapa com o mesmo conteúdo, sempre por instâncias novas — é o corpo que o
    /// cliente reenvia depois de reler a configuração.
    /// </summary>
    private static void RedeclararEtapaIdentica(ProcessoSeletivo processo)
    {
        EtapaProcesso etapa = processo.Etapas.Single();

        etapa.DefinirProdutos(ProdutosDaEtapaCompleta()).IsSuccess.Should().BeTrue();
        etapa.DefinirBancas([BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, TipoBancaOrigem), idDaBancaQueSubstitui: etapa.Bancas.SingleOrDefault()?.Id)]);
        etapa.DefinirRecursos(JanelasEmOrdemInversaDoDocumento(etapa)).IsSuccess.Should().BeTrue();
    }

    private static ProdutoDaEtapa[] ProdutosDaEtapaCompleta() =>
    [
        ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Preliminar),
        ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo),
        ProdutoDaEtapa.Criar("GABARITO", PapelDaPublicacaoPrevista.Preliminar),
    ];

    // Duas janelas, declaradas na ordem INVERSA à que o documento emite: a posição de cada
    // uma tem de sair do conteúdo da âncora, não da ordem de chegada nem do id sorteado da
    // linha do produto. Sem estas duas linhas, o bloco de recursos da etapa fica fora do
    // cenário de estabilidade de bytes.
    private static RecursoDaEtapa[] JanelasEmOrdemInversaDoDocumento(EtapaProcesso etapa) =>
    [
        JanelaAncoradaEm(etapa, "RESULTADO_CLASSIFICACAO"),
        JanelaAncoradaEm(etapa, "GABARITO"),
    ];

    /// <summary>
    /// Janela recursal que corre da publicação do produto preliminar do ato indicado — a
    /// âncora é resolvida DEPOIS da gravação dos produtos, contra a linha que sobreviveu à
    /// reconciliação, que é o que o handler do comando também faz.
    /// </summary>
    private static RecursoDaEtapa JanelaAncoradaEm(EtapaProcesso etapa, string atoCodigo) =>
        RecursoDaEtapa.Criar(
            EventoInicialDoPrazo.PublicacaoDoAto,
            ReferenciaRegra.De(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), "v1", HashFixo),
            ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas),
            etapa.Produtos.Single(p => p.Papel == PapelDaPublicacaoPrevista.Preliminar
                && string.Equals(p.AtoCodigo, atoCodigo, StringComparison.Ordinal)).Id).Value!;

    private static ProcessoSeletivo ProcessoBase()
    {
        ProcessoSeletivo processo = ProcessoComAtendimento();

        DefinirOfertaDeAmplaConcorrencia(processo);

        DefinirClassificacaoImportada(processo);
        processo.DefinirOpcoesDeCurso(1).IsSuccess.Should().BeTrue();

        DefinirCronogramaEEtapas(processo);

        return processo;
    }

    /// <summary>A fase única do certame, sob a unidade dada, sempre por instância nova.</summary>
    private static FaseCronograma FaseDoCertame(UnidadeResponsavelSnapshot unidadeResponsavel) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            OrigemId: FaseOrigem, Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: unidadeResponsavel),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
        bancasRequeridas: [BancaDeHomologacaoComIdentidadeFixa()]).Value!;

    // A banca com a mesma identidade em toda instância: o hash só se mantém entre gravações se a identidade da banca se mantém.
    private static BancaRequerida BancaDeHomologacaoComIdentidadeFixa() => BancaRequerida.ReconstruirDosDadosSalvos(ChaveLocal.Criar("banca").Value,
        new Guid("bbbb0000-0000-4000-8000-0000000000a1"),
        TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.HomologacaoDasInscricoes, new Guid("5555eeee-0000-4000-8000-0000000000a1")),
        [],
        BancasDeTeste.Regra());

    private static DadosEdital DadosDeReferencia() => DadosEdital.Criar(
        "001/2026",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        DocumentoAPublicar).Value!;

    private static void ConferirUnidadeNoEnvelope(byte[] comCga)
    {
        using JsonDocument envelope = JsonDocument.Parse(comCga);
        JsonElement unidade = envelope.RootElement.GetProperty("cronogramaFases").GetProperty("fases")[0].GetProperty("unidadeResponsavel");
        unidade.GetProperty("origemId").GetGuid().Should().Be(UnidadesDeTeste.Cga.OrigemId);
        unidade.GetProperty("sigla").GetString().Should().Be("CGA");
        unidade.GetProperty("nome").GetString().Should().Be("Centro de Gestão Acadêmica");
    }


    private static void DefinirOfertaDeAmplaConcorrencia(ProcessoSeletivo processo)
    {
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { Id = ModalidadeOrigem, RegraRemanejamento = null, BaseLegal = "Res. Unifesspa 532/2021" }, 40).Value!;
        processo.DefinirDistribuicaoVagas([ConfiguracaoDistribuicaoVagas.Criar(DistribuicaoOrigem,
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", HashFixo), Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }


    private static void DefinirClassificacaoImportada(ProcessoSeletivo processo)
    {
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), "v1", HashFixo),
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", HashFixo),
        }).Value!).IsSuccess.Should().BeTrue();
    }


    private static void DefinirCronogramaEEtapas(ProcessoSeletivo processo)
    {
        processo.DefinirCronogramaFases([FaseDoCertame(UnidadesDeTeste.Ceps)], []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(TipoEtapaOrigem, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();
    }


    private static ProcessoSeletivo ProcessoComAtendimento()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Hash 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, UnidadeOrigem,
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!)
            .IsSuccess.Should().BeTrue();

        return processo;
    }

}

namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

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
    private static readonly Guid DocumentoEdital = Guid.CreateVersion7();
    private static readonly Guid DistribuicaoOrigem = Guid.CreateVersion7();
    private static readonly Guid ModalidadeOrigem = Guid.CreateVersion7();
    private static readonly Guid FaseOrigem = Guid.CreateVersion7();

    [Fact(DisplayName = "Regravar a etapa com o mesmo conteúdo produz os mesmos bytes canônicos")]
    public void RegravarComOMesmoConteudo_ProduzOsMesmosBytes()
    {
        ProcessoSeletivo processo = ProcessoComEtapaCompleta();
        byte[] antes = Canonicalizar(processo);

        // O cliente devolve exatamente o que leu — é o que o editor faz ao salvar um passo sem
        // ter tocado neste. Instâncias novas, mesmo conteúdo.
        RedeclararEtapaIdentica(processo);
        byte[] depois = Canonicalizar(processo);

        depois.Should().Equal(antes,
            "nada da configuração mudou, então o documento que a representa não pode mudar — "
            + "se muda, o hash deixa de responder se o gravado ainda é o que foi publicado");
    }

    [Fact(DisplayName = "O ato do produto em NFC e em NFD produz os mesmos bytes canônicos")]
    public void AtoEmFormasUnicodeDiferentes_ProduzOsMesmosBytes()
    {
        // "AVALIAÇÃO" com Ç e Ã compostos (NFC) e decompostos (NFD): mesmo texto para o
        // operador, sequências de code points diferentes.
        const string EmNfc = "RESULTADO_AVALIAÇÃO";
        const string EmNfd = "RESULTADO_AVALIAÇÃO";

        // No MESMO processo: o produto declarado em NFC é regravado em NFD. Comparar dois
        // processos distintos não serviria enquanto o id do produto entrar nos bytes.
        ProcessoSeletivo processo = ProcessoComProdutoDeAto(EmNfc);
        byte[] comNfc = Canonicalizar(processo);

        processo.Etapas.Single()
            .DefinirProdutos([ProdutoDaEtapa.Criar(EmNfd, PapelProdutoFase.Preliminar)])
            .IsSuccess.Should().BeTrue();
        byte[] comNfd = Canonicalizar(processo);

        comNfd.Should().Equal(comNfc,
            "o bloco da fase normaliza para NFC desde sempre; o da etapa não normalizava, e dois "
            + "textos indistinguíveis na tela produziam hashes diferentes de um lado e iguais do outro");
    }

    private static byte[] Canonicalizar(ProcessoSeletivo processo) =>
        Canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(processo, DadosDeReferencia(), HashFixo, FusoInstitucional.ZoneId)).Bytes;

    /// <summary>Certame publicável com uma etapa que declara produtos, banca e janela recursal.</summary>
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

        etapa.DefinirProdutos([
            ProdutoDaEtapa.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Definitivo),
        ]).IsSuccess.Should().BeTrue();

        etapa.DefinirBancas([BancaDaEtapa.Criar(TipoBancaOrigem, "BANCA_TECNICA")])
            .IsSuccess.Should().BeTrue();
    }

    private static ProcessoSeletivo ProcessoComProdutoDeAto(string atoCodigo)
    {
        ProcessoSeletivo processo = ProcessoBase();
        processo.Etapas.Single()
            .DefinirProdutos([ProdutoDaEtapa.Criar(atoCodigo, PapelProdutoFase.Preliminar)])
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    private static ProcessoSeletivo ProcessoBase()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Hash 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, UnidadeOrigem,
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                "Prova Objetiva", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(TipoEtapaOrigem, "PROVA_OBJETIVA", "Prova Objetiva").Value!,
                peso: 1m, ordem: 1).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // O canonicalizador exige a configuração que o gate de publicação já teria cobrado.
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            ModalidadeOrigem, "AC", null, NaturezaLegalModalidade.Ampla,
            ComposicaoVagasModalidade.ResidualDoVo, null, RegraRemanejamentoModalidade.Nenhuma,
            null, null, null, [], null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas([ConfiguracaoDistribuicaoVagas.Criar(
            DistribuicaoOrigem, 40, 1m,
            ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!,
            null, null, [modalidade]).Value!], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!, null, null,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!,
            1, [], baseadoEmEnem: false).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases([FaseCronograma.Criar(
            ordem: 1, faseCanonicaOrigemId: FaseOrigem, codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS", origemData: OrigemDataFase.Propria, agrupaEtapas: true,
            permiteComplementacao: false, coletaInscricao: true, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [],
            regraRecurso: null).Value!], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static DadosEdital DadosDeReferencia() => DadosEdital.Criar(
        "001/2026",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        DocumentoEdital).Value!;
}

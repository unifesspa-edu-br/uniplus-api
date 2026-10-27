namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// O gate da forma de avaliação: a etapa com nota declara como a nota é obtida, a que não tem nota
/// não declara, e a forma humana exige a regra de consolidação, o passo do lançamento e ao menos
/// uma banca. É gate, e não recusa de gravação, porque depende do que o grafo diz sobre a etapa.
/// </summary>
public sealed class ConformidadeAvaliacaoDasEtapasTests
{
    private static readonly string HashFixo = new('a', 64);

    private static TipoEtapaSnapshot Tipo() =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    private static EtapaProcesso Etapa(
        string nome,
        int ordem,
        FormaDeAvaliacao? forma,
        CaraterEtapa carater = CaraterEtapa.Classificatoria,
        decimal? passo = null,
        bool comRegra = false,
        bool comBanca = false,
        IReadOnlyList<ItemDeAvaliacaoDeclarado>? itens = null,
        IReadOnlyList<NivelDeAtendimento>? escala = null)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            nome, carater, Tipo(), carater == CaraterEtapa.Classificatoria ? 1m : null, null, ordem,
            notaMaxima: carater == CaraterEtapa.Classificatoria ? 10m : null, passoDoLancamento: passo, formaDeAvaliacao: forma,
            regraConsolidacao: comRegra
                ? ProcessoConformeFactory.Regra(RegraConsolidacaoCodigo.AvaliacaoUnica, "b")
                : null,
            itensDeAvaliacao: itens, escalaDeAtendimento: escala).Value!;
        if (comBanca)
        {
            etapa.DefinirBancas([BancaDaEtapa.Criar(Guid.CreateVersion7(), "BANCA_AVALIADORA")]).IsSuccess.Should().BeTrue();
        }

        return etapa;
    }

    private static ProcessoSeletivo ProcessoCom(params EtapaProcesso[] etapas)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirEtapas(etapas, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static IEnumerable<ItemConformidade> Vermelhos(ProcessoSeletivo processo) =>
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum)
            .Where(static i => !i.Ok && i.Dimensao == DimensaoConformidade.Etapas);

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(
        ProcessoConformeFactory.Dados(), "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "teste",
        TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

    [Fact(DisplayName = "A etapa com nota sem forma de avaliação recusa a publicação; com forma publica")]
    public void EtapaComNotaSemForma_RecusaPublicacao()
    {
        ProcessoSeletivo semForma = ProcessoCom(Etapa("Prova", 1, forma: null));

        Vermelhos(semForma).Should().ContainSingle().Which.Should().Match<ItemConformidade>(
            i => i.Codigo == "etapa_com_nota_sem_forma_de_avaliacao" && i.Mensagem.Contains("\"Prova\"", StringComparison.Ordinal));
        Publicar(semForma).IsFailure.Should().BeTrue();

        ProcessoSeletivo comForma = ProcessoCom(Etapa("Prova", 1, FormaDeAvaliacao.Objetiva));
        Vermelhos(comForma).Should().BeEmpty();
        Publicar(comForma).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A etapa sem nota recusa a publicação se declara forma de avaliação")]
    public void EtapaSemNotaComForma_RecusaPublicacao()
    {
        ProcessoSeletivo processo = ProcessoCom(
            Etapa("Prova", 1, FormaDeAvaliacao.Objetiva),
            Etapa("Análise", 2, FormaDeAvaliacao.Objetiva, CaraterEtapa.Eliminatoria));

        Vermelhos(processo).Should().ContainSingle().Which.Should().Match<ItemConformidade>(
            i => i.Codigo == "etapa_sem_nota_com_forma_de_avaliacao" && i.Mensagem.Contains("\"Análise\"", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A forma humana exige regra, passo e banca, e a recusa lista tudo o que falta de uma vez")]
    public void EtapaHumanaIncompleta_AcumulaAsPendencias()
    {
        ProcessoSeletivo processo = ProcessoCom(Etapa("Redação", 1, FormaDeAvaliacao.Humana));

        Vermelhos(processo).Select(static i => i.Codigo).Should().BeEquivalentTo(
            "etapa_humana_sem_regra_de_consolidacao", "etapa_humana_sem_passo_do_lancamento", "etapa_humana_sem_banca");
        Publicar(processo).IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "A etapa humana sem nota gera uma só pendência: a da forma, sem mandar completar regra, passo e banca")]
    public void EtapaHumanaSemNota_GeraUmaSoPendencia()
    {
        ProcessoSeletivo processo = ProcessoCom(
            Etapa("Prova", 1, FormaDeAvaliacao.Objetiva),
            Etapa("Análise", 2, FormaDeAvaliacao.Humana, CaraterEtapa.Eliminatoria));

        Vermelhos(processo).Should().ContainSingle().Which.Codigo.Should().Be("etapa_sem_nota_com_forma_de_avaliacao");
    }

    [Fact(DisplayName = "A etapa de avaliação humana completa publica")]
    public void EtapaHumanaCompleta_Publica()
    {
        ProcessoSeletivo processo = ProcessoCom(
            Etapa("Redação", 1, FormaDeAvaliacao.Humana, passo: 0.5m, comRegra: true, comBanca: true));

        Vermelhos(processo).Should().BeEmpty();
        Publicar(processo).IsSuccess.Should().BeTrue();
    }

    private static readonly NivelDeAtendimento[] Escala = [new("Atendeu", 1m), new("Atendeu parcialmente", 0.5m), new("Não atendeu", 0m)];

    [Fact(DisplayName = "A soma das pontuações dos itens diferente da nota máxima recusa a publicação; igual publica")]
    public void ItensForaDaNotaMaxima_RecusaPublicacao()
    {
        ProcessoSeletivo divergente = ProcessoCom(Etapa(
            "Prova", 1, FormaDeAvaliacao.Objetiva, itens: [new(null, "Bloco A", 4m), new(null, "Bloco B", 5m)]));

        Vermelhos(divergente).Should().ContainSingle().Which.Should().Match<ItemConformidade>(
            i => i.Codigo == "etapa_com_itens_fora_da_nota_maxima" && i.Mensagem.Contains("\"Prova\"", StringComparison.Ordinal));
        Publicar(divergente).IsFailure.Should().BeTrue();

        ProcessoSeletivo igual = ProcessoCom(Etapa(
            "Prova", 1, FormaDeAvaliacao.Objetiva, itens: [new(null, "Bloco A", 4m), new(null, "Bloco B", 6m)]));
        Vermelhos(igual).Should().BeEmpty();
        Publicar(igual).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A etapa sem nota recusa a publicação se declara itens ou escala, e só essa pendência é listada")]
    public void EtapaSemNotaComItens_RecusaPublicacao()
    {
        ProcessoSeletivo processo = ProcessoCom(
            Etapa("Prova", 1, FormaDeAvaliacao.Objetiva),
            Etapa("Análise", 2, forma: null, CaraterEtapa.Eliminatoria, itens: [new(null, "Documentação", 10m)]));

        Vermelhos(processo).Should().ContainSingle().Which.Should().Match<ItemConformidade>(
            i => i.Codigo == "etapa_sem_nota_com_itens_de_avaliacao" && i.Mensagem.Contains("\"Análise\"", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Com escala de atendimento a etapa humana publica sem passo do lançamento; sem escala o passo continua exigido")]
    public void EtapaHumanaComEscala_DispensaOPasso()
    {
        ProcessoSeletivo comEscala = ProcessoCom(Etapa(
            "Entrevista", 1, FormaDeAvaliacao.Humana, comRegra: true, comBanca: true,
            itens: [new(null, "Postura", 5m), new(null, "Domínio", 5m)], escala: Escala));

        Vermelhos(comEscala).Should().BeEmpty();
        Publicar(comEscala).IsSuccess.Should().BeTrue();

        ProcessoSeletivo semEscala = ProcessoCom(Etapa(
            "Entrevista", 1, FormaDeAvaliacao.Humana, comRegra: true, comBanca: true,
            itens: [new(null, "Postura", 5m), new(null, "Domínio", 5m)]));

        Vermelhos(semEscala).Should().ContainSingle().Which.Codigo.Should().Be("etapa_humana_sem_passo_do_lancamento");
    }
}

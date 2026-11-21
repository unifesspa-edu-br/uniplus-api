namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// A conferência da forma de avaliação: a etapa com nota declara como a nota é obtida, a que não tem nota
/// não declara, e a forma humana exige a regra de consolidação, o passo do lançamento e ao menos
/// uma banca. É conferência, e não recusa de gravação, porque depende do que o grafo diz sobre a etapa.
/// </summary>
public sealed class ConformidadeAvaliacaoDasEtapasTests
{
    private static readonly string HashFixo = new('a', 64);

    private static TipoEtapaSnapshot Tipo() =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    private static ProcessoSeletivo ProcessoCom(params EtapaProcesso[] etapas)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirEtapas(etapas).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static IEnumerable<ItemConformidade> Vermelhos(ProcessoSeletivo processo) =>
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Where(static i => !i.Ok && i.Dimensao == DimensaoConformidade.Etapas);

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(
        new CenarioDeCongelamento { Dados = ProcessoConformeFactory.Dados(), Documento = new DocumentoCongelado("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo), AtorUsuarioContaId = "teste" });

    [Fact(DisplayName = "A etapa com nota sem forma de avaliação recusa a publicação; com forma publica")]
    public void EtapaComNotaSemForma_RecusaPublicacao()
    {
        ProcessoSeletivo semForma = ProcessoCom(new ModeloDeEtapa("Prova") { Ordem = 1, FaseCodigo = "RESULTADO_FINAL", Forma = null }.Criar());

        Vermelhos(semForma).Should().ContainSingle().Which.Should().Match<ItemConformidade>(
            i => i.Codigo == "etapa_com_nota_sem_forma_de_avaliacao" && i.Mensagem.Contains("\"Prova\"", StringComparison.Ordinal));
        Publicar(semForma).IsFailure.Should().BeTrue();

        ProcessoSeletivo comForma = ProcessoCom(new ModeloDeEtapa("Prova") { Ordem = 1, FaseCodigo = "RESULTADO_FINAL" }.Criar());
        Vermelhos(comForma).Should().BeEmpty();
        Publicar(comForma).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A etapa sem nota recusa a publicação se declara forma de avaliação")]
    public void EtapaSemNotaComForma_RecusaPublicacao()
    {
        ProcessoSeletivo processo = ProcessoCom(
            new ModeloDeEtapa("Prova") { Ordem = 1, FaseCodigo = "RESULTADO_FINAL" }.Criar(),
            new ModeloDeEtapa("Análise") { Ordem = 2, FaseCodigo = "RESULTADO_FINAL", Carater = CaraterEtapa.Eliminatoria, Peso = null, NotaMaxima = null }.Criar());

        Vermelhos(processo).Should().ContainSingle().Which.Should().Match<ItemConformidade>(
            i => i.Codigo == "etapa_sem_nota_com_forma_de_avaliacao" && i.Mensagem.Contains("\"Análise\"", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A forma humana exige regra, passo e banca, e a recusa lista tudo o que falta de uma vez")]
    public void EtapaHumanaIncompleta_AcumulaAsPendencias()
    {
        ProcessoSeletivo processo = ProcessoCom(new ModeloDeEtapa("Redação") { Ordem = 1, FaseCodigo = "RESULTADO_FINAL", Forma = FormaDeAvaliacao.Humana }.Criar());

        Vermelhos(processo).Select(static i => i.Codigo).Should().BeEquivalentTo(
            "etapa_humana_sem_regra_de_consolidacao", "etapa_humana_sem_passo_do_lancamento", "etapa_humana_sem_banca");
        Publicar(processo).IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "A etapa humana sem nota gera uma só pendência: a da forma, sem mandar completar regra, passo e banca")]
    public void EtapaHumanaSemNota_GeraUmaSoPendencia()
    {
        ProcessoSeletivo processo = ProcessoCom(
            new ModeloDeEtapa("Prova") { Ordem = 1, FaseCodigo = "RESULTADO_FINAL" }.Criar(),
            new ModeloDeEtapa("Análise") { Ordem = 2, FaseCodigo = "RESULTADO_FINAL", Carater = CaraterEtapa.Eliminatoria, Peso = null, NotaMaxima = null, Forma = FormaDeAvaliacao.Humana }.Criar());

        Vermelhos(processo).Should().ContainSingle().Which.Codigo.Should().Be("etapa_sem_nota_com_forma_de_avaliacao");
    }

    [Fact(DisplayName = "A etapa de avaliação humana completa publica")]
    public void EtapaHumanaCompleta_Publica()
    {
        ProcessoSeletivo processo = ProcessoCom(
            new ModeloDeEtapa("Redação") { Ordem = 1, FaseCodigo = "RESULTADO_FINAL", Forma = FormaDeAvaliacao.Humana, RegraConsolidacao = ProcessoConformeFactory.Regra(RegraConsolidacaoCodigo.AvaliacaoUnica, "b"), Bancas = [BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Guid.CreateVersion7()), BancasDeTeste.Regra())], Passo = 0.5m }.Criar());

        Vermelhos(processo).Should().BeEmpty();
        Publicar(processo).IsSuccess.Should().BeTrue();
    }

    private static readonly NivelDeAtendimento[] Escala = [new("Atendeu", 1m), new("Atendeu parcialmente", 0.5m), new("Não atendeu", 0m)];

    [Fact(DisplayName = "A soma das pontuações dos itens diferente da nota máxima recusa a publicação; igual publica")]
    public void ItensForaDaNotaMaxima_RecusaPublicacao()
    {
        ProcessoSeletivo divergente = ProcessoCom(new ModeloDeEtapa("Prova") { Ordem = 1, FaseCodigo = "RESULTADO_FINAL", Itens = [new(null, "Bloco A", 4m), new(null, "Bloco B", 5m)] }.Criar());

        Vermelhos(divergente).Should().ContainSingle().Which.Should().Match<ItemConformidade>(
            i => i.Codigo == "etapa_com_itens_fora_da_nota_maxima" && i.Mensagem.Contains("\"Prova\"", StringComparison.Ordinal));
        Publicar(divergente).IsFailure.Should().BeTrue();

        ProcessoSeletivo igual = ProcessoCom(new ModeloDeEtapa("Prova") { Ordem = 1, FaseCodigo = "RESULTADO_FINAL", Itens = [new(null, "Bloco A", 4m), new(null, "Bloco B", 6m)] }.Criar());
        Vermelhos(igual).Should().BeEmpty();
        Publicar(igual).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A etapa sem nota recusa a publicação se declara itens ou escala, e só essa pendência é listada")]
    public void EtapaSemNotaComItens_RecusaPublicacao()
    {
        ProcessoSeletivo processo = ProcessoCom(
            new ModeloDeEtapa("Prova") { Ordem = 1, FaseCodigo = "RESULTADO_FINAL" }.Criar(),
            new ModeloDeEtapa("Análise") { Ordem = 2, FaseCodigo = "RESULTADO_FINAL", Carater = CaraterEtapa.Eliminatoria, Peso = null, NotaMaxima = null, Forma = null, Itens = [new(null, "Documentação", 10m)] }.Criar());

        Vermelhos(processo).Should().ContainSingle().Which.Should().Match<ItemConformidade>(
            i => i.Codigo == "etapa_sem_nota_com_itens_de_avaliacao" && i.Mensagem.Contains("\"Análise\"", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Com escala de atendimento a etapa humana publica sem passo do lançamento; sem escala o passo continua exigido")]
    public void EtapaHumanaComEscala_DispensaOPasso()
    {
        ProcessoSeletivo comEscala = ProcessoCom(new ModeloDeEtapa("Entrevista") { Ordem = 1, FaseCodigo = "RESULTADO_FINAL", Forma = FormaDeAvaliacao.Humana, RegraConsolidacao = ProcessoConformeFactory.Regra(RegraConsolidacaoCodigo.AvaliacaoUnica, "b"), Bancas = [BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Guid.CreateVersion7()), BancasDeTeste.Regra())], Itens = [new(null, "Postura", 5m), new(null, "Domínio", 5m)], Escala = Escala }.Criar());

        Vermelhos(comEscala).Should().BeEmpty();
        Publicar(comEscala).IsSuccess.Should().BeTrue();

        ProcessoSeletivo semEscala = ProcessoCom(new ModeloDeEtapa("Entrevista") { Ordem = 1, FaseCodigo = "RESULTADO_FINAL", Forma = FormaDeAvaliacao.Humana, RegraConsolidacao = ProcessoConformeFactory.Regra(RegraConsolidacaoCodigo.AvaliacaoUnica, "b"), Bancas = [BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Guid.CreateVersion7()), BancasDeTeste.Regra())], Itens = [new(null, "Postura", 5m), new(null, "Domínio", 5m)] }.Criar());

        Vermelhos(semEscala).Should().ContainSingle().Which.Codigo.Should().Be("etapa_humana_sem_passo_do_lancamento");
    }
}

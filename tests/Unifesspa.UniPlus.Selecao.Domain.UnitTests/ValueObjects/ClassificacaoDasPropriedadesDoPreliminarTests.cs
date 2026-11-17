namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Salvaguarda da <c>EntradaDoPreliminar</c>: toda propriedade pública da fase e da etapa está classificada
/// como entrada do preliminar ou não, com o motivo. A propriedade nova que ninguém classificou reprova o teste,
/// de modo que um dado de entrada não escape da conferência do ciclo novo só porque a entidade ganhou um campo.
/// </summary>
public sealed class ClassificacaoDasPropriedadesDoPreliminarTests
{
    private const string IdentidadeEAuditoria = "Identidade e auditoria da linha: não descrevem o que o preliminar usa.";
    private const string PublicacoesDeclaradas = "O que a fase ou a etapa publica: a mudança das publicações é conferida à parte, na saída ou troca do item.";

    internal static readonly Dictionary<string, Classificacao> DaFase = new()
    {
        [nameof(FaseCronograma.Id)] = NaoEntra(IdentidadeEAuditoria),
        [nameof(FaseCronograma.CreatedAt)] = NaoEntra(IdentidadeEAuditoria),
        [nameof(FaseCronograma.UpdatedAt)] = NaoEntra(IdentidadeEAuditoria),
        [nameof(FaseCronograma.DomainEvents)] = NaoEntra(IdentidadeEAuditoria),
        [nameof(FaseCronograma.ProcessoSeletivoId)] = NaoEntra("Vínculo com o processo, igual em toda versão."),
        [nameof(FaseCronograma.Ordem)] = NaoEntra("Posição na linha do tempo: o que muda o preliminar é o prazo e a unidade, não o número da posição."),
        [nameof(FaseCronograma.FaseCanonicaOrigemId)] = NaoEntra("É a identidade da fase entre versões."),
        [nameof(FaseCronograma.Codigo)] = Entra("O código vem do conjunto canônico e diz qual fase ela é: trocá-lo troca a fase."),
        [nameof(FaseCronograma.UnidadeResponsavelOrigemId)] = Entra("A unidade que responde pela fase define quem decide o preliminar."),
        [nameof(FaseCronograma.UnidadeResponsavelSigla)] = NaoEntra("Cópia de apresentação da unidade; a identidade é a origem."),
        [nameof(FaseCronograma.UnidadeResponsavelNome)] = NaoEntra("Cópia de apresentação da unidade; a identidade é a origem."),
        [nameof(FaseCronograma.UnidadeResponsavel)] = NaoEntra("Derivada da origem, da sigla e do nome."),
        [nameof(FaseCronograma.OrigemData)] = Entra("Quando a data é fixada decide a obrigatoriedade da janela."),
        [nameof(FaseCronograma.AgrupaEtapas)] = Entra("Muda de onde vem a nota da fase."),
        [nameof(FaseCronograma.PermiteComplementacao)] = Entra("Muda o que o candidato pode acrescentar antes da decisão."),
        [nameof(FaseCronograma.ColetaInscricao)] = Entra("Muda quem entra no certame."),
        [nameof(FaseCronograma.ColetaSolicitacaoIsencao)] = Entra("Muda quem pede isenção."),
        [nameof(FaseCronograma.Inicio)] = Entra("A janela da fase delimita o que o preliminar considera."),
        [nameof(FaseCronograma.Fim)] = Entra("A janela da fase delimita o que o preliminar considera."),
        [nameof(FaseCronograma.FaseConcluinteCodigo)] = Entra("Muda a fase que fecha o ciclo do preliminar."),
        [nameof(FaseCronograma.EmiteParecerIndividual)] = Entra("O parecer individual é o fundamento do recurso."),
        [nameof(FaseCronograma.ProduzResultado)] = NaoEntra("Derivada de Produtos."),
        [nameof(FaseCronograma.RegraRecurso)] = Entra("A regra de prazo e os procedimentos do recurso mudam o que o candidato pode contestar e como o recurso é revisado."),
        [nameof(FaseCronograma.Produtos)] = NaoEntra(PublicacoesDeclaradas),
        [nameof(FaseCronograma.BancasRequeridas)] = Entra("Quem julga e como delibera define a decisão do preliminar."),
        [nameof(FaseCronograma.TiposDeDecisao)] = Entra("Os tipos de decisão dizem o que o preliminar decide e o que dele se pode contestar."),
        [nameof(FaseCronograma.RegraDeHomologacao)] = NaoEntra("Ainda fora do envelope congelado; passa a entrar no preliminar quando entrar no envelope."),
    };

    internal static readonly Dictionary<string, Classificacao> DaEtapa = new()
    {
        [nameof(EtapaProcesso.Id)] = NaoEntra(IdentidadeEAuditoria),
        [nameof(EtapaProcesso.CreatedAt)] = NaoEntra(IdentidadeEAuditoria),
        [nameof(EtapaProcesso.UpdatedAt)] = NaoEntra(IdentidadeEAuditoria),
        [nameof(EtapaProcesso.DomainEvents)] = NaoEntra(IdentidadeEAuditoria),
        [nameof(EtapaProcesso.ProcessoSeletivoId)] = NaoEntra("Vínculo com o processo, igual em toda versão."),
        [nameof(EtapaProcesso.FaseCodigo)] = Entra("Muda a fase onde a etapa acontece."),
        [nameof(EtapaProcesso.Nome)] = NaoEntra("Rótulo editorial da etapa."),
        [nameof(EtapaProcesso.Carater)] = Entra("Classificatória ou eliminatória muda o efeito da nota."),
        [nameof(EtapaProcesso.TipoEtapa)] = NaoEntra("Cópia do tipo de etapa: entra só a origem."),
        [nameof(EtapaProcesso.TipoEtapaOrigemId)] = Entra("O tipo de etapa define o que a etapa avalia."),
        [nameof(EtapaProcesso.Peso)] = Entra("O peso muda a nota que compõe o preliminar."),
        [nameof(EtapaProcesso.NotaMinima)] = Entra("A nota mínima muda quem segue no certame."),
        [nameof(EtapaProcesso.NotaMaxima)] = Entra("O teto da escala muda a nota lançada."),
        [nameof(EtapaProcesso.PassoDoLancamento)] = Entra("O passo muda as notas que se podem lançar."),
        [nameof(EtapaProcesso.FormaDeAvaliacao)] = Entra("Muda como a nota da etapa é obtida."),
        [nameof(EtapaProcesso.RegraConsolidacao)] = Entra("Muda como as notas dos avaliadores viram a nota da etapa."),
        [nameof(EtapaProcesso.EscalaDeAtendimento)] = Entra("Muda os níveis entre os quais a banca escolhe."),
        [nameof(EtapaProcesso.Ordem)] = NaoEntra("Posição da etapa na apresentação."),
        [nameof(EtapaProcesso.Inicio)] = Entra("A janela da etapa delimita o que o preliminar considera."),
        [nameof(EtapaProcesso.Fim)] = Entra("A janela da etapa delimita o que o preliminar considera."),
        [nameof(EtapaProcesso.EmiteParecerIndividual)] = Entra("O parecer individual é o fundamento do recurso."),
        [nameof(EtapaProcesso.ItensDeAvaliacao)] = Entra("Os itens e a pontuação máxima de cada um compõem a nota."),
        [nameof(EtapaProcesso.Bancas)] = Entra("Quem julga e como delibera define a decisão do preliminar."),
        [nameof(EtapaProcesso.Recursos)] = Entra("As janelas recursais, com o prazo e os procedimentos de revisão, mudam o que o candidato pode contestar e como o recurso é revisado."),
        [nameof(EtapaProcesso.TiposDeDecisao)] = Entra("Os tipos de decisão dizem o que o preliminar decide e o que dele se pode contestar."),
        [nameof(EtapaProcesso.Produtos)] = NaoEntra(PublicacoesDeclaradas),
        [nameof(EtapaProcesso.ProduzResultado)] = NaoEntra("Derivada de Produtos."),
        [nameof(EtapaProcesso.ComponeNota)] = NaoEntra("Derivada do caráter e do peso, que entram."),
        [nameof(EtapaProcesso.DeclaraNotaDoEnem)] = NaoEntra("Derivada do tipo de etapa, cuja origem entra."),
    };

    [Fact(DisplayName = "Toda propriedade pública da fase está classificada como entrada do preliminar ou não")]
    public void Fase_TodaPropriedadePublicaEstaClassificada() =>
        PropriedadesSemClassificacao(typeof(FaseCronograma), DaFase).Should().BeEmpty(
            "propriedade nova de FaseCronograma precisa ser classificada neste teste, dizendo se entra na EntradaDoPreliminar");

    [Fact(DisplayName = "A classificação da fase não cita propriedade que deixou de existir")]
    public void Fase_ClassificacaoNaoCitaPropriedadeInexistente() =>
        ClassificadasQueNaoExistem(typeof(FaseCronograma), DaFase).Should().BeEmpty(
            "propriedade removida de FaseCronograma sai da classificação");

    [Fact(DisplayName = "Toda propriedade pública da etapa está classificada como entrada do preliminar ou não")]
    public void Etapa_TodaPropriedadePublicaEstaClassificada() =>
        PropriedadesSemClassificacao(typeof(EtapaProcesso), DaEtapa).Should().BeEmpty(
            "propriedade nova de EtapaProcesso precisa ser classificada neste teste, dizendo se entra na EntradaDoPreliminar");

    [Fact(DisplayName = "A classificação da etapa não cita propriedade que deixou de existir")]
    public void Etapa_ClassificacaoNaoCitaPropriedadeInexistente() =>
        ClassificadasQueNaoExistem(typeof(EtapaProcesso), DaEtapa).Should().BeEmpty(
            "propriedade removida de EtapaProcesso sai da classificação");

    [Fact(DisplayName = "Toda classificação diz o motivo")]
    public void Toda_ClassificacaoDizOMotivo() =>
        DaFase.Concat(DaEtapa).Where(static c => string.IsNullOrWhiteSpace(c.Value.Motivo)).Select(static c => c.Key)
            .Should().BeEmpty("a classificação sem motivo não explica a decisão a quem altera a entidade");

    /// <summary>As propriedades classificadas como entrada do preliminar, para os testes que provam a variação de cada uma.</summary>
    internal static IEnumerable<string> PropriedadesQueEntram(Dictionary<string, Classificacao> classificadas) =>
        classificadas.Where(static c => c.Value.EntraNaEntrada).Select(static c => c.Key).Order(StringComparer.Ordinal);

    private static IEnumerable<string> PropriedadesPublicas(System.Type tipo) =>
        tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy).Select(static p => p.Name);

    private static IEnumerable<string> PropriedadesSemClassificacao(System.Type tipo, Dictionary<string, Classificacao> classificadas) =>
        PropriedadesPublicas(tipo).Where(nome => !classificadas.ContainsKey(nome)).Order(StringComparer.Ordinal);

    private static IEnumerable<string> ClassificadasQueNaoExistem(System.Type tipo, Dictionary<string, Classificacao> classificadas) =>
        classificadas.Keys.Except(PropriedadesPublicas(tipo)).Order(StringComparer.Ordinal);

    private static Classificacao Entra(string motivo) => new(EntraNaEntrada: true, motivo);

    private static Classificacao NaoEntra(string motivo) => new(EntraNaEntrada: false, motivo);

    internal sealed record Classificacao(bool EntraNaEntrada, string Motivo);
}

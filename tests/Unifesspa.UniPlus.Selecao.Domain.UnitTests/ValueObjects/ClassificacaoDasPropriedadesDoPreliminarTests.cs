namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>As propriedades da fase e da etapa consideradas na entrada do preliminar.</summary>
public sealed class ClassificacaoDasPropriedadesDoPreliminarTests
{
    private const string IdentidadeEAuditoria = "Identidade e auditoria da linha: não descrevem o que o preliminar usa.";
    private const string PublicacoesDeclaradas = "O que a fase ou a etapa publica: a mudança das publicações é conferida à parte, na saída ou troca do item.";

    internal static readonly Dictionary<string, Classificacao> DaFase = new()
    {
        [nameof(FaseCronograma.Chave)] = NaoEntra("Identidade local estável da fase."),
        [nameof(FaseCronograma.Id)] = NaoEntra(IdentidadeEAuditoria),
        [nameof(FaseCronograma.CreatedAt)] = NaoEntra(IdentidadeEAuditoria),
        [nameof(FaseCronograma.UpdatedAt)] = NaoEntra(IdentidadeEAuditoria),
        [nameof(FaseCronograma.DomainEvents)] = NaoEntra(IdentidadeEAuditoria),
        [nameof(FaseCronograma.ProcessoSeletivoId)] = NaoEntra("Vínculo com o processo, igual em toda versão."),
        [nameof(FaseCronograma.Ordem)] = NaoEntra("Posição na linha do tempo: o que muda o preliminar é o prazo e a unidade, não o número da posição."),
        [nameof(FaseCronograma.FaseCanonicaOrigemId)] = NaoEntra("É a identidade da fase entre versões."),
        [nameof(FaseCronograma.TipoFaseNome)] = NaoEntra("É cópia do nome do tipo de fase, só para leitura: trocar o nome não troca a fase."),
        [nameof(FaseCronograma.Citacao)] = NaoEntra("Derivada de TipoFaseNome, que já não entra."),
        [nameof(FaseCronograma.NoMesmoMomentoDaAnterior)] = NaoEntra("Muda o momento da fase, e a retificação confere o momento à parte, pela marca e pelas fases que vêm antes."),
        [nameof(FaseCronograma.Codigo)] = Entra("O código vem do conjunto canônico e diz qual fase ela é: trocá-lo troca a fase."),
        [nameof(FaseCronograma.UnidadeResponsavel)] = Entra("A origem da unidade define quem decide o preliminar; código, sigla e nome não mudam a entrada."),
        [nameof(FaseCronograma.OrigemData)] = Entra("Quando a data é fixada decide a obrigatoriedade do período."),
        [nameof(FaseCronograma.AgrupaEtapas)] = Entra("Muda de onde vem a nota da fase."),
        [nameof(FaseCronograma.PermiteComplementacao)] = Entra("Muda o que o candidato pode acrescentar antes da decisão."),
        [nameof(FaseCronograma.ColetaInscricao)] = Entra("Muda quem entra no certame."),
        [nameof(FaseCronograma.ColetaSolicitacaoIsencao)] = Entra("Muda quem pede isenção."),
        [nameof(FaseCronograma.OrganizadaEmChamadas)] = NaoEntra("Só decide se o edital exige formulário obrigatório na fase; não muda o resultado que o preliminar publica."),
        [nameof(FaseCronograma.Inicio)] = Entra("O período da fase delimita o que o preliminar considera."),
        [nameof(FaseCronograma.Fim)] = Entra("O período da fase delimita o que o preliminar considera."),
        [nameof(FaseCronograma.EmiteParecerIndividual)] = Entra("O parecer individual é o fundamento do recurso."),
        [nameof(FaseCronograma.ProduzResultado)] = NaoEntra("Derivada de Produtos."),
        [nameof(FaseCronograma.RegraRecurso)] = Entra("A regra de prazo e os procedimentos do recurso mudam o que o candidato pode contestar e como o recurso é revisado."),
        [nameof(FaseCronograma.Produtos)] = NaoEntra(PublicacoesDeclaradas),
        [nameof(FaseCronograma.BancasRequeridas)] = Entra("Quem julga e como delibera define a decisão do preliminar."),
        [nameof(FaseCronograma.TiposDeDecisao)] = Entra("Os tipos de decisão dizem o que o preliminar decide e o que dele se pode contestar."),
        [nameof(FaseCronograma.RegraDeHomologacao)] = NaoEntra("A regra de homologação das inscrições não entra na comparação do preliminar."),
    };

    internal static readonly Dictionary<string, Classificacao> DaEtapa = new()
    {
        [nameof(EtapaProcesso.Chave)] = NaoEntra("Identidade local estável da etapa."),
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
        [nameof(EtapaProcesso.Inicio)] = Entra("O período da etapa delimita o que o preliminar considera."),
        [nameof(EtapaProcesso.Fim)] = Entra("O período da etapa delimita o que o preliminar considera."),
        [nameof(EtapaProcesso.EmiteParecerIndividual)] = Entra("O parecer individual é o fundamento do recurso."),
        [nameof(EtapaProcesso.ItensDeAvaliacao)] = Entra("Os itens e a pontuação máxima de cada um compõem a nota."),
        [nameof(EtapaProcesso.Bancas)] = Entra("Quem julga e como delibera define a decisão do preliminar."),
        [nameof(EtapaProcesso.Recursos)] = Entra("Os períodos de recurso, com o prazo e os procedimentos de revisão, mudam o que o candidato pode contestar e como o recurso é revisado."),
        [nameof(EtapaProcesso.TiposDeDecisao)] = Entra("Os tipos de decisão dizem o que o preliminar decide e o que dele se pode contestar."),
        [nameof(EtapaProcesso.Produtos)] = NaoEntra(PublicacoesDeclaradas),
        [nameof(EtapaProcesso.ProduzResultado)] = NaoEntra("Derivada de Produtos."),
        [nameof(EtapaProcesso.ComponeNota)] = NaoEntra("Derivada do caráter e do peso, que entram."),
        [nameof(EtapaProcesso.DeclaraNotaDoEnem)] = NaoEntra("Derivada do tipo de etapa, cuja origem entra."),
    };

    [Fact(DisplayName = "Toda classificação diz o motivo")]
    public void Toda_ClassificacaoDizOMotivo() =>
        DaFase.Concat(DaEtapa).Where(static c => string.IsNullOrWhiteSpace(c.Value.Motivo)).Select(static c => c.Key)
            .Should().BeEmpty("a classificação sem motivo não explica a decisão a quem altera a entidade");

    /// <summary>As propriedades classificadas como entrada do preliminar, para os testes que provam a variação de cada uma.</summary>
    internal static IEnumerable<string> PropriedadesQueEntram(Dictionary<string, Classificacao> classificadas) =>
        classificadas.Where(static c => c.Value.EntraNaEntrada).Select(static c => c.Key).Order(StringComparer.Ordinal);

    private static Classificacao Entra(string motivo) => new(EntraNaEntrada: true, motivo);

    private static Classificacao NaoEntra(string motivo) => new(EntraNaEntrada: false, motivo);

    internal sealed record Classificacao(bool EntraNaEntrada, string Motivo);
}

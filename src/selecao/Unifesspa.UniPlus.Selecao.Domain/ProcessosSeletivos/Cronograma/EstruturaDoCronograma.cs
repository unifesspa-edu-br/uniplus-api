namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O cronograma como conjunto de fases: existência, ordem única, fase canônica única e a precedência que o
/// cadastro declara entre elas. Cada causa sai na posição da fase que a repete ou a viola.
/// </summary>
internal static class EstruturaDoCronograma
{
    internal const string CronogramaVazio = "ProcessoSeletivo.CronogramaFasesVazio";

    internal static IReadOnlyList<FieldError> Recusas(IReadOnlyList<FaseDeclarada> fases) =>
    [
        .. FasesAusentes(fases),
        .. OrdensRepetidas(fases),
        .. FasesCanonicasRepetidas(fases),
    ];

    /// <summary>
    /// A precedência de cada aresta do cadastro. A aresta com fase ausente não se aplica, e a que envolve código
    /// repetido fica suspensa: a repetição já é causa de estrutura, e não há como saber qual fase o código designa.
    /// </summary>
    internal static IReadOnlyList<FieldError> Precedencia(CronogramaDoProcesso cronograma, IReadOnlyList<ArestaPrecedencia> arestas) =>
        [.. arestas.Select(aresta => ViolacaoDaAresta(cronograma, aresta)).OfType<FieldError>()];

    private static IEnumerable<FieldError> FasesAusentes(IReadOnlyList<FaseDeclarada> fases) =>
        fases.Count == 0
            ? [new FieldError("fases", new DomainError(CronogramaVazio, "O processo deve ter ao menos uma fase no cronograma."))]
            : [];

    private static IEnumerable<FieldError> OrdensRepetidas(IReadOnlyList<FaseDeclarada> fases)
    {
        HashSet<int> vistas = [];
        for (int posicao = 0; posicao < fases.Count; posicao++)
        {
            if (!vistas.Add(fases[posicao].Ordem))
            {
                yield return CronogramaDoProcesso.NaFase(
                    posicao, "ordem", new DomainError("ProcessoSeletivo.OrdemFaseDuplicada", "Cada fase deve ter uma ordem única dentro do cronograma."));
            }
        }
    }

    // Origem e código repetidos são a mesma causa: o cadastro só garante código único entre as fases não excluídas, então
    // origens diferentes podem congelar o mesmo código.
    private static IEnumerable<FieldError> FasesCanonicasRepetidas(IReadOnlyList<FaseDeclarada> fases)
    {
        HashSet<Guid> origens = [];
        HashSet<string> codigos = new(StringComparer.Ordinal);
        for (int posicao = 0; posicao < fases.Count; posicao++)
        {
            bool origemRepetida = !origens.Add(fases[posicao].OrigemId);
            bool codigoRepetido = !codigos.Add(fases[posicao].Codigo);
            if (origemRepetida || codigoRepetido)
            {
                yield return CronogramaDoProcesso.NaFase(
                    posicao, "faseCanonicaId", new DomainError("ProcessoSeletivo.FaseCanonicaDuplicada", "A mesma fase canônica não pode aparecer duas vezes no cronograma."));
            }
        }
    }

    private static FieldError? ViolacaoDaAresta(CronogramaDoProcesso cronograma, ArestaPrecedencia aresta)
    {
        FaseDeclarada? antecessora = cronograma.FaseDeCodigo(aresta.AntecessoraCodigo);
        FaseDeclarada? sucessora = cronograma.FaseDeCodigo(aresta.SucessoraCodigo);
        if (antecessora is null || sucessora is null)
        {
            return null;
        }

        return OrdemViolada(cronograma, aresta, antecessora, sucessora) ?? SobreposicaoNaoPermitida(cronograma, aresta, antecessora, sucessora);
    }

    private static FieldError? OrdemViolada(
        CronogramaDoProcesso cronograma, ArestaPrecedencia aresta, FaseDeclarada antecessora, FaseDeclarada sucessora) =>
        antecessora.Ordem >= sucessora.Ordem
            ? CronogramaDoProcesso.NaFase(
                cronograma.PosicaoDe(sucessora),
                "ordem",
                new DomainError(
                    "ProcessoSeletivo.PrecedenciaFaseViolada",
                    $"A fase '{aresta.AntecessoraCodigo}' (ordem {antecessora.Ordem}) precede '{aresta.SucessoraCodigo}' (ordem {sucessora.Ordem}) — a ordem declarada viola a precedência do cadastro."))
            : null;

    // Janela invertida não tem extremos confiáveis: a sobreposição daquela fase não se avalia, e a recusa da janela já está na declaração.
    private static FieldError? SobreposicaoNaoPermitida(
        CronogramaDoProcesso cronograma, ArestaPrecedencia aresta, FaseDeclarada antecessora, FaseDeclarada sucessora) =>
        !aresta.PermiteSobreposicao
        && !antecessora.Janela.Invertida
        && !sucessora.Janela.Invertida
        && antecessora.Janela.Fim > sucessora.Janela.Inicio
            ? CronogramaDoProcesso.NaFase(
                cronograma.PosicaoDe(sucessora),
                "inicio",
                new DomainError(
                    "ProcessoSeletivo.SobreposicaoDeJanelasNaoPermitida",
                    $"A janela da fase '{aresta.SucessoraCodigo}' ({sucessora.Janela.Inicio:O}–{sucessora.Janela.Fim:O}) se sobrepõe à da fase '{aresta.AntecessoraCodigo}' ({antecessora.Janela.Inicio:O}–{antecessora.Janela.Fim:O}), e o cadastro não permite sobreposição entre elas."))
            : null;
}

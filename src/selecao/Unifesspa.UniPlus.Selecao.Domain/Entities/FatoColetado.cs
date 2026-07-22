namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Um fato do vocabulário que <b>este</b> processo seletivo coleta do candidato, com a sua
/// posição na ordem de coleta e a pré-condição que decide se o campo produtor é apresentado
/// (Story #926).
/// </summary>
/// <remarks>
/// <para>
/// A pré-condição é <b>aresta do grafo por processo, não propriedade do catálogo</b>: o mesmo
/// fato pode ser coletado sem gate nenhum em outro processo. Por isso vive aqui, na configuração
/// do certame, e não em <c>FatoCandidato</c> — que é catálogo global e seed-governado (ADR-0111).
/// </para>
/// <para>
/// Fato <b>sem</b> pré-condição é coletado sempre — é o caso das autodeclarações de abertura, que
/// não dependem de nada anterior. Fato com pré-condição só é apresentado quando o predicado
/// resolve verdadeiro; quando resolve falso, o fato vinculado passa a não-aplicável, e quando
/// fica indeterminado, o fato o acompanha.
/// </para>
/// </remarks>
public sealed class FatoColetado : EntityBase
{
    private readonly List<CondicaoPrecondicaoFato> _precondicoes = [];

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Código do fato no vocabulário fechado do candidato.</summary>
    public string FatoCodigo { get; private set; } = string.Empty;

    /// <summary>
    /// Posição na ordem de coleta. Estritamente crescente e única no processo: é ela que dá
    /// sentido a "fato anterior", e todo fato citado numa pré-condição precisa ter ordem menor
    /// que a do fato que o cita.
    /// </summary>
    public int Ordem { get; private set; }

    public IReadOnlyCollection<CondicaoPrecondicaoFato> Precondicoes => _precondicoes.AsReadOnly();

    private FatoColetado() { }

    public static Result<FatoColetado> Criar(
        string fatoCodigo,
        int ordem,
        IReadOnlyList<CondicaoPrecondicaoFato>? precondicoes)
    {
        if (string.IsNullOrWhiteSpace(fatoCodigo))
        {
            return Result<FatoColetado>.Failure(new DomainError(
                FatoColetadoErrorCodes.FatoCodigoObrigatorio,
                "O código do fato coletado é obrigatório."));
        }

        if (ordem < 0)
        {
            return Result<FatoColetado>.Failure(new DomainError(
                FatoColetadoErrorCodes.OrdemInvalida,
                "A ordem de coleta não pode ser negativa."));
        }

        string codigo = fatoCodigo.Trim();
        IReadOnlyList<CondicaoPrecondicaoFato> condicoes = precondicoes ?? [];

        // Auto-referência é ciclo de comprimento um. Barrada aqui, na criação, porque não depende
        // de conhecer os irmãos — e um erro específico diz mais do que o genérico de ciclo, que
        // reportaria um caminho de um nó só. Toda a validação acontece antes de qualquer mutação:
        // uma condição só é vinculada depois de o fato inteiro ser aceito, para nunca deixar uma
        // instância recebida do chamador apontando para um fato que a factory acabou de recusar.
        foreach (CondicaoPrecondicaoFato precondicao in condicoes)
        {
            if (string.Equals(precondicao.Fato, codigo, StringComparison.Ordinal))
            {
                return Result<FatoColetado>.Failure(new DomainError(
                    FatoColetadoErrorCodes.PrecondicaoAutorreferente,
                    $"A pré-condição do fato '{codigo}' cita o próprio fato."));
            }
        }

        FatoColetado fato = new() { FatoCodigo = codigo, Ordem = ordem };
        foreach (CondicaoPrecondicaoFato precondicao in condicoes)
        {
            precondicao.VincularFatoColetado(fato.Id);
            fato._precondicoes.Add(precondicao);
        }

        return Result<FatoColetado>.Success(fato);
    }

    /// <summary>Indica se o fato é coletado incondicionalmente.</summary>
    public bool SemPrecondicao => _precondicoes.Count == 0;

    /// <summary>Códigos dos fatos citados na pré-condição, sem repetição.</summary>
    public IReadOnlyCollection<string> FatosCitados =>
        [.. _precondicoes.Select(static c => c.Fato).Distinct(StringComparer.Ordinal)];

    internal void VincularProcessoSeletivo(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// O predicado de pré-condição na forma avaliável, ou <see langword="null"/> quando o fato é
    /// coletado incondicionalmente. Um predicado sem cláusula nenhuma avaliaria falso — que é o
    /// oposto de "sem pré-condição" —, então a ausência é representada pelo nulo, nunca por um
    /// predicado vazio.
    /// </summary>
    internal ValueObjects.PredicadoDnf? ParaPredicado() =>
        _precondicoes.Count == 0
            ? null
            : ValueObjects.PredicadoDnf.CriarDeCondicoesAgrupadas(
                [.. _precondicoes.Select(static c => (c.Clausula, c.ParaCondicaoDnf()))]).Value!;
}

/// <summary>Códigos de erro de <see cref="FatoColetado"/>.</summary>
public static class FatoColetadoErrorCodes
{
    public const string FatoCodigoObrigatorio = "FatoColetado.FatoCodigoObrigatorio";
    public const string OrdemInvalida = "FatoColetado.OrdemInvalida";
    public const string PrecondicaoAutorreferente = "FatoColetado.PrecondicaoAutorreferente";
    public const string FatoDuplicado = "FatoColetado.FatoDuplicado";
    public const string OrdemDuplicada = "FatoColetado.OrdemDuplicada";
    public const string PrecondicaoCitaFatoNaoColetado = "FatoColetado.PrecondicaoCitaFatoNaoColetado";
    public const string PrecondicaoCitaFatoPosterior = "FatoColetado.PrecondicaoCitaFatoPosterior";
    public const string GrafoComCiclo = "FatoColetado.GrafoComCiclo";
}

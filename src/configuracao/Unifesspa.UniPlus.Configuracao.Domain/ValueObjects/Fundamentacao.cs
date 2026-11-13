namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Texto livre que fundamenta um ato da proteção do catálogo: a justificativa da decisão ou o texto da manifestação
/// técnica. Sem os espaços das pontas, com até <see cref="MaxLength"/> caracteres; em branco conta como ausente.
/// </summary>
public sealed record Fundamentacao
{
    /// <summary>Tamanho máximo do texto, em caracteres, também o das colunas que o guardam.</summary>
    public const int MaxLength = 2000;

    private Fundamentacao(string valor) => Valor = valor;

    /// <summary>O texto aparado.</summary>
    public string Valor { get; }

    /// <summary>
    /// A fundamentação informada, ou <see langword="null"/> quando ausente ou inválida. O texto obrigatório ausente e o
    /// longo demais são recusados no campo de <paramref name="recusas"/>, junto das demais recusas do lote (ADR-0125).
    /// </summary>
    public static Fundamentacao? Ler(string? texto, bool obrigatoria, RecusasDaFundamentacao recusas, List<FieldError> erros)
    {
        ArgumentNullException.ThrowIfNull(recusas);
        ArgumentNullException.ThrowIfNull(erros);

        string? aparado = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        DomainError? recusa = Recusa(aparado, obrigatoria, recusas);
        if (recusa is not null)
        {
            erros.Add(new FieldError(recusas.Campo, recusa));
            return null;
        }

        return aparado is null ? null : new Fundamentacao(aparado);
    }

    /// <summary>Reidrata o texto já conferido na gravação (value converter do EF).</summary>
    /// <exception cref="ArgumentException">Se o texto está em branco, tem espaço nas pontas ou passa do limite.</exception>
    public static Fundamentacao Analisar(string valor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor);

        return valor.Length <= MaxLength && valor.Trim().Length == valor.Length
            ? new Fundamentacao(valor)
            : throw new ArgumentException("Fundamentação fora da forma gravada.", nameof(valor));
    }

    private static DomainError? Recusa(string? aparado, bool obrigatoria, RecusasDaFundamentacao recusas) => aparado switch
    {
        null when obrigatoria => new(recusas.Obrigatoria, recusas.MensagemObrigatoria),
        { Length: > MaxLength } => new(recusas.MuitoLonga, $"{recusas.Rotulo} tem no máximo {MaxLength} caracteres."),
        _ => null,
    };
}

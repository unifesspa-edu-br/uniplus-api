namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Results;

/// <summary>
/// O texto que alguém digitou para pesquisar uma listagem, já na forma de comparação
/// (<see cref="FormaParaComparacao"/>). Texto em branco não é busca: <see cref="Criar"/> devolve
/// sucesso com <see langword="null"/>.
/// </summary>
/// <remarks>
/// O tipo guarda só a forma normalizada, sem o escape dos curingas do <c>LIKE</c>, que é assunto
/// do banco. O limite de comprimento desconsidera espaços em branco nas pontas, antes da normalização.
/// </remarks>
public sealed record TermoDeBusca
{
    /// <summary>Comprimento máximo do texto pesquisado, o do maior campo pesquisável: um nome.</summary>
    public const int ComprimentoMaximo = 200;

    /// <summary>Código da recusa do texto que passa do <see cref="ComprimentoMaximo"/>.</summary>
    public const string CodigoDeBuscaMuitoLonga = "Consulta.BuscaMuitoLonga";

    private TermoDeBusca(string normalizado) => Normalizado = normalizado;

    /// <summary>O termo sem acento, em minúsculas, aparado e sem caractere nulo.</summary>
    public string Normalizado { get; }

    /// <summary>
    /// Confere o comprimento do texto aparado e só então o normaliza.
    /// </summary>
    public static Result<TermoDeBusca?> Criar(string? pedido)
    {
        if (ConferirComprimento(pedido) is { } recusa)
        {
            return Result<TermoDeBusca?>.Failure(recusa);
        }

        string? normalizado = FormaParaComparacao.Termo(pedido);

        return Result<TermoDeBusca?>.Success(normalizado is null ? null : new TermoDeBusca(normalizado));
    }

    /// <summary>A recusa do texto longo demais, ou <see langword="null"/> quando ele cabe.</summary>
    public static DomainError? ConferirComprimento(string? pedido) =>
        pedido.AsSpan().Trim().Length > ComprimentoMaximo
            ? new DomainError(CodigoDeBuscaMuitoLonga, $"O texto pesquisado excede {ComprimentoMaximo} caracteres, sem contar os espaços das pontas.")
            : null;

    /// <summary>Não imprime o termo: ele pode ser o nome de uma pessoa e não vai a log.</summary>
    public override string ToString() => nameof(TermoDeBusca);
}

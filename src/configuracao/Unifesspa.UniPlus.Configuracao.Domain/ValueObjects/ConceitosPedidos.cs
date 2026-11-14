namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Text;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os códigos de tipo de conceito pedidos para um tipo de etapa, a conferir contra o catálogo: a lista é
/// obrigatória, ainda que vazia, e cada código precisa existir, estar ativo e aparecer uma só vez. As recusas
/// são uma por espécie e não repetem o código informado.
/// </summary>
public sealed class ConceitosPedidos
{
    public const string Campo = "conceitosAdmitidos";

    private static readonly (string Codigo, string Mensagem)[] Recusas =
    [
        (TipoEtapaErrorCodes.ConceitoInexistente, "Há conceito admitido que não existe no catálogo de tipos de conceito."),
        (TipoEtapaErrorCodes.ConceitoInativo, "Há conceito admitido que está desativado no catálogo de tipos de conceito."),
        (TipoEtapaErrorCodes.ConceitoDuplicado, "Há conceito admitido informado mais de uma vez."),
    ];

    private readonly string[]? _codigos;

    private ConceitosPedidos(string[]? codigos) => _codigos = codigos;

    /// <summary>Os códigos como o catálogo os guarda: sem espaços nas pontas e em NFC.</summary>
    public static ConceitosPedidos De(IEnumerable<string?>? codigos) =>
        new(codigos?.Select(Normalizar).ToArray());

    /// <summary>
    /// O que consultar no catálogo: os códigos distintos e não vazios, na ordem informada. O código com o
    /// caractere nulo não vai à consulta, porque o banco não o aceita em texto e o catálogo nunca o guarda: ele
    /// segue como conceito inexistente.
    /// </summary>
    public IReadOnlyCollection<string> CodigosParaConsulta =>
        _codigos is null ? [] : [.. _codigos.Where(static codigo => codigo.Length > 0 && !codigo.Contains('\0', StringComparison.Ordinal)).Distinct(StringComparer.Ordinal)];

    /// <summary>Os tipos de conceito do catálogo que correspondem aos códigos, ou as recusas acumuladas.</summary>
    public Result<IReadOnlyList<TipoConceito>> Resolver(IEnumerable<TipoConceito> catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        return _codigos is null
            ? Falha(
                TipoEtapaErrorCodes.ConceitosAdmitidosObrigatorio,
                "É obrigatório informar os conceitos admitidos pelo tipo de etapa, ainda que a lista seja vazia.")
            : Conferir(_codigos, catalogo.ToDictionary(static tipo => tipo.Codigo, StringComparer.Ordinal));
    }

    private static Result<IReadOnlyList<TipoConceito>> Conferir(string[] codigos, Dictionary<string, TipoConceito> porCodigo)
    {
        HashSet<string> vistos = new(StringComparer.Ordinal);
        (string Codigo, string? Recusa)[] conferidos = [.. codigos.Select(codigo => (codigo, RecusaDe(codigo, vistos, porCodigo)))];
        FieldError[] erros = [.. Recusas
            .Where(recusa => conferidos.Any(conferido => conferido.Recusa == recusa.Codigo))
            .Select(static recusa => new FieldError(Campo, new DomainError(recusa.Codigo, recusa.Mensagem)))];
        return erros.Length > 0
            ? Result<IReadOnlyList<TipoConceito>>.ValidationFailure(erros)
            : Result<IReadOnlyList<TipoConceito>>.Success([.. conferidos.Select(conferido => porCodigo[conferido.Codigo])]);
    }

    // A ordem dos testes é a das recusas: o que se repete já foi visto, então não é conferido de novo no catálogo.
    private static string? RecusaDe(string codigo, HashSet<string> vistos, Dictionary<string, TipoConceito> porCodigo) =>
        !vistos.Add(codigo) ? TipoEtapaErrorCodes.ConceitoDuplicado
        : !porCodigo.TryGetValue(codigo, out TipoConceito? tipo) ? TipoEtapaErrorCodes.ConceitoInexistente
        : tipo.Ativo ? null
        : TipoEtapaErrorCodes.ConceitoInativo;

    private static Result<IReadOnlyList<TipoConceito>> Falha(string codigo, string mensagem) =>
        Result<IReadOnlyList<TipoConceito>>.ValidationFailure([new(Campo, new DomainError(codigo, mensagem))]);

    private static string Normalizar(string? codigo) =>
        codigo is null ? string.Empty : codigo.Trim().Normalize(NormalizationForm.FormC);
}

namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que a leitura de uma parte da exigência montou: o valor, nulo quando a parte é ausente ou não pôde ser montada, e as recusas
/// que ela acumulou. A parte cortada depende de outra que a leitura recusou: fica sem valor e sem recusa, porque a causa é da outra.
/// </summary>
internal sealed record ParteLida<T>(T? Valor, IReadOnlyList<FieldError> Recusas, bool Cortada = false)
    where T : class
{
    internal static ParteLida<T> Ausente { get; } = new(null, []);

    internal static ParteLida<T> CortadaSemCausa { get; } = new(null, [], true);

    internal static ParteLida<T> Montada(T valor) => new(valor, []);

    internal static ParteLida<T> Recusada(IEnumerable<FieldError> recusas) => new(null, [.. recusas]);

    /// <summary>O resultado da fábrica de domínio, com as recusas no campo do documento que a parte ocupa.</summary>
    internal static ParteLida<T> Do(Result<T> resultado, string campo) =>
        resultado.IsSuccess ? Montada(resultado.Value!) : Recusada(resultado.Errors.Select(recusa => recusa.NoCaminhoDoItem(campo)));
}

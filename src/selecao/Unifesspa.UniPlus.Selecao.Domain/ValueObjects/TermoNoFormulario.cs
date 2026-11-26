namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O termo exigido por um formulário: o código do termo só é único dentro do formulário que o exige, então
/// o aceite e a versão admitida se identificam pelo par.
/// </summary>
/// <param name="FormularioCodigo">O código do formulário que exige o termo.</param>
/// <param name="Codigo">O código do termo dentro do formulário.</param>
public sealed record TermoNoFormulario(string FormularioCodigo, string Codigo);

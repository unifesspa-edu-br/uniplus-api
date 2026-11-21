namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Um prazo do recurso já conferido: o de interposição ou o da 2ª instância. A mesma conferência vale para os dois
/// (<see cref="PrazoDeclarado"/>), porque a contagem é a mesma: dias úteis inteiros ou horas.
/// </summary>
/// <param name="Valor">Magnitude do prazo, estritamente positiva. Em dias úteis, é um número inteiro.</param>
/// <param name="Unidade">Unidade declarável do prazo: <see cref="UnidadePrazo.DiasUteis"/> ou <see cref="UnidadePrazo.Horas"/>.</param>
public sealed record PrazoDoRecurso(decimal Valor, UnidadePrazo Unidade);

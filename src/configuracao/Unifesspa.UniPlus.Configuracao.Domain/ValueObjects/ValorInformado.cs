namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>Um valor categórico como chega na gravação do tipo de conceito, ainda não validado.</summary>
/// <param name="Codigo">Código informado, ou <see langword="null"/> quando ausente.</param>
/// <param name="Rotulo">Rótulo informado, ou <see langword="null"/> quando ausente.</param>
/// <param name="ContaComoAprovacao">Se escolher este valor conta como aprovação, ou <see langword="null"/> quando ausente.</param>
public sealed record ValorInformado(string? Codigo, string? Rotulo, bool? ContaComoAprovacao);

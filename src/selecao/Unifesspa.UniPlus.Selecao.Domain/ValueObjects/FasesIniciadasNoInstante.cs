namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>As fases iniciadas calculadas num instante, junto do instante que serviu de referência.</summary>
public sealed record FasesIniciadasNoInstante(FasesIniciadas Fases, DateTimeOffset Instante);

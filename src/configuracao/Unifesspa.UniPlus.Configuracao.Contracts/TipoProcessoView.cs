namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>Vista ativa dos tipos de processo para consumo cross-módulo (ADR-0056).</summary>
public sealed record TipoProcessoView(Guid Id, string Codigo, string Nome, string? Descricao);

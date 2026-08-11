namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>Vista ativa dos tipos de etapa para consumo cross-módulo (ADR-0056).</summary>
public sealed record TipoEtapaView(Guid Id, string Codigo, string Nome, string? Descricao);

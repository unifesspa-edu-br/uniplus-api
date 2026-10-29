namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Identifica o pedido ou a instalação que gerou a concessão.</summary>
public sealed record OrigemDaConcessao(TipoDeOrigem Tipo, Guid Id);

namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O que se grava da classe de uma proposta na criação: o alcance, que não muda, e a etapa extra que o acréscimo exige.</summary>
public sealed record ClasseDaVersao(TipoDeEscopo Alcance, bool ExigeProtecaoDeDados);

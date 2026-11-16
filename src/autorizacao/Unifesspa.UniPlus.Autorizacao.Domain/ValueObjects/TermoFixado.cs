namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>A versão do termo fixada no pedido, com o número e o resumo copiados dela, imutável depois da criação.</summary>
public sealed record TermoFixado(Guid VersaoId, int Numero, string Resumo);

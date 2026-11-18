namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>O aceite de um termo, com a versão e o hash que a inscrição carrega da versão da inscrição.</summary>
public sealed record AceiteDeTermo(string Codigo, Guid VersaoId, string HashVersao);

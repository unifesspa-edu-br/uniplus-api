namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um termo que o formulário de inscrição da versão fixada exige, com a versão e o hash em que o
/// candidato o aceita. O aceite que a inscrição grava tem de ser exatamente este.
/// </summary>
public sealed record TermoAdmitido(Guid VersaoId, string HashVersao);

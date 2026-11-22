namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>Respostas de um registro de grupo do formulário usado na comprovação.</summary>
/// <param name="OcorrenciaId">Identificador estável do registro.</param>
/// <param name="Atributos">Fatos resolvidos do registro.</param>
public sealed record OcorrenciaDoGrupo(string OcorrenciaId, IReadOnlyDictionary<string, FatoResolvido> Atributos);

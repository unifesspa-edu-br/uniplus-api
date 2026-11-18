namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;

/// <summary>Uma ocorrência de grupo repetível da inscrição, pelo id estável do membro.</summary>
public sealed record OcorrenciaDeGrupoInscricao(string Id, IReadOnlyDictionary<string, JsonElement> Respostas);

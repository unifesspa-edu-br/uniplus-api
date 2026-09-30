namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

/// <summary>Corpo da edição de um fato: o nome e a descrição, os únicos campos editáveis.</summary>
public sealed record DescritivoDoFatoInput(string Nome, string? Descricao);

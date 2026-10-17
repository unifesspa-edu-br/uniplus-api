namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

/// <summary>Corpo da reordenação: todos os códigos do fato, ativos e desativados, na ordem desejada.</summary>
public sealed record OrdemDosValoresInput(IReadOnlyList<string> Codigos);

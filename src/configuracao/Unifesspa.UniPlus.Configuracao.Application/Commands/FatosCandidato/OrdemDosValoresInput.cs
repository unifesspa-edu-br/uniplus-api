namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using System.ComponentModel;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Corpo da reordenação: todos os códigos do fato, ativos e desativados, na ordem desejada. A lista
/// ausente ou nula chega ao domínio e é recusada com 422.
/// </summary>
public sealed record OrdemDosValoresInput(
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422); envie [] quando não houver nenhum.")]
    IReadOnlyList<string>? Codigos);

namespace Unifesspa.UniPlus.Selecao.Application.Queries.CampoDaInscricao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Pede o valor completo de UM campo identificador de uma inscrição enviada do processo, por quem opera o processo. O
/// campo em grupo é pedido com o identificador da ocorrência.
/// </summary>
/// <param name="ProcessoSeletivoId">O processo da rota.</param>
/// <param name="InscricaoId">A inscrição.</param>
/// <param name="FatoCodigo">O código do fato, que não é dado pessoal.</param>
/// <param name="Ocorrencia">O identificador da ocorrência do grupo, quando o campo está num grupo.</param>
public sealed record RevelarCampoDaInscricaoQuery(Guid ProcessoSeletivoId, Guid InscricaoId, string FatoCodigo, string? Ocorrencia)
    : IQuery<Result<CampoReveladoDto>>;

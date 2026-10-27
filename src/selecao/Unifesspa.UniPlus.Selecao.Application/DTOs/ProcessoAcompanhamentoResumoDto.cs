namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Domain.Enums;

/// <summary>Identificação mínima de um processo para seleção no acompanhamento.</summary>
public sealed record ProcessoAcompanhamentoResumoDto(Guid Id, string Nome, StatusProcesso Status);

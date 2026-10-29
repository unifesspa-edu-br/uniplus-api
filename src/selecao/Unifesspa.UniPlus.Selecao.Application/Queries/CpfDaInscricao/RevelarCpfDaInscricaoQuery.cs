namespace Unifesspa.UniPlus.Selecao.Application.Queries.CpfDaInscricao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Pede o CPF completo de uma inscrição enviada do processo, por quem opera o processo.</summary>
public sealed record RevelarCpfDaInscricaoQuery(Guid ProcessoSeletivoId, Guid InscricaoId) : IQuery<Result<CpfReveladoDto>>;

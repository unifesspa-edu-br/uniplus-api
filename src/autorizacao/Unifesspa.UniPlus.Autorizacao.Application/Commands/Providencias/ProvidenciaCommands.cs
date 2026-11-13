namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Providencias;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Solicita providência sobre uma concessão (UNI-REQ-0375). O tipo e o fundamento chegam como o corpo os traz, e o
/// agregado os confere com as demais recusas, num único lote (ADR-0125).
/// </summary>
public sealed record SolicitarProvidenciaCommand(Guid ConcessaoId, SolicitacaoDaProvidencia Solicitacao) : ICommand<Result<ProvidenciaDto>>;

/// <summary>Responde a providência com motivo, sem revogar a concessão (UNI-REQ-0376).</summary>
public sealed record ResponderProvidenciaCommand(Guid ProvidenciaId, string? Motivo) : ICommand<Result<ProvidenciaDto>>;

/// <summary>Atende a providência pela revogação vinculada da concessão, que segue a revogação comum (UNI-REQ-0376).</summary>
public sealed record AtenderProvidenciaCommand(Guid ProvidenciaId, DeclaracaoDoAtendimento Declaracao) : ICommand<Result<ProvidenciaDto>>;

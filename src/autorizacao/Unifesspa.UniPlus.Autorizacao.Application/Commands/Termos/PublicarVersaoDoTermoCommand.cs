namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Termos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Publica uma versão nova do termo de responsabilidade, que passa a ser a vigente (UNI-REQ-0377).</summary>
/// <param name="VersaoVigenteEsperadaId">A vigente que o publicador viu; nula se não havia nenhuma.</param>
public sealed record PublicarVersaoDoTermoCommand(string? Texto, string? Ato, Guid? VersaoVigenteEsperadaId)
    : ICommand<Result<VersaoDoTermoDto>>;

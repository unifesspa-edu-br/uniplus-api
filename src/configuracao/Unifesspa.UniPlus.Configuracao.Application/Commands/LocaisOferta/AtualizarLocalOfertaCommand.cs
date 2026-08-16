namespace Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <remarks>
/// <c>CidadeCodigoIbge</c>, <c>CidadeNome</c> e <c>CidadeUf</c> são
/// <c>string?</c>, não <c>string</c> (ADR-0125) — mesma justificativa de
/// <see cref="CriarLocalOfertaCommand"/>.
/// </remarks>
public sealed record AtualizarLocalOfertaCommand(
    Guid Id,
    TipoLocalOferta Tipo,
    Guid? CampusResponsavelId,
    string? CidadeCodigoIbge,
    string? CidadeNome,
    string? CidadeUf,
    EnderecoGeoInput? Endereco,
    string? CodigoEmec) : ICommand<Result>;

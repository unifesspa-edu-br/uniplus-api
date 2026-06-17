namespace Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record AtualizarLocalOfertaCommand(
    Guid Id,
    TipoLocalOferta Tipo,
    Guid? CampusResponsavelId,
    string CidadeCodigoIbge,
    string CidadeNome,
    string CidadeUf,
    string? Endereco,
    string? CodigoEmec) : ICommand<Result>;

namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record AtualizarCampusCommand(
    Guid Id,
    string Sigla,
    string Nome,
    string CidadeCodigoIbge,
    string CidadeNome,
    string CidadeUf,
    string? Endereco,
    string? Cep,
    decimal? Latitude,
    decimal? Longitude,
    string? CodigoEmec) : ICommand<Result>;

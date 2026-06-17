namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um Campus. A proveniência do display cache (<c>cidade_origem</c>) e o
/// instante (<c>cidade_display_atualizado_em</c>) são carimbados pelo handler
/// (server-side, ADR-0090) — não viajam no payload.
/// </summary>
public sealed record CriarCampusCommand(
    string Sigla,
    string Nome,
    string CidadeCodigoIbge,
    string CidadeNome,
    string CidadeUf,
    string? Endereco,
    string? Cep,
    decimal? Latitude,
    decimal? Longitude,
    string? CodigoEmec) : ICommand<Result<Guid>>;

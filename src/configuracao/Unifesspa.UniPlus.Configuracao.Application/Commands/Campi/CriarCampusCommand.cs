namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um Campus. A proveniência do display cache (<c>cidade_origem</c>) e o
/// instante (<c>cidade_display_atualizado_em</c>) são carimbados pelo handler
/// (server-side, ADR-0090) — não viajam no payload. O <see cref="Endereco"/> é o
/// endereço estruturado opcional ao Geo via CEP (ADR-0096).
/// </summary>
public sealed record CriarCampusCommand(
    string Sigla,
    string Nome,
    string CidadeCodigoIbge,
    string CidadeNome,
    string CidadeUf,
    EnderecoGeoInput? Endereco,
    string? CodigoEmec) : ICommand<Result<Guid>>;

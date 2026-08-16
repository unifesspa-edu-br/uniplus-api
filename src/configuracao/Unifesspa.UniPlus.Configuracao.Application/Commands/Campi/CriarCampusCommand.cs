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
/// <remarks>
/// Campos obrigatórios são <c>string?</c>, não <c>string</c>, de propósito
/// (ADR-0125): se fossem não-anuláveis, o model binding automático do
/// <c>[ApiController]</c> rejeitaria JSON com o campo ausente/nulo com um 400
/// genérico do ASP.NET (fora do formato RFC 9457 do resto da API) antes de o
/// Wolverine e o domínio nunca chegarem a rodar — o mesmo problema estrutural
/// que motivou esta ADR, só que na camada de binding em vez da de validação.
/// </remarks>
public sealed record CriarCampusCommand(
    string? Sigla,
    string? Nome,
    string? CidadeCodigoIbge,
    string? CidadeNome,
    string? CidadeUf,
    EnderecoGeoInput? Endereco,
    string? CodigoEmec) : ICommand<Result<Guid>>;

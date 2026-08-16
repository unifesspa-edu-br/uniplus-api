namespace Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um Local de Oferta (modelo flat, ADR-0065). A proveniência do display
/// cache (<c>cidade_origem</c>) e o instante são carimbados pelo handler. O
/// <see cref="Endereco"/> é o endereço estruturado opcional ao Geo via CEP (ADR-0096).
/// </summary>
/// <remarks>
/// <c>CidadeCodigoIbge</c>, <c>CidadeNome</c> e <c>CidadeUf</c> são
/// <c>string?</c>, não <c>string</c> (ADR-0125): sem validator FluentValidation
/// garantindo não-nulo a montante, o model binding automático do
/// <c>[ApiController]</c> interceptaria um campo ausente/nulo com um 400
/// genérico do ASP.NET, antes de o domínio rodar. <c>Tipo</c> não precisa do
/// mesmo tratamento: é um <see langword="enum"/> (tipo valor), e um campo
/// ausente no JSON já desserializa para o sentinela
/// <see cref="TipoLocalOferta.Nenhum"/> (0), que o domínio já recusa.
/// </remarks>
public sealed record CriarLocalOfertaCommand(
    TipoLocalOferta Tipo,
    Guid? CampusResponsavelId,
    string? CidadeCodigoIbge,
    string? CidadeNome,
    string? CidadeUf,
    EnderecoGeoInput? Endereco,
    string? CodigoEmec) : ICommand<Result<Guid>>;

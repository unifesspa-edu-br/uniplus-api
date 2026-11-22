namespace Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um Local de Oferta (modelo flat, ADR-0065). A proveniência do display
/// cache (<c>cidade_origem</c>) e o instante são definidos pelo local ao ser criado. O
/// <see cref="Endereco"/> é o endereço estruturado opcional ao Geo via CEP (ADR-0096).
/// </summary>
/// <remarks>
/// <c>CidadeCodigoIbge</c>, <c>CidadeNome</c> e <c>CidadeUf</c> são
/// <c>string?</c>, não <c>string</c> (ADR-0125): sem validator FluentValidation
/// garantindo não-nulo a montante, o model binding automático do
/// <c>[ApiController]</c> interceptaria um campo ausente/nulo com um 400
/// genérico do ASP.NET, antes de o domínio rodar. <c>Tipo</c> chega como código
/// de contrato (<see cref="TipoLocalOferta"/>): o domínio recusa o ausente e o
/// desconhecido, com os demais erros.
/// </remarks>
public sealed record CriarLocalOfertaCommand(
    [property: AceitaCodigosDe<TipoLocalOferta>] string? Tipo,
    Guid? CampusResponsavelId,
    string? CidadeCodigoIbge,
    string? CidadeNome,
    string? CidadeUf,
    EnderecoGeoInput? Endereco,
    string? CodigoEmec) : ICommand<Result<Guid>>
{
    public DeclaracaoDoLocalDeOferta Declaracao(ReferenciaEnderecoGeo? endereco) =>
        new(Tipo, CampusResponsavelId, CidadeCodigoIbge, CidadeNome, CidadeUf, endereco, CodigoEmec);
}

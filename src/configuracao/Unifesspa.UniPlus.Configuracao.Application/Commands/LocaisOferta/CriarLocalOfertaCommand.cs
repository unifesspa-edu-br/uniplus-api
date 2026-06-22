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
public sealed record CriarLocalOfertaCommand(
    TipoLocalOferta Tipo,
    Guid? CampusResponsavelId,
    string CidadeCodigoIbge,
    string CidadeNome,
    string CidadeUf,
    EnderecoGeoInput? Endereco,
    string? CodigoEmec) : ICommand<Result<Guid>>;

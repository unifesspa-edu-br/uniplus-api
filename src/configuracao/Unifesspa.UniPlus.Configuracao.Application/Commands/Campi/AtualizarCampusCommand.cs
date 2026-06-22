namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record AtualizarCampusCommand(
    Guid Id,
    string Sigla,
    string Nome,
    string CidadeCodigoIbge,
    string CidadeNome,
    string CidadeUf,
    EnderecoGeoInput? Endereco,
    string? CodigoEmec) : ICommand<Result>;

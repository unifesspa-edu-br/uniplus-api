namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

/// <remarks>
/// Campos obrigatórios são <c>string?</c>, não <c>string</c> — ver justificativa em
/// <see cref="CriarCampusCommand"/> (ADR-0125).
/// </remarks>
public sealed record AtualizarCampusCommand(
    Guid Id,
    string? Sigla,
    string? Nome,
    string? CidadeCodigoIbge,
    string? CidadeNome,
    string? CidadeUf,
    EnderecoGeoInput? Endereco,
    string? CodigoEmec) : ICommand<Result>;

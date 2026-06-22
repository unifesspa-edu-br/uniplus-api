namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Enderecos;

/// <summary>
/// Edita os dados regulatórios e o vínculo com a reitoria da Instituição
/// existente. A referência de cidade da sede é opcional (all-or-nothing): o trio
/// <c>CidadeCodigoIbge</c>/<c>CidadeNome</c>/<c>CidadeUf</c> viaja no payload; a
/// proveniência/frescura do display cache é recarimbada server-side pelo handler
/// apenas quando o trio muda (ADR-0090).
/// </summary>
public sealed record AtualizarInstituicaoCommand(
    Guid Id,
    string CodigoEmec,
    string Nome,
    string Sigla,
    string OrganizacaoAcademica,
    string CategoriaAdministrativa,
    string? Cnpj,
    string? Mantenedora,
    string? CodigoMantenedoraEmec,
    string? Situacao,
    string? AtoCredenciamento,
    string? AtoRecredenciamento,
    string? ConceitoInstitucional,
    string? Igc,
    string? Website,
    EnderecoGeoInput? Endereco,
    string? CidadeCodigoIbge,
    string? CidadeNome,
    string? CidadeUf,
    Guid? UnidadeRaizId) : ICommand<Result>;

namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Edita os dados regulatórios e o vínculo com a reitoria da Instituição
/// existente.
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
    string? EnderecoSede,
    string? MunicipioSede,
    Guid? UnidadeRaizId) : ICommand<Result>;

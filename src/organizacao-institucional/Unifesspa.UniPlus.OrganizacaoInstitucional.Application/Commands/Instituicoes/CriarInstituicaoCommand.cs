namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria a Instituição singleton. Rejeitado se já existe uma Instituição viva
/// (ADR-0055 · CA-02).
/// </summary>
public sealed record CriarInstituicaoCommand(
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
    Guid? UnidadeRaizId) : ICommand<Result<Guid>>;

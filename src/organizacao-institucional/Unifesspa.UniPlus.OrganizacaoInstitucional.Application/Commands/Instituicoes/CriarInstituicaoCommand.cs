namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Enderecos;

/// <summary>
/// Cria a Instituição singleton. Rejeitado se já existe uma Instituição viva
/// (ADR-0055 · CA-02). A referência de cidade da sede é opcional (all-or-nothing)
/// e segue o padrão Geo (ADR-0090): o trio <c>CidadeCodigoIbge</c>/<c>CidadeNome</c>/
/// <c>CidadeUf</c> viaja no payload (composição no cliente); a proveniência
/// (<c>cidade_origem</c>) e o instante (<c>cidade_display_atualizado_em</c>) são
/// carimbados server-side pelo handler.
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
    EnderecoGeoInput? Endereco,
    string? CidadeCodigoIbge,
    string? CidadeNome,
    string? CidadeUf,
    Guid? UnidadeRaizId) : ICommand<Result<Guid>>;

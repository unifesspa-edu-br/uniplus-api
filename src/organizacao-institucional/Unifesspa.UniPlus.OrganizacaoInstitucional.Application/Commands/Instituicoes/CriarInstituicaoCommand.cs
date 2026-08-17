namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Enderecos;

/// <summary>
/// Cria a Instituição singleton. Rejeitado se já existe uma Instituição viva
/// (ADR-0055). A referência de cidade da sede é opcional (all-or-nothing)
/// e segue o padrão Geo (ADR-0090): o trio <c>CidadeCodigoIbge</c>/<c>CidadeNome</c>/
/// <c>CidadeUf</c> viaja no payload (composição no cliente); a proveniência
/// (<c>cidade_origem</c>) e o instante (<c>cidade_display_atualizado_em</c>) são
/// carimbados server-side pelo handler.
/// </summary>
/// <remarks>
/// Os cinco campos obrigatórios são <c>string?</c>, não <c>string</c> (ADR-0125):
/// sem valor default, para o schema OpenAPI continuar listando-os como
/// obrigatórios; nulos, para o campo ausente escapar do <c>[ApiController]</c> e
/// chegar à validação de domínio, que acumula toda violação no mesmo lote.
/// </remarks>
public sealed record CriarInstituicaoCommand(
    string? CodigoEmec,
    string? Nome,
    string? Sigla,
    string? OrganizacaoAcademica,
    string? CategoriaAdministrativa,
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

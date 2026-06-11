namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

/// <summary>
/// Confere a integridade do vínculo <c>unidade_raiz_id</c> da Instituição:
/// quando informado, deve apontar para uma Unidade viva do tipo reitoria
/// (UNI-REQ-0007 · CA-04). Compartilhado entre os handlers de criação e
/// atualização.
/// </summary>
internal static class InstituicaoUnidadeRaizGuard
{
    /// <summary>
    /// Retorna <see langword="null"/> quando o vínculo é válido (ou ausente), ou
    /// o <see cref="DomainError"/> correspondente quando a Unidade referenciada
    /// não existe / foi removida ou não é do tipo reitoria.
    /// </summary>
    public static async Task<DomainError?> ValidarAsync(
        Guid? unidadeRaizId,
        IUnidadeRepository unidadeRepository,
        CancellationToken cancellationToken)
    {
        if (unidadeRaizId is not { } id)
        {
            return null;
        }

        Unidade? unidade = await unidadeRepository
            .ObterPorIdParaLeituraAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (unidade is null)
        {
            return new DomainError(
                InstituicaoErrorCodes.UnidadeRaizNaoEncontrada,
                "A Unidade informada como raiz não foi encontrada ou foi removida.");
        }

        if (unidade.Tipo != TipoUnidade.Reitoria)
        {
            return new DomainError(
                InstituicaoErrorCodes.UnidadeRaizNaoEhReitoria,
                "A Unidade informada como raiz da Instituição deve ser do tipo reitoria.");
        }

        return null;
    }
}

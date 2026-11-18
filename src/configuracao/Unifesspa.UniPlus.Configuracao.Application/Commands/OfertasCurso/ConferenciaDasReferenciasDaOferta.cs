namespace Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Confere que o curso e o local de uma oferta nova existem vivos e congela a unidade ofertante por cópia (ADR-0061): a
/// unidade é lida viva pelo leitor entre módulos (ADR-0056), e sem identidade viva não há o que congelar. O filtro global de
/// exclusão lógica já exclui os removidos, então inexistente e removido caem no mesmo erro.
/// </summary>
public sealed class ConferenciaDasReferenciasDaOferta(
    ICursoRepository cursoRepository,
    ILocalOfertaRepository localOfertaRepository,
    IUnidadeReader unidadeReader)
{
    public async Task<Result<UnidadeOfertante>> ConferirAsync(
        Guid cursoId, Guid localOfertaId, Guid unidadeOfertanteOrigemId, CancellationToken cancellationToken)
    {
        Result<UnidadeOfertante>? recusa = await ConferirCursoELocalAsync(cursoId, localOfertaId, cancellationToken).ConfigureAwait(false);

        return recusa ?? await CongelarUnidadeAsync(unidadeOfertanteOrigemId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<UnidadeOfertante>?> ConferirCursoELocalAsync(Guid cursoId, Guid localOfertaId, CancellationToken cancellationToken)
    {
        if (await cursoRepository.ObterPorIdParaLeituraAsync(cursoId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Recusar(OfertaCursoErrorCodes.CursoInexistenteOuRemovido, "Curso inexistente ou removido — a oferta exige um curso vivo.");
        }

        return await localOfertaRepository.ObterPorIdParaLeituraAsync(localOfertaId, cancellationToken).ConfigureAwait(false) is null
            ? Recusar(OfertaCursoErrorCodes.LocalOfertaInexistenteOuRemovido, "Local de oferta inexistente ou removido — a oferta exige um local vivo.")
            : null;
    }

    private static Result<UnidadeOfertante> Recusar(string codigo, string mensagem) =>
        Result<UnidadeOfertante>.Failure(new DomainError(codigo, mensagem));

    // O tipo da visão já é a string estável do contrato, desacoplada do enum interno de Organização: congela como veio.
    private async Task<Result<UnidadeOfertante>> CongelarUnidadeAsync(Guid origemId, CancellationToken cancellationToken)
    {
        UnidadeView? unidade = await unidadeReader.ObterPorIdAsync(origemId, cancellationToken).ConfigureAwait(false);

        return unidade is null
            ? Result<UnidadeOfertante>.Failure(new DomainError(
                OfertaCursoErrorCodes.UnidadeOfertanteInexistente,
                "Unidade ofertante inexistente ou removida — não há identidade viva para congelar."))
            : UnidadeOfertante.Criar(unidade.Id, unidade.Sigla, unidade.Nome, unidade.Tipo);
    }
}

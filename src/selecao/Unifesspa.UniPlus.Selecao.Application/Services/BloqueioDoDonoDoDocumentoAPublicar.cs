namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// O bloqueio compartilhado do dono do documento a publicar, que antes da primeira publicação é o rascunho e depois é o
/// processo. Confirmar o documento é gravar o que depende de o dono existir: o bloqueio convive com outra confirmação,
/// mas espera o <c>PUT</c>, a exclusão e a publicação do rascunho, de modo que não sobra documento confirmado de dono
/// que deixou de existir. A ordem dos bloqueios é sempre a do rascunho, depois a do documento.
/// </summary>
public sealed class BloqueioDoDonoDoDocumentoAPublicar(
    IRascunhoDeProcessoSeletivoRepository rascunhos, IProcessoSeletivoRepository processos)
{
    /// <summary>Bloqueia o dono e devolve se ele existe; sem rascunho, o processo publicado é o dono.</summary>
    public async Task<bool> BloquearSeExisteAsync(Guid processoSeletivoId, CancellationToken cancellationToken) =>
        await rascunhos.BloquearCompartilhadoAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false)
        || await processos.ExisteAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
}

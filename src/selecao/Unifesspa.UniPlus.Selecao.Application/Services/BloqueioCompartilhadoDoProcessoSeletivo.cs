namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// O bloqueio compartilhado vale pelo identificador único do processo seletivo. Antes da primeira publicação, bloqueia o
/// rascunho; depois dela, apenas confere que o processo existe. Serve a quem confirma um arquivo que depende de o processo
/// existir. A ordem dos bloqueios é sempre a do rascunho, depois a da linha do arquivo.
/// </summary>
public sealed class BloqueioCompartilhadoDoProcessoSeletivo(
    IRascunhoDeProcessoSeletivoRepository rascunhos, IProcessoSeletivoRepository processos)
{
    /// <summary>Bloqueia o dono e devolve se ele existe; sem rascunho, o processo publicado é o dono.</summary>
    public async Task<bool> BloquearSeExisteAsync(Guid processoSeletivoId, CancellationToken cancellationToken) =>
        await rascunhos.BloquearCompartilhadoAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false)
        || await processos.ExisteAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
}

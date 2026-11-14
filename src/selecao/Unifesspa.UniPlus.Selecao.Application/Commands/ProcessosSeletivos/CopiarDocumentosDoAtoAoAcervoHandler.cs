namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Copia os documentos do ato publicado ao acervo e registra os endereços na divulgação do certame (ADR-0132). A
/// reentrega é esperada e não duplica nada: a cópia já feita é encontrada no acervo.
/// </summary>
public static class CopiarDocumentosDoAtoAoAcervoHandler
{
    public static async Task Handle(
        CopiarDocumentosDoAtoAoAcervo mensagem,
        CopiaDosDocumentosDoAto copia,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        ArgumentNullException.ThrowIfNull(copia);

        await copia.CopiarERegistrarAsync(mensagem, cancellationToken).ConfigureAwait(false);
    }
}

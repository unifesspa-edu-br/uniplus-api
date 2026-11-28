namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

/// <summary>O que a conferência da operação resolveu, e as causas que recusaram o que ela não resolveu.</summary>
internal sealed record OperacaoConferida(
    DocumentosConferidos Documentos, FatosConferidos Fatos)
{
    /// <summary>As causas que apontam o documento da configuração: o edital e os documentos, na ordem da conferência.</summary>
    public IReadOnlyList<FieldError> CausasDosDocumentos => Documentos.Causas;

    /// <summary>As causas que apontam o campo do pedido, e não o documento da configuração.</summary>
    public IReadOnlyList<FieldError> CausasDoPedido => Documentos.CausasDoPedido;

    /// <summary>As causas dos fatos que a versão congela.</summary>
    public IReadOnlyList<FieldError> CausasDosFatos => Fatos.Causas;
}

/// <summary>
/// O que só a operação confere sobre a configuração já conferida: o edital e os documentos, os fatos que a versão congela.
/// Cada conferência roda sempre e acumula a sua causa; só deixa de rodar a que lê o dado que
/// outra recusou (<see cref="ConferenciaDosDocumentosDaOperacao"/> e <see cref="ConferenciaDosFatosDaVersao"/>).
/// </summary>
public sealed class ConferenciaDaOperacaoAoCongelar(
    ConferenciaDosDocumentosDaOperacao documentos)
{
    internal async Task<OperacaoConferida> ConferirAsync(
        ProcessoSeletivo processo,
        EditalDeclarado edital,
        AtoEDocumentoConferidos ato,
        OrigemDoCongelamento origem,
        LeiturasDoCongelamento lidos,
        CancellationToken cancellationToken)
    {
        DocumentosConferidos doEdital = await documentos.ConferirAsync(processo, edital, ato, origem, lidos, cancellationToken).ConfigureAwait(false);
        FatosConferidos dosFatos = ConferenciaDosFatosDaVersao.Conferir(processo, lidos);

        return new OperacaoConferida(doEdital, dosFatos);
    }
}

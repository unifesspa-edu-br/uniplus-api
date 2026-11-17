namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

/// <summary>O que a conferência da operação resolveu, e as causas que recusaram o que ela não resolveu.</summary>
internal sealed record OperacaoConferida(
    DocumentosConferidos Documentos, FatosConferidos Fatos, IReadOnlyList<FieldError> RecusasDaOrigem)
{
    /// <summary>As causas na ordem da conferência: o edital e os documentos, os fatos e, por fim, as da origem.</summary>
    public IReadOnlyList<FieldError> Causas => [.. Documentos.Causas, .. Fatos.Causas, .. RecusasDaOrigem];
}

/// <summary>
/// O que só a operação confere sobre a configuração já conferida: o edital e os documentos, os fatos que a versão congela
/// e o que a origem recusa. Cada conferência roda sempre e acumula a sua causa; só deixa de rodar a que lê o dado que
/// outra recusou (<see cref="ConferenciaDosDocumentosDaOperacao"/> e <see cref="ConferenciaDosFatosDaVersao"/>).
/// </summary>
public sealed class ConferenciaDaOperacaoAoCongelar(
    ConferenciaDosDocumentosDaOperacao documentos,
    ConferenciaDosFatosDaVersao fatos,
    LeituraDaBaseDaRetificacao leituraDaBase)
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
        FatosConferidos dosFatos = await fatos.ConferirAsync(processo, origem, lidos, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<FieldError> recusasDaOrigem = await origem
            .RecusasDaOperacaoAsync(leituraDaBase, processo, ato.Publicacao.Instante, cancellationToken)
            .ConfigureAwait(false);

        return new OperacaoConferida(doEdital, dosFatos, recusasDaOrigem);
    }
}

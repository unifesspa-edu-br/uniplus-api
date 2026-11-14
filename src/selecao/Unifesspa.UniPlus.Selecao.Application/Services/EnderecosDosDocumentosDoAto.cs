namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Interfaces;

using Queries.ProcessosSeletivos;

/// <summary>
/// Resolve os documentos que o ato de uma versão publicou no acervo, com o portão da cópia: o endereço só é exposto
/// depois de a cópia ao acervo registrá-lo na divulgação do certame, o mesmo portão do certame público.
/// </summary>
public sealed class EnderecosDosDocumentosDoAto(ICertameDivulgadoRepository certames, IEnderecoNoAcervoPublico endereco)
{
    public async Task<DocumentosDoAtoNoAcervo> DoAtoAsync(Guid processoSeletivoId, Guid atoId, CancellationToken cancellationToken)
    {
        Domain.Entities.CertameDivulgado? divulgado = await certames
            .ObterParaLeituraAsync(processoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        return new DocumentosDoAtoNoAcervo(processoSeletivoId, atoId, endereco.De, divulgado?.CopiouOsDocumentosDoAto(atoId) ?? false);
    }
}

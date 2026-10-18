namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Divulga os documentos de um ato do processo que não cria versão de configuração: aviso, errata,
/// resultado declarado no edital e semelhantes.
/// </summary>
/// <remarks>
/// <para>
/// A fonte é o registro do ato do processo, gravado pela Seleção junto do envio do ato: a mensagem de
/// confirmação leva só o ato, o ano e o tipo, e não diz de qual processo nem com quais arquivos. Ato
/// sem registro é de outro domínio e não tem o que divulgar aqui.
/// </para>
/// <para>
/// Roda na mesma condição do edital (ADR-0132, emenda 1): depois de o ato estar registrado, copiando
/// o principal e os anexos ao acervo antes de gravar os endereços na divulgação do certame. Falha na
/// cópia lança, a divulgação não avança, e a reentrega repete encontrando no acervo o que já foi
/// copiado.
/// </para>
/// <para>
/// A unidade e o identificador do processo na chave do principal são os gravados no registro do ato no
/// momento da publicação: a chave é parte do endereço divulgado e não pode mudar depois.
/// </para>
/// </remarks>
internal static class DivulgarAtoDoProcessoNoAcervo
{
    public static async Task DivulgarAsync(
        AtoNormativoRegistrado mensagem,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IAcervoPublico acervoPublico,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        AtoDoProcesso? ato = await atoDoProcessoRepository
            .ObterComAnexosAsync(mensagem.AtoId, cancellationToken)
            .ConfigureAwait(false);

        if (ato is null)
        {
            return;
        }

        CertameDivulgado divulgado = await certameDivulgadoRepository
            .ObterParaMutacaoAsync(ato.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new CertameAindaNaoDivulgadoException(
                $"O processo {ato.ProcessoSeletivoId} ainda não tem divulgação para receber os documentos do ato {ato.Id}.");

        IReadOnlyList<AnexoDoAtoDoProcesso> anexos = [.. ato.Anexos.OrderBy(static a => a.Ordem)];
        IReadOnlyList<DocumentoAPublicar> cadastrados = await documentoAPublicarRepository
            .ListarDoProcessoAsync(
                ato.ProcessoSeletivoId,
                [ato.DocumentoPrincipalId, .. anexos.Select(static a => a.DocumentoAPublicarId)],
                cancellationToken)
            .ConfigureAwait(false);

        DocumentoDivulgado principal = await PublicarPrincipalAsync(
            mensagem, ato, Selado(ato, cadastrados, ato.DocumentoPrincipalId), acervoPublico, enderecoNoAcervo, cancellationToken)
            .ConfigureAwait(false);

        List<DocumentoDivulgado> documentos = [principal];
        foreach (AnexoDoAtoDoProcesso anexo in anexos)
        {
            documentos.Add(await PublicarAnexoAsync(
                ato, anexo, Selado(ato, cadastrados, anexo.DocumentoAPublicarId), acervoPublico, enderecoNoAcervo, cancellationToken)
                .ConfigureAwait(false));
        }

        // Registrado só depois da cópia: o endereço divulgado nunca aponta para arquivo ausente.
        if (!divulgado.RegistrarEdital(new EditalDivulgado(ato.Id, null, principal.UrlDownload, documentos)))
        {
            return;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<DocumentoDivulgado> PublicarPrincipalAsync(
        AtoNormativoRegistrado mensagem,
        AtoDoProcesso ato,
        DocumentoSelado documento,
        IAcervoPublico acervoPublico,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        CancellationToken cancellationToken)
    {
        string chave = ChaveNoAcervoPublico.DoEdital(
            mensagem.Ano,
            ato.UnidadeAdministradora,
            mensagem.TipoCodigo,
            ato.IdentificadorLegivelDoProcesso,
            ato.Id,
            documento.Hash,
            documento.Formato);
        string nome = $"{ato.IdentificadorLegivelDoProcesso}-{ChaveNoAcervoPublico.SegmentoDoTipo(mensagem.TipoCodigo)}.{FormatosDeDocumento.Extensao(documento.Formato)}";

        return await CopiarAsync(
            PapelDoDocumentoDivulgado.Principal, nome, chave, ato, documento, acervoPublico, enderecoNoAcervo, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<DocumentoDivulgado> PublicarAnexoAsync(
        AtoDoProcesso ato,
        AnexoDoAtoDoProcesso anexo,
        DocumentoSelado documento,
        IAcervoPublico acervoPublico,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        CancellationToken cancellationToken)
    {
        string chave = ChaveNoAcervoPublico.DoAnexo(ato.ProcessoSeletivoId, ato.Id, anexo.Id, documento.Hash, documento.Formato);

        return await CopiarAsync(
            PapelDoDocumentoDivulgado.Anexo, anexo.NomeDeExibicao, chave, ato, documento, acervoPublico, enderecoNoAcervo, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<DocumentoDivulgado> CopiarAsync(
        string papel,
        string nome,
        string chave,
        AtoDoProcesso ato,
        DocumentoSelado documento,
        IAcervoPublico acervoPublico,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        CancellationToken cancellationToken)
    {
        await acervoPublico
            .CopiarAsync(
                documento.ChavePrivada,
                chave,
                new DocumentoNoAcervo(documento.TipoDeMidia, nome, ato.Id, ato.ProcessoSeletivoId, documento.Hash),
                cancellationToken)
            .ConfigureAwait(false);

        return new DocumentoDivulgado(
            papel, nome, documento.TipoDeMidia, documento.TamanhoBytes, documento.Hash, enderecoNoAcervo.De(chave).OriginalString);
    }

    /// <summary>
    /// O documento do ato com a cópia selada privada de onde o acervo o lê. Documento ausente, de
    /// outro processo ou sem cópia selada é dado inconsistente: divulgar o endereço de um arquivo que o
    /// ato não oficializou seria pior que não divulgar.
    /// </summary>
    private static DocumentoSelado Selado(AtoDoProcesso ato, IReadOnlyList<DocumentoAPublicar> cadastrados, Guid documentoId)
    {
        DocumentoAPublicar? cadastrado = cadastrados.FirstOrDefault(d => d.Id == documentoId);

        return cadastrado is { ObjectKeyConfirmado: { } chavePrivada, HashSha256: { } hash, TamanhoBytes: { } tamanho }
            ? new DocumentoSelado(chavePrivada, hash, tamanho, cadastrado.Formato, cadastrado.TipoDeMidia)
            : throw new InvalidOperationException(
                $"O documento {documentoId} do ato {ato.Id} do processo {ato.ProcessoSeletivoId} não tem cópia selada confirmada.");
    }

    private sealed record DocumentoSelado(
        string ChavePrivada,
        string Hash,
        long TamanhoBytes,
        FormatoDeDocumento Formato,
        string TipoDeMidia);
}

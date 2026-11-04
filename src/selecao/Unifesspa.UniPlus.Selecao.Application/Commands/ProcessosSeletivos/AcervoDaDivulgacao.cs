namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json.Nodes;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Copia para o acervo público o que a divulgação publica — o documento do edital, os anexos e os
/// modelos que a versão congelou, e os documentos de um ato do processo — e monta o edital divulgado
/// que a linha do certame registra.
/// </summary>
/// <remarks>
/// O que se publica vem do envelope ou do registro do ato — nome, formato, tamanho e hash —, e do
/// cadastro só se lê onde está a cópia selada privada. Documento sem cópia selada, ou com outro
/// conteúdo, é dado inconsistente: divulgar o endereço de um arquivo que não é o congelado seria pior
/// que não divulgar.
/// </remarks>
internal sealed class AcervoDaDivulgacao
{
    private readonly IDocumentoAPublicarRepository _documentos;
    private readonly IModeloDeDocumentoRepository _modelos;
    private readonly IAcervoPublico _acervo;
    private readonly IEnderecoNoAcervoPublico _endereco;

    public AcervoDaDivulgacao(
        IDocumentoAPublicarRepository documentos,
        IModeloDeDocumentoRepository modelos,
        IAcervoPublico acervo,
        IEnderecoNoAcervoPublico endereco)
    {
        _documentos = documentos;
        _modelos = modelos;
        _acervo = acervo;
        _endereco = endereco;
    }

    /// <summary>O endereço público de uma chave do acervo.</summary>
    internal Uri EnderecoDe(string chave) => _endereco.De(chave);

    /// <summary>
    /// Copia o edital, os anexos e os modelos para o acervo e devolve o edital divulgado.
    /// </summary>
    internal async Task<EditalDivulgado> PublicarVersaoAsync(
        VersaoConfiguracao versao,
        AtoDaDivulgacao ato,
        JsonObject envelope,
        Uri enderecoDoEdital,
        CancellationToken cancellationToken)
    {
        DocumentoDivulgado principal = await PublicarEditalAsync(versao, ato, envelope, enderecoDoEdital, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<DocumentoDivulgado> anexos = await PublicarAnexosAsync(versao, envelope, cancellationToken)
            .ConfigureAwait(false);
        await PublicarModelosAsync(versao, envelope, cancellationToken).ConfigureAwait(false);

        return new EditalDivulgado(versao.AtoCriadorId, versao.NumeroVersao, principal.UrlDownload, [principal, .. anexos]);
    }

    private async Task<DocumentoDivulgado> PublicarEditalAsync(
        VersaoConfiguracao versao,
        AtoDaDivulgacao ato,
        JsonObject envelope,
        Uri enderecoDoEdital,
        CancellationToken cancellationToken)
    {
        if (!LeituraDoCertameEdital.TentarEditalNoAcervo(envelope, ato, out EditalNoAcervo? edital))
        {
            throw new InvalidOperationException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não tem a forma esperada no documento do edital.");
        }

        DocumentoAPublicar documento = await ObterCopiaSeladaDoEditalAsync(versao, edital, cancellationToken).ConfigureAwait(false);
        DocumentoDivulgado divulgado = DivulgarEdital(edital, documento, enderecoDoEdital);

        await _acervo
            .CopiarAsync(
                documento.ObjectKeyConfirmado!,
                edital.Chave,
                new DocumentoNoAcervo(documento.TipoDeMidia, edital.NomeDeApresentacao, versao.AtoCriadorId, versao.ProcessoSeletivoId, edital.HashSha256),
                cancellationToken)
            .ConfigureAwait(false);

        return divulgado;
    }

    private static DocumentoDivulgado DivulgarEdital(EditalNoAcervo edital, DocumentoAPublicar documento, Uri enderecoDoEdital) => new(
        PapelDoDocumentoDivulgado.Principal,
        edital.NomeDeApresentacao,
        documento.TipoDeMidia,
        documento.TamanhoBytes ?? throw new InvalidOperationException(
            $"O documento do edital {edital.DocumentoEditalId} está confirmado e sem tamanho gravado."),
        edital.HashSha256,
        enderecoDoEdital.OriginalString);

    private async Task<DocumentoAPublicar> ObterCopiaSeladaDoEditalAsync(
        VersaoConfiguracao versao, EditalNoAcervo edital, CancellationToken cancellationToken)
    {
        DocumentoAPublicar? documento = await _documentos
            .ObterPorIdAsync(edital.DocumentoEditalId, cancellationToken)
            .ConfigureAwait(false);

        return documento is not null
            && documento.ProcessoSeletivoId == versao.ProcessoSeletivoId
            && documento.ObjectKeyConfirmado is not null
            && string.Equals(documento.HashSha256, edital.HashSha256, StringComparison.Ordinal)
                ? documento
                : throw new InvalidOperationException(
                    $"O documento do edital {edital.DocumentoEditalId}, congelado na versão {versao.NumeroVersao} do processo {versao.ProcessoSeletivoId}, não tem cópia selada com o conteúdo congelado.");
    }

    private async Task<IReadOnlyList<DocumentoDivulgado>> PublicarAnexosAsync(
        VersaoConfiguracao versao, JsonObject envelope, CancellationToken cancellationToken)
    {
        if (!LeituraDoCertameEdital.TentarAnexosCongelados(envelope, out List<AnexoDoEdital>? anexos))
        {
            throw new InvalidOperationException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não tem a forma esperada nos anexos do edital.");
        }

        if (anexos.Count == 0)
        {
            return [];
        }

        IReadOnlyList<DocumentoAPublicar> cadastrados = await _documentos
            .ListarDoProcessoAsync(versao.ProcessoSeletivoId, [.. anexos.Select(static a => a.DocumentoId)], cancellationToken)
            .ConfigureAwait(false);

        List<DocumentoDivulgado> divulgados = [];
        foreach (AnexoDoEdital anexo in anexos)
        {
            divulgados.Add(await PublicarAnexoAsync(versao, anexo, cadastrados, cancellationToken).ConfigureAwait(false));
        }

        return divulgados;
    }

    private async Task<DocumentoDivulgado> PublicarAnexoAsync(
        VersaoConfiguracao versao,
        AnexoDoEdital anexo,
        IReadOnlyList<DocumentoAPublicar> cadastrados,
        CancellationToken cancellationToken)
    {
        DocumentoAPublicar? cadastrado = cadastrados.FirstOrDefault(d => d.Id == anexo.DocumentoId);
        if (cadastrado?.ObjectKeyConfirmado is not { } chavePrivada
            || !string.Equals(cadastrado.HashSha256, anexo.HashSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"O anexo {anexo.Id} do edital, congelado na versão {versao.NumeroVersao} do processo {versao.ProcessoSeletivoId}, não tem cópia selada com o conteúdo congelado.");
        }

        string chave = ChaveNoAcervoPublico.DoAnexo(versao.ProcessoSeletivoId, versao.AtoCriadorId, anexo);
        await _acervo
            .CopiarAsync(
                chavePrivada,
                chave,
                new DocumentoNoAcervo(anexo.TipoDeMidia, anexo.NomeArquivo, versao.AtoCriadorId, versao.ProcessoSeletivoId, anexo.HashSha256),
                cancellationToken)
            .ConfigureAwait(false);

        return new DocumentoDivulgado(
            PapelDoDocumentoDivulgado.Anexo,
            anexo.NomeArquivo,
            anexo.TipoDeMidia,
            anexo.TamanhoBytes,
            anexo.HashSha256,
            EnderecoDe(chave).OriginalString);
    }

    private async Task PublicarModelosAsync(
        VersaoConfiguracao versao, JsonObject envelope, CancellationToken cancellationToken)
    {
        if (!LeituraDoCertameDocumentos.TentarModelosCongelados(envelope, out List<ModeloDaExigencia>? modelos))
        {
            throw new InvalidOperationException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não tem a forma esperada nos modelos de documento.");
        }

        if (modelos.Count == 0)
        {
            return;
        }

        IReadOnlyList<ModeloDeDocumento> cadastrados = await _modelos
            .ListarDoProcessoAsync(versao.ProcessoSeletivoId, [.. modelos.Select(static m => m.ModeloId)], cancellationToken)
            .ConfigureAwait(false);

        foreach (ModeloDaExigencia modelo in modelos)
        {
            await PublicarModeloAsync(versao, modelo, cadastrados, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PublicarModeloAsync(
        VersaoConfiguracao versao,
        ModeloDaExigencia modelo,
        IReadOnlyList<ModeloDeDocumento> cadastrados,
        CancellationToken cancellationToken)
    {
        ModeloDeDocumento? cadastrado = cadastrados.FirstOrDefault(m => m.Id == modelo.ModeloId);
        if (cadastrado?.ObjectKeyConfirmado is not { } chavePrivada
            || !string.Equals(cadastrado.HashSha256, modelo.HashSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"O modelo de documento {modelo.ModeloId}, congelado na versão {versao.NumeroVersao} do processo {versao.ProcessoSeletivoId}, não tem cópia selada com o conteúdo congelado.");
        }

        await _acervo
            .CopiarAsync(
                chavePrivada,
                ChaveNoAcervoPublico.DoModelo(versao.ProcessoSeletivoId, versao.AtoCriadorId, modelo),
                new DocumentoNoAcervo(
                    FormatosDeDocumento.TipoDeMidia(modelo.Formato),
                    modelo.NomeArquivo,
                    versao.AtoCriadorId,
                    versao.ProcessoSeletivoId,
                    modelo.HashSha256),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Copia o principal e os anexos do ato do processo para o acervo e devolve o edital divulgado.
    /// </summary>
    internal async Task<EditalDivulgado> PublicarAtoAsync(AtoNormativoRegistrado mensagem, AtoDoProcesso ato, CancellationToken cancellationToken)
    {
        IReadOnlyList<AnexoDoAtoDoProcesso> anexos = [.. ato.Anexos.OrderBy(static a => a.Ordem)];
        IReadOnlyList<DocumentoAPublicar> cadastrados = await _documentos
            .ListarDoProcessoAsync(
                ato.ProcessoSeletivoId,
                [ato.DocumentoPrincipalId, .. anexos.Select(static a => a.DocumentoAPublicarId)],
                cancellationToken)
            .ConfigureAwait(false);

        DocumentoDivulgado principal = await PublicarPrincipalDoAtoAsync(
            mensagem, ato, SeladoDoAto(ato, cadastrados, ato.DocumentoPrincipalId), cancellationToken)
            .ConfigureAwait(false);

        List<DocumentoDivulgado> documentos = [principal];
        foreach (AnexoDoAtoDoProcesso anexo in anexos)
        {
            documentos.Add(await PublicarAnexoDoAtoAsync(
                ato, anexo, SeladoDoAto(ato, cadastrados, anexo.DocumentoAPublicarId), cancellationToken)
                .ConfigureAwait(false));
        }

        return new EditalDivulgado(ato.Id, null, principal.UrlDownload, documentos);
    }

    private async Task<DocumentoDivulgado> PublicarPrincipalDoAtoAsync(
        AtoNormativoRegistrado mensagem,
        AtoDoProcesso ato,
        DocumentoSelado documento,
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

        return await CopiarDocumentoDoAtoAsync(
            PapelDoDocumentoDivulgado.Principal, nome, nome, chave, ato, documento, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<DocumentoDivulgado> PublicarAnexoDoAtoAsync(
        AtoDoProcesso ato,
        AnexoDoAtoDoProcesso anexo,
        DocumentoSelado documento,
        CancellationToken cancellationToken)
    {
        string chave = ChaveNoAcervoPublico.DoAnexo(ato.ProcessoSeletivoId, ato.Id, anexo.Id, documento.Hash, documento.Formato);

        // O rótulo exibido não carrega extensão; o nome de download precisa dela para o arquivo abrir
        // no programa certo.
        string extensao = FormatosDeDocumento.Extensao(documento.Formato);
        string nomeDeDownload = anexo.NomeDeExibicao.EndsWith($".{extensao}", StringComparison.OrdinalIgnoreCase)
            ? anexo.NomeDeExibicao
            : $"{anexo.NomeDeExibicao}.{extensao}";

        return await CopiarDocumentoDoAtoAsync(
            PapelDoDocumentoDivulgado.Anexo, anexo.NomeDeExibicao, nomeDeDownload, chave, ato, documento, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<DocumentoDivulgado> CopiarDocumentoDoAtoAsync(
        string papel,
        string nome,
        string nomeDeDownload,
        string chave,
        AtoDoProcesso ato,
        DocumentoSelado documento,
        CancellationToken cancellationToken)
    {
        await _acervo
            .CopiarAsync(
                documento.ChavePrivada,
                chave,
                new DocumentoNoAcervo(documento.TipoDeMidia, nomeDeDownload, ato.Id, ato.ProcessoSeletivoId, documento.Hash),
                cancellationToken)
            .ConfigureAwait(false);

        return new DocumentoDivulgado(
            papel, nome, documento.TipoDeMidia, documento.TamanhoBytes, documento.Hash, EnderecoDe(chave).OriginalString);
    }

    /// <summary>
    /// O documento do ato com a cópia selada privada de onde o acervo o lê. Documento ausente, de
    /// outro processo ou sem cópia selada é dado inconsistente: divulgar o endereço de um arquivo que o
    /// ato não oficializou seria pior que não divulgar.
    /// </summary>
    private static DocumentoSelado SeladoDoAto(AtoDoProcesso ato, IReadOnlyList<DocumentoAPublicar> cadastrados, Guid documentoId)
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

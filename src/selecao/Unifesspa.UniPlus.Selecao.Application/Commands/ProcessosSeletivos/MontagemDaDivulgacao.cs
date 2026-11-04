namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json.Nodes;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Copia para o acervo público o que a versão congelou — o documento do edital, os anexos e os
/// modelos de documento — e monta o edital divulgado que a linha do certame registra.
/// </summary>
/// <remarks>
/// O que se publica vem do envelope — nome, formato, tamanho e hash são os do edital —, e do cadastro
/// só se lê onde está a cópia selada privada. Documento sem cópia selada, ou com outro conteúdo, é dado
/// inconsistente: divulgar o endereço de um arquivo que não é o congelado seria pior que não divulgar.
/// </remarks>
internal static class MontagemDaDivulgacao
{
    /// <summary>
    /// Copia o edital, os anexos e os modelos para o acervo e devolve o edital divulgado.
    /// </summary>
    internal static async Task<EditalDivulgado> PublicarNoAcervoAsync(
        VersaoConfiguracao versao,
        AtoDaDivulgacao ato,
        JsonObject envelope,
        Uri enderecoDoEdital,
        AcervoDaDivulgacao acervo,
        CancellationToken cancellationToken)
    {
        DocumentoDivulgado principal = await PublicarEditalAsync(versao, ato, envelope, enderecoDoEdital, acervo, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<DocumentoDivulgado> anexos = await PublicarAnexosAsync(versao, envelope, acervo, cancellationToken)
            .ConfigureAwait(false);
        await PublicarModelosAsync(versao, envelope, acervo, cancellationToken).ConfigureAwait(false);

        return new EditalDivulgado(versao.AtoCriadorId, versao.NumeroVersao, principal.UrlDownload, [principal, .. anexos]);
    }

    private static async Task<DocumentoDivulgado> PublicarEditalAsync(
        VersaoConfiguracao versao,
        AtoDaDivulgacao ato,
        JsonObject envelope,
        Uri enderecoDoEdital,
        AcervoDaDivulgacao acervo,
        CancellationToken cancellationToken)
    {
        if (!LeituraDoCertameEdital.TentarEditalNoAcervo(envelope, ato, out EditalNoAcervo? edital))
        {
            throw new InvalidOperationException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não tem a forma esperada no documento do edital.");
        }

        DocumentoAPublicar documento = await ObterCopiaSeladaDoEditalAsync(versao, edital, acervo, cancellationToken).ConfigureAwait(false);
        DocumentoDivulgado divulgado = DivulgarEdital(edital, documento, enderecoDoEdital);

        await acervo.Acervo
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

    private static async Task<DocumentoAPublicar> ObterCopiaSeladaDoEditalAsync(
        VersaoConfiguracao versao, EditalNoAcervo edital, AcervoDaDivulgacao acervo, CancellationToken cancellationToken)
    {
        DocumentoAPublicar? documento = await acervo.Documentos
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

    private static async Task<IReadOnlyList<DocumentoDivulgado>> PublicarAnexosAsync(
        VersaoConfiguracao versao, JsonObject envelope, AcervoDaDivulgacao acervo, CancellationToken cancellationToken)
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

        IReadOnlyList<DocumentoAPublicar> cadastrados = await acervo.Documentos
            .ListarDoProcessoAsync(versao.ProcessoSeletivoId, [.. anexos.Select(static a => a.DocumentoId)], cancellationToken)
            .ConfigureAwait(false);

        List<DocumentoDivulgado> divulgados = [];
        foreach (AnexoDoEdital anexo in anexos)
        {
            divulgados.Add(await PublicarAnexoAsync(versao, anexo, cadastrados, acervo, cancellationToken).ConfigureAwait(false));
        }

        return divulgados;
    }

    private static async Task<DocumentoDivulgado> PublicarAnexoAsync(
        VersaoConfiguracao versao,
        AnexoDoEdital anexo,
        IReadOnlyList<DocumentoAPublicar> cadastrados,
        AcervoDaDivulgacao acervo,
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
        await acervo.Acervo
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
            acervo.Endereco.De(chave).OriginalString);
    }

    private static async Task PublicarModelosAsync(
        VersaoConfiguracao versao, JsonObject envelope, AcervoDaDivulgacao acervo, CancellationToken cancellationToken)
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

        IReadOnlyList<ModeloDeDocumento> cadastrados = await acervo.Modelos
            .ListarDoProcessoAsync(versao.ProcessoSeletivoId, [.. modelos.Select(static m => m.ModeloId)], cancellationToken)
            .ConfigureAwait(false);

        foreach (ModeloDaExigencia modelo in modelos)
        {
            await PublicarModeloAsync(versao, modelo, cadastrados, acervo, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task PublicarModeloAsync(
        VersaoConfiguracao versao,
        ModeloDaExigencia modelo,
        IReadOnlyList<ModeloDeDocumento> cadastrados,
        AcervoDaDivulgacao acervo,
        CancellationToken cancellationToken)
    {
        ModeloDeDocumento? cadastrado = cadastrados.FirstOrDefault(m => m.Id == modelo.ModeloId);
        if (cadastrado?.ObjectKeyConfirmado is not { } chavePrivada
            || !string.Equals(cadastrado.HashSha256, modelo.HashSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"O modelo de documento {modelo.ModeloId}, congelado na versão {versao.NumeroVersao} do processo {versao.ProcessoSeletivoId}, não tem cópia selada com o conteúdo congelado.");
        }

        await acervo.Acervo
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
}

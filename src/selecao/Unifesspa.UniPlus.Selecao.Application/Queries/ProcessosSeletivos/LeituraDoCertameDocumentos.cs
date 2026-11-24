namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;

/// <summary>
/// Leitura, no envelope congelado, das exigências de documentos: rótulo, aplicabilidade, obrigatoriedade, formatos, prazo
/// de emissão, validade e modelo editável, e a cópia congelada dos modelos que a divulgação publica no acervo. O
/// atendimento especializado tem leitura própria.
/// </summary>
internal static class LeituraDoCertameDocumentos
{
    /// <summary>
    /// Só o que o candidato precisa para reunir documentos. Os blocos irmãos de
    /// <c>documentosExigidos</c> — obrigatoriedades legais, referência temporal dos fatos e os
    /// metadados dos fatos que condicionam cada exigência — não entram. <paramref name="incluir"/>
    /// recorta as exigências projetadas, como as da fase de um formulário; nulo quando a exigência
    /// não tem a forma que o recorte lê, o que recusa a leitura inteira em vez de omiti-la.
    /// <paramref name="acervo"/> dá o endereço do modelo publicado pelo ato da versão lida, e
    /// <paramref name="cronograma"/>, as fases e o fuso congelados na mesma versão, de onde saem as datas de referência.
    /// </summary>
    internal static bool TentarExigencias(
        JsonObject envelope,
        Func<JsonObject, bool?> incluir,
        DocumentosDoAtoNoAcervo acervo,
        CronogramaCongelado cronograma,
        [NotNullWhen(true)] out List<ExigenciaDocumentalCertameDto>? exigencias)
    {
        exigencias = null;
        if (!TentarArrayDeExigencias(envelope, out JsonArray? array)
            || !TentarIncluidas(array, incluir, acervo, cronograma, out List<ExigenciaDocumentalCertameDto>? lidas))
        {
            return false;
        }

        exigencias = lidas;
        return true;
    }

    /// <summary>O arranjo <c>exigencias</c> do bloco público de documentos exigidos.</summary>
    internal static bool TentarArrayDeExigencias(JsonObject envelope, [NotNullWhen(true)] out JsonArray? array)
    {
        array = null;
        return PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("documentosExigidos"), out JsonObject? bloco)
            && PrimitivasDaProjecao.TentarArray(bloco, "exigencias", out array);
    }

    private static bool TentarIncluidas(
        JsonArray array,
        Func<JsonObject, bool?> incluir,
        DocumentosDoAtoNoAcervo acervo,
        CronogramaCongelado cronograma,
        [NotNullWhen(true)] out List<ExigenciaDocumentalCertameDto>? exigencias)
    {
        exigencias = null;
        List<ExigenciaDocumentalCertameDto> lidas = [];
        foreach (JsonNode? item in array)
        {
            if (!TentarAcrescentar(item, incluir, acervo, cronograma, lidas))
            {
                return false;
            }
        }

        exigencias = lidas;
        return true;
    }

    private static bool TentarAcrescentar(
        JsonNode? item,
        Func<JsonObject, bool?> incluir,
        DocumentosDoAtoNoAcervo acervo,
        CronogramaCongelado cronograma,
        List<ExigenciaDocumentalCertameDto> lidas)
    {
        if (!TentarExigencia(item, acervo, cronograma, out JsonObject? bruta, out ExigenciaDocumentalCertameDto? exigencia)
            || incluir(bruta) is not { } incluida)
        {
            return false;
        }

        if (incluida)
        {
            lidas.Add(exigencia);
        }

        return true;
    }

    private static bool TentarExigencia(
        JsonNode? item,
        DocumentosDoAtoNoAcervo acervo,
        CronogramaCongelado cronograma,
        [NotNullWhen(true)] out JsonObject? bruta,
        [NotNullWhen(true)] out ExigenciaDocumentalCertameDto? exigencia)
    {
        bruta = item as JsonObject;
        exigencia = null;
        return bruta is not null && TentarDoObjeto(bruta, acervo, cronograma, out exigencia);
    }

    private static bool TentarDoObjeto(
        JsonObject objeto,
        DocumentosDoAtoNoAcervo acervo,
        CronogramaCongelado cronograma,
        [NotNullWhen(true)] out ExigenciaDocumentalCertameDto? exigencia)
    {
        exigencia = null;
        if (!PrimitivasDaProjecao.TentarTexto(objeto, "tipoDocumentoNome", out string rotulo)
            || !PrimitivasDaProjecao.TentarCodigoComRotulo(objeto, "aplicabilidade", out CodigoComRotulo<Aplicabilidade>? aplicabilidade)
            || !PrimitivasDaProjecao.TentarBooleano(objeto, "obrigatorio", out bool obrigatorio)
            || !TentarFormatos(objeto, out FormatosAceitosCertameDto? formatos)
            || !LeituraDoCertamePrazoEValidade.TentarPrazoEValidade(objeto, cronograma, out PrazoDeEmissaoCertameDto? prazo, out ValidadeDoDocumentoCertameDto? validade)
            || !TentarModelo(objeto, acervo, out ModeloDocumentalCertameDto? modelo))
        {
            return false;
        }

        exigencia = new ExigenciaDocumentalCertameDto(rotulo, aplicabilidade, obrigatorio, formatos, prazo, validade, modelo);
        return true;
    }

    /// <summary>
    /// Bicondicional do envelope: lista nula equivale a "qualquer formato". O tamanho máximo por
    /// formato fica fora — é limite de upload, não informação do edital.
    /// </summary>
    private static bool TentarFormatos(JsonObject exigencia, [NotNullWhen(true)] out FormatosAceitosCertameDto? formatos)
    {
        formatos = null;
        if (!PrimitivasDaProjecao.TentarObjeto(exigencia, "formatosPermitidos", out JsonObject? bloco)
            || !PrimitivasDaProjecao.TentarBooleano(bloco, "qualquer", out bool qualquer)
            || !bloco.TryGetPropertyValue("lista", out JsonNode? listaNode))
        {
            return false;
        }

        formatos = listaNode is null ? SemLista(qualquer) : ComLista(qualquer, listaNode);
        return formatos is not null;
    }

    /// <summary>
    /// A bicondicional é CONFERIDA, não só documentada: "aceita qualquer formato" com uma lista
    /// de formatos ao lado são duas afirmações contraditórias, e repassá-las publicaria a
    /// contradição num documento com efeito jurídico.
    /// </summary>
    private static FormatosAceitosCertameDto? SemLista(bool qualquer) => qualquer ? new FormatosAceitosCertameDto(true, null) : null;

    /// <summary>
    /// Lista VAZIA é a terceira forma contraditória: "não aceita qualquer formato" sem nenhum
    /// formato ao lado publicaria uma exigência que nenhum arquivo satisfaz.
    /// </summary>
    private static FormatosAceitosCertameDto? ComLista(bool qualquer, JsonNode listaNode) =>
        !qualquer
        && listaNode is JsonArray { Count: > 0 } array
        && PrimitivasDaProjecao.TentarItens<string>(array, TentarFormato, out List<string>? lista)
            ? new FormatosAceitosCertameDto(false, lista)
            : null;

    private static bool TentarFormato(JsonNode? item, [NotNullWhen(true)] out string? formato)
    {
        formato = null;
        return item is JsonObject entrada && PrimitivasDaProjecao.TentarTexto(entrada, "formato", out formato);
    }

    /// <summary>
    /// O modelo que a exigência oferece ao candidato: a chave é obrigatória, nula quando a
    /// exigência não tem modelo. O id do modelo fica fora como campo — é referência interna do
    /// cadastro —, e só aparece dentro do endereço no acervo.
    /// </summary>
    internal static bool TentarModelo(JsonObject exigencia, DocumentosDoAtoNoAcervo acervo, out ModeloDocumentalCertameDto? modelo)
    {
        modelo = null;
        if (!TentarModeloCongelado(exigencia, out ModeloDaExigencia? congelado))
        {
            return false;
        }

        modelo = congelado is null ? null : AoCandidato(congelado, acervo);
        return true;
    }

    /// <summary>
    /// Os modelos de documento que o edital congelou, um por modelo, na ordem das exigências — o
    /// que a divulgação publica no acervo. Vazio quando nenhuma exigência oferece modelo.
    /// </summary>
    internal static bool TentarModelosCongelados(JsonObject envelope, [NotNullWhen(true)] out List<ModeloDaExigencia>? modelos)
    {
        modelos = null;
        if (!TentarArrayDeExigencias(envelope, out JsonArray? array)
            || !TentarTodos(array, out List<ModeloDaExigencia>? lidos))
        {
            return false;
        }

        modelos = lidos;
        return true;
    }

    private static ModeloDocumentalCertameDto AoCandidato(ModeloDaExigencia congelado, DocumentosDoAtoNoAcervo acervo) => new(
        congelado.NomeArquivo,
        ModeloDeDocumento.TokenDe(congelado.Formato),
        congelado.HashSha256,
        acervo.DoModelo(congelado));

    private static bool TentarTodos(JsonArray array, [NotNullWhen(true)] out List<ModeloDaExigencia>? modelos)
    {
        modelos = [];
        foreach (JsonNode? item in array)
        {
            if (!TentarAcrescentar(item, modelos))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TentarAcrescentar(JsonNode? item, List<ModeloDaExigencia> lidos)
    {
        if (item is not JsonObject exigencia || !TentarModeloCongelado(exigencia, out ModeloDaExigencia? modelo))
        {
            return false;
        }

        if (modelo is not null && !lidos.Exists(m => m.ModeloId == modelo.ModeloId))
        {
            lidos.Add(modelo);
        }

        return true;
    }

    /// <summary>
    /// A cópia do modelo como o envelope a congelou, numa leitura só para o contrato público e para a
    /// publicação no acervo. Formato fora do vocabulário recusa a leitura.
    /// </summary>
    private static bool TentarModeloCongelado(JsonObject exigencia, out ModeloDaExigencia? modelo)
    {
        modelo = null;
        if (!exigencia.TryGetPropertyValue("modelo", out JsonNode? node))
        {
            return false;
        }

        return node is null || (node is JsonObject bloco && TentarModeloDoBloco(bloco, out modelo));
    }

    private static bool TentarModeloDoBloco(JsonObject bloco, [NotNullWhen(true)] out ModeloDaExigencia? modelo)
    {
        modelo = null;
        if (!PrimitivasDaProjecao.TentarIdentificador(bloco, "modeloId", out Guid modeloId)
            || !PrimitivasDaProjecao.TentarTexto(bloco, "nomeArquivo", out string nomeArquivo)
            || !PrimitivasDaProjecao.TentarTexto(bloco, "formato", out string token)
            || !PrimitivasDaProjecao.TentarTexto(bloco, "hashSha256", out string hash)
            || ModeloDeDocumento.FormatoDoToken(token) is not { } formato)
        {
            return false;
        }

        modelo = new ModeloDaExigencia(modeloId, nomeArquivo, formato, hash);
        return true;
    }
}

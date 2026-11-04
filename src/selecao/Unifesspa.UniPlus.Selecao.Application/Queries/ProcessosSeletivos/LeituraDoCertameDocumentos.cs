namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Leitura, no envelope congelado, dos documentos exigidos, com modelos e formatos, e do atendimento especializado.
/// </summary>
internal static class LeituraDoCertameDocumentos
{
    /// <summary>
    /// Só o que o candidato precisa para reunir documentos. Os blocos irmãos de
    /// <c>documentosExigidos</c> — obrigatoriedades legais, referência temporal dos fatos e os
    /// metadados dos fatos que condicionam cada exigência — não entram. <paramref name="incluir"/>
    /// recorta as exigências projetadas, como as da fase de um formulário; nulo quando a exigência
    /// não tem a forma que o recorte lê, o que recusa a leitura inteira em vez de omiti-la.
    /// <paramref name="acervo"/> dá o endereço do modelo publicado pelo ato da versão lida.
    /// </summary>
    internal static bool TentarExigencias(
        JsonObject envelope,
        Func<JsonObject, bool?> incluir,
        DocumentosDoAtoNoAcervo acervo,
        [NotNullWhen(true)] out List<ExigenciaDocumentalCertameDto>? exigencias)
    {
        exigencias = null;
        if (!PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("documentosExigidos"), out JsonObject? bloco)
            || !PrimitivasDaProjecao.TentarArray(bloco, "exigencias", out JsonArray? array))
        {
            return false;
        }

        List<ExigenciaDocumentalCertameDto> lidas = [];
        foreach (JsonNode? item in array)
        {
            if (!TentarExigencia(item, acervo, out JsonObject? bruta, out ExigenciaDocumentalCertameDto? exigencia)
                || incluir(bruta) is not { } incluida)
            {
                return false;
            }

            if (incluida)
            {
                lidas.Add(exigencia);
            }
        }

        exigencias = lidas;
        return true;
    }

    private static bool TentarExigencia(
        JsonNode? item,
        DocumentosDoAtoNoAcervo acervo,
        [NotNullWhen(true)] out JsonObject? bruta,
        [NotNullWhen(true)] out ExigenciaDocumentalCertameDto? exigencia)
    {
        bruta = null;
        exigencia = null;
        if (item is not JsonObject objeto
            || !PrimitivasDaProjecao.TentarTexto(objeto, "tipoDocumentoNome", out string rotulo)
            || !PrimitivasDaProjecao.TentarTexto(objeto, "aplicabilidade", out string aplicabilidade)
            || !PrimitivasDaProjecao.TentarBooleano(objeto, "obrigatorio", out bool obrigatorio)
            || !TentarFormatos(objeto, out FormatosAceitosCertameDto? formatos)
            || !TentarModelo(objeto, acervo, out ModeloDocumentalCertameDto? modelo))
        {
            return false;
        }

        bruta = objeto;
        exigencia = new ExigenciaDocumentalCertameDto(rotulo, aplicabilidade, obrigatorio, formatos, modelo);
        return true;
    }

    /// <summary>
    /// O modelo que a exigência oferece ao candidato: a chave é obrigatória, nula quando a
    /// exigência não tem modelo. O id do modelo fica fora como campo — é referência interna do
    /// cadastro —, e só aparece dentro do endereço no acervo.
    /// </summary>
    private static bool TentarModelo(JsonObject exigencia, DocumentosDoAtoNoAcervo acervo, out ModeloDocumentalCertameDto? modelo)
    {
        modelo = null;
        if (!TentarModeloCongelado(exigencia, out ModeloDaExigencia? congelado))
        {
            return false;
        }

        if (congelado is not null)
        {
            modelo = new ModeloDocumentalCertameDto(
                congelado.NomeArquivo,
                ModeloDeDocumento.TokenDe(congelado.Formato),
                congelado.HashSha256,
                acervo.DoModelo(congelado));
        }

        return true;
    }

    /// <summary>
    /// Os modelos de documento que o edital congelou, um por modelo, na ordem das exigências — o
    /// que a divulgação publica no acervo. Vazio quando nenhuma exigência oferece modelo.
    /// </summary>
    internal static bool TentarModelosCongelados(
        JsonObject envelope,
        [NotNullWhen(true)] out List<ModeloDaExigencia>? modelos)
    {
        modelos = null;
        if (!PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("documentosExigidos"), out JsonObject? bloco)
            || !PrimitivasDaProjecao.TentarArray(bloco, "exigencias", out JsonArray? array))
        {
            return false;
        }

        List<ModeloDaExigencia> lidos = [];
        foreach (JsonNode? item in array)
        {
            if (item is not JsonObject exigencia || !TentarModeloCongelado(exigencia, out ModeloDaExigencia? modelo))
            {
                return false;
            }

            if (modelo is not null && !lidos.Exists(m => m.ModeloId == modelo.ModeloId))
            {
                lidos.Add(modelo);
            }
        }

        modelos = lidos;
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

        if (node is null)
        {
            return true;
        }

        if (node is not JsonObject bloco
            || !PrimitivasDaProjecao.TentarIdentificador(bloco, "modeloId", out Guid modeloId)
            || !PrimitivasDaProjecao.TentarTexto(bloco, "nomeArquivo", out string nomeArquivo)
            || !PrimitivasDaProjecao.TentarTexto(bloco, "formato", out string token)
            || !PrimitivasDaProjecao.TentarTexto(bloco, "hashSha256", out string hash))
        {
            return false;
        }

        if (ModeloDeDocumento.FormatoDoToken(token) is not { } formato)
        {
            return false;
        }

        modelo = new ModeloDaExigencia(modeloId, nomeArquivo, formato, hash);
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

        // A bicondicional é CONFERIDA, não só documentada: "aceita qualquer formato" com uma lista
        // de formatos ao lado são duas afirmações contraditórias, e repassá-las publicaria a
        // contradição num documento com efeito jurídico.
        if (listaNode is null)
        {
            formatos = qualquer ? new FormatosAceitosCertameDto(true, null) : null;
            return qualquer;
        }

        // Lista VAZIA é a terceira forma contraditória: "não aceita qualquer formato" sem nenhum
        // formato ao lado publicaria uma exigência que nenhum arquivo satisfaz.
        if (qualquer || listaNode is not JsonArray array || array.Count == 0
            || !PrimitivasDaProjecao.TentarItens<string>(array, TentarFormato, out List<string>? lista))
        {
            return false;
        }

        formatos = new FormatosAceitosCertameDto(false, lista);
        return true;
    }

    private static bool TentarFormato(JsonNode? item, [NotNullWhen(true)] out string? formato)
    {
        formato = null;
        return item is JsonObject entrada && PrimitivasDaProjecao.TentarTexto(entrada, "formato", out formato);
    }

    internal static bool TentarAtendimento(JsonObject envelope, [NotNullWhen(true)] out AtendimentoCertameDto? atendimento)
    {
        atendimento = null;
        if (!PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("atendimento"), out JsonObject? bloco)
            || !TentarParesNomeados(bloco, "condicoes", "condicaoCodigo", "condicaoNome", out List<CondicaoAtendimentoCertameDto>? condicoes)
            || !TentarParesNomeados(bloco, "tiposDeficiencia", "tipoDeficienciaCodigo", "tipoDeficienciaNome", out List<CondicaoAtendimentoCertameDto>? tipos)
            || !PrimitivasDaProjecao.TentarArray(bloco, "recursos", out JsonArray? recursosArray))
        {
            return false;
        }

        List<string> recursos = [];
        foreach (JsonNode? item in recursosArray)
        {
            if (item is not JsonObject recurso || !PrimitivasDaProjecao.TentarTexto(recurso, "recursoNome", out string nome))
            {
                return false;
            }

            recursos.Add(nome);
        }

        atendimento = new AtendimentoCertameDto(condicoes, recursos, tipos);
        return true;
    }

    private static bool TentarParesNomeados(
        JsonObject bloco,
        string chaveArray,
        string chaveCodigo,
        string chaveNome,
        [NotNullWhen(true)] out List<CondicaoAtendimentoCertameDto>? pares)
    {
        pares = null;
        if (!PrimitivasDaProjecao.TentarArray(bloco, chaveArray, out JsonArray? array))
        {
            return false;
        }

        List<CondicaoAtendimentoCertameDto> lidos = [];
        foreach (JsonNode? item in array)
        {
            if (item is not JsonObject entrada
                || !PrimitivasDaProjecao.TentarTexto(entrada, chaveCodigo, out string codigo)
                || !PrimitivasDaProjecao.TentarTexto(entrada, chaveNome, out string nome))
            {
                return false;
            }

            lidos.Add(new CondicaoAtendimentoCertameDto(codigo, nome));
        }

        pares = lidos;
        return true;
    }
}

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
/// Leitura, no envelope congelado, dos blocos de identidade do certame: edital e anexos, unidade administradora, tipo do processo, taxa de inscrição e retificação.
/// </summary>
internal static class LeituraDoCertameEdital
{
    /// <summary>Par código/nome, a forma que tipo de processo e tipo de etapa compartilham.</summary>
    internal static bool TentarTipoNomeado(JsonObject? objeto, [NotNullWhen(true)] out TipoCatalogadoCertameDto? tipo)
    {
        tipo = null;
        if (!PrimitivasDaProjecao.TentarTexto(objeto, "codigo", out string codigo) || !PrimitivasDaProjecao.TentarTexto(objeto, "nome", out string nome))
        {
            return false;
        }

        tipo = new TipoCatalogadoCertameDto(codigo, nome);
        return true;
    }

    /// <summary>
    /// O documento do edital que a versão congelou, como o ato o publica no acervo — o que a
    /// divulgação copia e o endereço que o contrato público divulga, numa leitura só.
    /// </summary>
    /// <remarks>
    /// A chave é a da ADR-0132 para o acervo de editais. O processo entra pelo identificador legível
    /// e a unidade pelo slug, ambos congelados na publicação; o slug é chave do caminho, não campo
    /// do contrato. O nome de apresentação usa os mesmos segmentos legíveis, para o arquivo baixado
    /// dizer de qual certame e de qual ato é.
    /// </remarks>
    internal static bool TentarEditalNoAcervo(
        JsonObject envelope,
        AtoDaDivulgacao ato,
        [NotNullWhen(true)] out EditalNoAcervo? edital)
    {
        ArgumentNullException.ThrowIfNull(ato);

        edital = null;
        if (!PrimitivasDaProjecao.TentarTexto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("identificadorLegivel"), out string identificadorLegivel)
            || !PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("identidadesUnidade"), out JsonObject? unidades)
            || !PrimitivasDaProjecao.TentarObjeto(unidades, "administradora", out JsonObject? administradora)
            || !PrimitivasDaProjecao.TentarTexto(administradora, "slug", out string unidade)
            || !PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("hashesEdital"), out JsonObject? hashes)
            || !PrimitivasDaProjecao.TentarIdentificador(hashes, "documentoEditalId", out Guid documentoEditalId)
            || !PrimitivasDaProjecao.TentarTexto(hashes, "hashSha256", out string hashSha256)
            || string.IsNullOrWhiteSpace(unidade)
            || string.IsNullOrWhiteSpace(hashSha256)
            || string.IsNullOrWhiteSpace(ato.TipoCodigo))
        {
            return false;
        }

        string chave = ChaveNoAcervoPublico.DoEdital(ato.Ano, unidade, ato.TipoCodigo, identificadorLegivel, ato.Id, hashSha256, FormatoDeDocumento.Pdf);
        string nomeDeApresentacao = $"{identificadorLegivel}-{ChaveNoAcervoPublico.SegmentoDoTipo(ato.TipoCodigo)}.pdf";
        edital = new EditalNoAcervo(documentoEditalId, hashSha256, chave, nomeDeApresentacao);
        return true;
    }

    /// <summary>
    /// Os anexos do edital como o envelope da versão os congelou, na ordem em que o edital os
    /// apresenta — o que a divulgação publica no acervo. Vazio quando o edital não tem anexo.
    /// </summary>
    internal static bool TentarAnexosCongelados(
        JsonObject envelope,
        [NotNullWhen(true)] out List<AnexoDoEdital>? anexos)
    {
        anexos = null;
        if (!PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("hashesEdital"), out JsonObject? hashes)
            || !PrimitivasDaProjecao.TentarArray(hashes, "anexos", out JsonArray? array))
        {
            return false;
        }

        List<AnexoDoEdital> lidos = [];
        foreach (JsonNode? item in array)
        {
            if (item is not JsonObject anexo
                || !PrimitivasDaProjecao.TentarIdentificador(anexo, "anexoId", out Guid anexoId)
                || !PrimitivasDaProjecao.TentarIdentificador(anexo, "documentoId", out Guid documentoId)
                || !PrimitivasDaProjecao.TentarTexto(anexo, "nomeArquivo", out string nomeArquivo)
                || !PrimitivasDaProjecao.TentarTexto(anexo, "tipoDeMidia", out string tipoDeMidia)
                || !PrimitivasDaProjecao.TentarInteiro(anexo, "tamanhoBytes", out int tamanhoBytes)
                || !PrimitivasDaProjecao.TentarTexto(anexo, "hashSha256", out string hashSha256)
                || FormatosDeDocumento.DoTipoDeMidia(tipoDeMidia) is not { } formato)
            {
                return false;
            }

            lidos.Add(new AnexoDoEdital(anexoId, documentoId, nomeArquivo, formato, tamanhoBytes, hashSha256));
        }

        anexos = lidos;
        return true;
    }

    /// <summary>
    /// Os anexos que a versão congelou, para a versão seguinte. Quando o pedido não traz a lista
    /// (<paramref name="herdaOsDaVersao"/>), são eles que a versão nova herda, e um envelope que não se
    /// deixa ler é defeito — a versão publicada já foi lida pelo codec —, não estado do processo.
    /// Quando o pedido traz a própria lista, os da versão servem só para manter a identidade do
    /// anexo que continua o mesmo, e a leitura que falha apenas deixa de manter a identidade.
    /// </summary>
    internal static IReadOnlyList<AnexoDoEdital> AnexosDaVersao(VersaoConfiguracao versao, bool herdaOsDaVersao)
    {
        ArgumentNullException.ThrowIfNull(versao);

        if (TentarAnexosDaVersao(versao, out List<AnexoDoEdital>? anexos))
        {
            return anexos;
        }

        return herdaOsDaVersao
            ? throw new InvalidOperationException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não tem a forma esperada nos anexos do edital.")
            : [];
    }

    private static bool TentarAnexosDaVersao(VersaoConfiguracao versao, [NotNullWhen(true)] out List<AnexoDoEdital>? anexos)
    {
        anexos = null;
        try
        {
            return JsonNode.Parse(versao.ConfiguracaoCongelada) is JsonObject envelope
                && TentarAnexosCongelados(envelope, out anexos);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static bool TentarUnidadeAdministradora(
        JsonObject envelope,
        [NotNullWhen(true)] out UnidadeAdministradoraCertameDto? unidade)
    {
        unidade = null;
        if (!PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("identidadesUnidade"), out JsonObject? bloco)
            || !PrimitivasDaProjecao.TentarObjeto(bloco, "administradora", out JsonObject? administradora)
            || !PrimitivasDaProjecao.TentarTexto(administradora, "sigla", out string sigla)
            || !PrimitivasDaProjecao.TentarTexto(administradora, "nome", out string nome)
            || !PrimitivasDaProjecao.TentarTexto(administradora, "tipo", out string tipo)
            || !PrimitivasDaProjecao.TentarTextoOpcional(administradora, "cidadeNome", out string? cidadeNome)
            || !PrimitivasDaProjecao.TentarTextoOpcional(administradora, "cidadeUf", out string? cidadeUf))
        {
            return false;
        }

        // O identificador de origem e o slug ficam fora: são chave de integração com o cadastro
        // institucional, não informação que o edital comunica ao candidato.
        unidade = new UnidadeAdministradoraCertameDto(sigla, nome, tipo, cidadeNome, cidadeUf);
        return true;
    }

    /// <summary>
    /// Bloco de presença explícita: <c>presente: false</c> significa publicação sem taxa
    /// configurada, e vira ausência do bloco na resposta pública — não um objeto com campos nulos,
    /// que o candidato leria como "taxa de valor desconhecido".
    /// </summary>
    internal static bool TentarTaxaInscricao(JsonObject envelope, out TaxaInscricaoCertameDto? taxa)
    {
        taxa = null;
        if (!PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("taxaInscricao"), out JsonObject? bloco)
            || !PrimitivasDaProjecao.TentarBooleano(bloco, "presente", out bool presente))
        {
            return false;
        }

        if (!presente)
        {
            return true;
        }

        if (!PrimitivasDaProjecao.TentarBooleano(bloco, "cobra", out bool cobra)
            || !PrimitivasDaProjecao.TentarTextoOpcional(bloco, "valor", out string? valor)
            || !PrimitivasDaProjecao.TentarTextos(bloco, "fundamentos", out List<string>? fundamentos))
        {
            return false;
        }

        taxa = new TaxaInscricaoCertameDto(cobra, valor, fundamentos);
        return true;
    }

    /// <summary>
    /// Bloco condicional: ausente na publicação original, presente quando o edital foi emendado.
    /// Ausência é sucesso com <see langword="null"/>; presença malformada é falha.
    /// </summary>
    internal static bool TentarRetificacao(JsonObject envelope, out RetificacaoCertameDto? retificacao)
    {
        retificacao = null;
        if (!envelope.TryGetPropertyValue(ProjecaoDoCertamePublicado.BlocoPublico("retificacao"), out JsonNode? node) || node is null)
        {
            return true;
        }

        if (node is not JsonObject bloco
            || !PrimitivasDaProjecao.TentarIdentificador(bloco, "editalRetificadoId", out Guid atoRetificadoId)
            || !PrimitivasDaProjecao.TentarTexto(bloco, "motivo", out string motivo))
        {
            return false;
        }

        retificacao = new RetificacaoCertameDto(atoRetificadoId, motivo);
        return true;
    }
}

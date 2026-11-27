namespace Unifesspa.UniPlus.Portal.Infrastructure.Origens;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Portal.Application.Certames;

/// <summary>
/// Os campos do detalhe público do certame (Seleção) que a Portal lê. Campos que a origem envia
/// e não estão aqui são ignorados na leitura.
/// </summary>
internal sealed record CertamePublicadoPayload(
    Guid ProcessoSeletivoId,
    Guid AtoCriadorId,
    string? Nome,
    PeriodoDoCertamePayload? Periodo,
    DocumentoEditalDoCertamePayload? DocumentoEdital,
    SituacaoDoCertamePayload? Situacao,
    IReadOnlyList<EditalPublicadoDoCertamePayload?>? EditaisPublicados = null);

/// <summary>
/// A situação como Seleção a devolve, com o código e o rótulo. A Portal lê só o código, e um código que ela
/// não conhece não vira situação: o certame fica indisponível, e não com uma situação inventada.
/// </summary>
internal sealed record SituacaoDoCertamePayload(string? Codigo)
{
    public SituacaoDaInscricao? Valor => Codigo switch
    {
        "EM_BREVE" => SituacaoDaInscricao.EmBreve,
        "INSCRICOES_ABERTAS" => SituacaoDaInscricao.InscricoesAbertas,
        "ULTIMOS_DIAS" => SituacaoDaInscricao.UltimosDias,
        "ENCERRADAS" => SituacaoDaInscricao.Encerradas,
        _ => null,
    };
}

/// <summary>A parte do período de inscrição que a Portal usa: o número do edital.</summary>
internal sealed record PeriodoDoCertamePayload(string? Numero);

/// <summary>A parte do documento do edital que a Portal usa: o endereço público no acervo.</summary>
internal sealed record DocumentoEditalDoCertamePayload(Uri? UrlDownload);

/// <summary>
/// O edital de uma versão divulgada: o ato que a criou e os documentos dele no acervo. Origem
/// anterior ao campo não o envia. Edital nulo é descartado na leitura; edital sem a lista de
/// documentos, que o contrato exige, deixa a origem indisponível.
/// </summary>
internal sealed record EditalPublicadoDoCertamePayload(
    Guid AtoId,
    IReadOnlyList<DocumentoDoAtoPayload?>? Documentos = null);

/// <summary>
/// Um documento do ato no acervo. O papel chega como texto (<c>principal</c> ou <c>anexo</c>): um
/// papel que a Portal não conhece descarta o documento, e não a página.
/// </summary>
internal sealed record DocumentoDoAtoPayload(
    string? Papel,
    string? Nome,
    string? TipoDeMidia,
    long? TamanhoBytes,
    Uri? UrlDownload);

/// <summary>
/// Os campos de um ato da consulta unificada (Publicações) que a Portal lê. O assinante não é
/// declarado de propósito: dado pessoal que a página pública não usa não entra na Portal. Ano, data e
/// indicador de congelamento são anuláveis para que a ausência fique visível: o contrato os exige, e o
/// valor padrão do tipo (zero, data mínima, falso) nunca pode passar por valor recebido. O ano só
/// vale como número: o padrão do leitor aceitaria o texto <c>"2026"</c>, que o contrato não admite.
/// </summary>
internal sealed record AtoNormativoPayload(
    Guid Id,
    string? TipoCodigo,
    string? TipoNome,
    [property: JsonNumberHandling(JsonNumberHandling.Strict)] int? Ano,
    string? Numero,
    DateOnly? DataDoAto,
    bool? CongelaConfiguracao,
    string? MotivoRetificacao);

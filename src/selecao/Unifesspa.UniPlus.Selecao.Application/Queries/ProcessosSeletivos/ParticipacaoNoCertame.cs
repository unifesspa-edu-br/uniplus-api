namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json.Nodes;

using Domain.Enums;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Results;

using CronogramaDeFases = (Unifesspa.UniPlus.Application.Abstractions.Respostas.CodigoComRotulo<Unifesspa.UniPlus.Selecao.Domain.Enums.OrigemCandidatos> OrigemCandidatos, List<DTOs.FaseCronogramaCertameDto> Fases);

/// <summary>
/// O que o candidato precisa saber para participar: as etapas, a origem dos candidatos e as fases do
/// cronograma, as exigências documentais, o atendimento especializado e a taxa de inscrição, como o
/// envelope congelado os guarda.
/// </summary>
/// <param name="Taxa">Nulo quando a publicação não configurou taxa de inscrição.</param>
internal sealed record ParticipacaoNoCertame(
    List<EtapaCertameDto> Etapas,
    CodigoComRotulo<OrigemCandidatos> OrigemCandidatos,
    List<FaseCronogramaCertameDto> Fases,
    List<ExigenciaDocumentalCertameDto> Exigencias,
    AtendimentoCertameDto Atendimento,
    TaxaInscricaoCertameDto? Taxa)
{
    /// <summary>Lê a participação do envelope, parando na primeira parte que não se deixa ler.</summary>
    internal static Result<ParticipacaoNoCertame> Ler(JsonObject envelope, DocumentosDoAtoNoAcervo acervo) =>
        LerEtapas(envelope).Encadear(etapas =>
            LerCronograma(envelope).Encadear(cronograma =>
            LerExigencias(envelope, acervo).Encadear(exigencias =>
            LerAtendimento(envelope).Encadear(atendimento =>
            LerTaxa(envelope).Compor(taxa => new ParticipacaoNoCertame(
                etapas, cronograma.OrigemCandidatos, cronograma.Fases, exigencias, atendimento, taxa))))));

    private static Result<List<EtapaCertameDto>> LerEtapas(JsonObject envelope) =>
        LeituraDoCertameCronograma.TentarEtapas(envelope, out List<EtapaCertameDto>? etapas)
            ? Result<List<EtapaCertameDto>>.Success(etapas)
            : ProjecaoDoCertamePublicado.Recusar<List<EtapaCertameDto>>("etapas");

    private static Result<CronogramaDeFases> LerCronograma(JsonObject envelope) =>
        LeituraDoCertameCronograma.TentarCronograma(envelope, out CodigoComRotulo<OrigemCandidatos>? origemCandidatos, out List<FaseCronogramaCertameDto>? fases)
            ? Result<CronogramaDeFases>.Success((origemCandidatos, fases))
            : ProjecaoDoCertamePublicado.Recusar<CronogramaDeFases>("cronograma");

    private static Result<List<ExigenciaDocumentalCertameDto>> LerExigencias(JsonObject envelope, DocumentosDoAtoNoAcervo acervo) =>
        CronogramaCongelado.TentarDoEnvelope(envelope, out CronogramaCongelado? cronograma)
        && LeituraDoCertameDocumentos.TentarExigencias(envelope, static _ => true, acervo, cronograma, out List<ExigenciaDocumentalCertameDto>? exigencias)
            ? Result<List<ExigenciaDocumentalCertameDto>>.Success(exigencias)
            : ProjecaoDoCertamePublicado.Recusar<List<ExigenciaDocumentalCertameDto>>("documentos exigidos");

    private static Result<AtendimentoCertameDto> LerAtendimento(JsonObject envelope) =>
        LeituraDoAtendimentoDoCertame.TentarAtendimento(envelope, out AtendimentoCertameDto? atendimento)
            ? Result<AtendimentoCertameDto>.Success(atendimento)
            : ProjecaoDoCertamePublicado.Recusar<AtendimentoCertameDto>("atendimento especializado");

    private static Result<TaxaInscricaoCertameDto?> LerTaxa(JsonObject envelope) =>
        LeituraDoCertameEdital.TentarTaxaInscricao(envelope, out TaxaInscricaoCertameDto? taxa)
            ? Result<TaxaInscricaoCertameDto?>.Success(taxa)
            : ProjecaoDoCertamePublicado.Recusar<TaxaInscricaoCertameDto?>("taxa de inscrição");
}

namespace Unifesspa.UniPlus.Selecao.Application.DTOs.NotasDoEnem;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Uma importação do retorno do INEP, com os totais e, na consulta de um lote, o que não virou nota.</summary>
/// <param name="Vigente">Verdadeiro só no lote mais recente da edição: é o único cujas notas valem.</param>
/// <param name="Ocorrencias">Nula na listagem; na consulta e na importação, tudo o que não virou nota.</param>
public sealed record LoteDeNotasDoEnemDto(
    Guid Id,
    string Edicao,
    string LeiauteCodigo,
    string LeiauteVersao,
    string LeiauteHash,
    string ArquivoHash,
    string ImportadoPor,
    DateTimeOffset ImportadoEm,
    int TotalDeLinhas,
    int TotalDeInscritosComNota,
    int TotalDeLancamentos,
    int TotalDeInscritosSemLinha,
    int TotalDeLinhasSemInscrito,
    int TotalDeInscritosEmConflito,
    bool Vigente,
    IReadOnlyList<OcorrenciaDoLoteDto>? Ocorrencias)
{
    public static LoteDeNotasDoEnemDto De(
        LoteDeNotasDoEnem lote,
        bool vigente,
        IReadOnlyDictionary<Guid, string>? numerosDeInscricao)
    {
        ArgumentNullException.ThrowIfNull(lote);

        return new LoteDeNotasDoEnemDto(
            lote.Id,
            lote.EdicaoCodigo,
            lote.LeiauteCodigo,
            lote.LeiauteVersao,
            lote.LeiauteHash,
            lote.ArquivoHash,
            lote.ImportadoPor,
            lote.ImportadoEm,
            lote.TotalDeLinhas,
            lote.TotalDeInscritosComNota,
            lote.TotalDeLancamentos,
            lote.TotalDeInscritosSemLinha,
            lote.TotalDeLinhasSemInscrito,
            lote.TotalDeInscritosEmConflito,
            vigente,
            numerosDeInscricao is null
                ? null
                : [.. lote.Ocorrencias
                    .Select(o => OcorrenciaDoLoteDto.De(o, numerosDeInscricao))
                    .OrderBy(static o => o.Tipo, StringComparer.Ordinal)
                    .ThenBy(static o => o.NumeroDeInscricao, StringComparer.Ordinal)
                    .ThenBy(static o => o.NumerosDasLinhas?[0])]);
    }
}

/// <summary>
/// O que, no arquivo ou nas inscrições, não virou nota. Nunca traz CPF: o inscrito é identificado
/// pelo número de inscrição do Uni+, e a linha de quem não é inscrito só pelo número da linha.
/// </summary>
public sealed record OcorrenciaDoLoteDto(
    string Tipo,
    Guid? InscricaoId,
    string? NumeroDeInscricao,
    IReadOnlyList<int>? NumerosDasLinhas)
{
    public static OcorrenciaDoLoteDto De(OcorrenciaDoLoteDeNotas ocorrencia, IReadOnlyDictionary<Guid, string> numerosDeInscricao)
    {
        ArgumentNullException.ThrowIfNull(ocorrencia);
        ArgumentNullException.ThrowIfNull(numerosDeInscricao);

        string? numero = ocorrencia.InscricaoId is { } id && numerosDeInscricao.TryGetValue(id, out string? encontrado) ? encontrado : null;

        return new OcorrenciaDoLoteDto(
            ocorrencia.Tipo switch
            {
                TipoDeOcorrenciaDoLote.InscritoSemLinha => "INSCRITO_SEM_LINHA",
                TipoDeOcorrenciaDoLote.LinhaSemInscrito => "LINHA_SEM_INSCRITO",
                _ => "CONFLITO",
            },
            ocorrencia.InscricaoId,
            numero,
            ocorrencia.NumerosDasLinhas);
    }
}

/// <summary>O resultado de importar um arquivo: o lote e se ele foi criado agora ou já era o vigente.</summary>
public sealed record ImportacaoDeNotasDto(LoteDeNotasDoEnemDto Lote, bool Criado);

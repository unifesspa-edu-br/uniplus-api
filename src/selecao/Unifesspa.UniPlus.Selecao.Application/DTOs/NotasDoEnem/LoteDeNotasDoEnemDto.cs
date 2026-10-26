namespace Unifesspa.UniPlus.Selecao.Application.DTOs.NotasDoEnem;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Uma importação do retorno do INEP, com os totais e, na consulta de um lote, o que não virou nota.</summary>
/// <param name="Vigente">Verdadeiro só no lote mais recente da edição: é o único cujas notas valem.</param>
/// <param name="Ocorrencias">Nula na listagem; na consulta e na importação, tudo o que não virou nota.</param>
/// <param name="TotalDeInscritosAusentes">Inscritos com ao menos uma área em que faltaram à prova.</param>
/// <param name="Ausentes">Nula na listagem; na consulta e na importação, quem faltou e em quais áreas.</param>
public sealed record LoteDeNotasDoEnemDto(
    Guid Id,
    string Edicao,
    string LeiauteCodigo,
    string LeiauteVersao,
    string LeiauteHash,
    string ArquivoHash,
    string ImportadoPor,
    string? ImportadoPorNome,
    DateTimeOffset ImportadoEm,
    int TotalDeLinhas,
    int TotalDeInscritosComNota,
    int TotalDeLancamentos,
    int TotalDeInscritosSemLinha,
    int TotalDeLinhasSemInscrito,
    int TotalDeInscritosEmConflito,
    bool Vigente,
    IReadOnlyList<OcorrenciaDoLoteDto>? Ocorrencias,
    int TotalDeInscritosAusentes,
    IReadOnlyList<AusenteDoLoteDto>? Ausentes)
{
    public static LoteDeNotasDoEnemDto De(
        LoteDeNotasDoEnem lote,
        bool vigente,
        DetalheDoLoteDeNotas? detalhe,
        int totalDeInscritosAusentes)
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
            lote.ImportadoPorNome,
            lote.ImportadoEm,
            lote.TotalDeLinhas,
            lote.TotalDeInscritosComNota,
            lote.TotalDeLancamentos,
            lote.TotalDeInscritosSemLinha,
            lote.TotalDeLinhasSemInscrito,
            lote.TotalDeInscritosEmConflito,
            vigente,
            detalhe is null
                ? null
                : [.. lote.Ocorrencias
                    .Select(o => OcorrenciaDoLoteDto.De(o, detalhe))
                    .OrderBy(static o => o.Tipo, StringComparer.Ordinal)
                    .ThenBy(static o => o.NumeroDeInscricao, StringComparer.Ordinal)
                    .ThenBy(static o => o.NumerosDasLinhas?[0])],
            totalDeInscritosAusentes,
            detalhe is null
                ? null
                : [.. detalhe.AusentesPorInscricao
                    .Select(a => AusenteDoLoteDto.De(a.Key, a.Value, detalhe))
                    .OrderBy(static a => a.NumeroDeInscricao, StringComparer.Ordinal)]);
    }
}

/// <summary>
/// O que, no arquivo ou nas inscrições, não virou nota. O inscrito é identificado pelo número de
/// inscrição, pelo nome e pelo CPF mascarado; a linha de quem não é inscrito, pelo número da linha e
/// pelo CPF mascarado, pelo nome e pelo sexo (<c>F</c> ou <c>M</c>) que o arquivo trouxe, gravados
/// na importação, sem inscrição.
/// </summary>
public sealed record OcorrenciaDoLoteDto(
    string Tipo,
    Guid? InscricaoId,
    string? NumeroDeInscricao,
    IReadOnlyList<int>? NumerosDasLinhas,
    string? NomeDoCandidato,
    string? CpfMascarado,
    string? Sexo,
    IReadOnlyList<OpcaoDeCursoDoLoteDto>? OpcoesDeCurso)
{
    public static OcorrenciaDoLoteDto De(OcorrenciaDoLoteDeNotas ocorrencia, DetalheDoLoteDeNotas detalhe)
    {
        ArgumentNullException.ThrowIfNull(ocorrencia);
        ArgumentNullException.ThrowIfNull(detalhe);

        Guid? inscricaoId = ocorrencia.InscricaoId;
        IdentificacaoDoInscrito? identificacao = inscricaoId is { } id ? detalhe.Identificar(id) : null;

        return new OcorrenciaDoLoteDto(
            ocorrencia.Tipo switch
            {
                TipoDeOcorrenciaDoLote.InscritoSemLinha => "INSCRITO_SEM_LINHA",
                TipoDeOcorrenciaDoLote.LinhaSemInscrito => "LINHA_SEM_INSCRITO",
                _ => "CONFLITO",
            },
            inscricaoId,
            inscricaoId is { } n ? detalhe.NumeroDe(n) : null,
            ocorrencia.NumerosDasLinhas,
            identificacao?.Nome ?? ocorrencia.Nome,
            identificacao?.CpfMascarado ?? ocorrencia.CpfMascarado,
            ocorrencia.Sexo,
            inscricaoId is { } o ? detalhe.OpcoesDe(o) : null);
    }
}

/// <summary>Um inscrito que faltou à prova de ao menos uma área, com os códigos das áreas, na ordem das áreas.</summary>
public sealed record AusenteDoLoteDto(
    Guid InscricaoId,
    string NumeroDeInscricao,
    string? NomeDoCandidato,
    string? CpfMascarado,
    IReadOnlyList<string> Areas,
    IReadOnlyList<OpcaoDeCursoDoLoteDto>? OpcoesDeCurso)
{
    public static AusenteDoLoteDto De(Guid inscricaoId, IReadOnlyList<string> areas, DetalheDoLoteDeNotas detalhe)
    {
        ArgumentNullException.ThrowIfNull(areas);
        ArgumentNullException.ThrowIfNull(detalhe);

        IdentificacaoDoInscrito? identificacao = detalhe.Identificar(inscricaoId);
        return new AusenteDoLoteDto(
            inscricaoId, detalhe.NumeroDe(inscricaoId) ?? string.Empty, identificacao?.Nome, identificacao?.CpfMascarado, areas, detalhe.OpcoesDe(inscricaoId));
    }
}

/// <summary>Um curso escolhido pelo inscrito: a ordem da opção (1ª ou 2ª) e a oferta como o edital a nomeou.</summary>
public sealed record OpcaoDeCursoDoLoteDto(int Ordem, string Oferta);

/// <summary>O que se lê por inscrição para legendar um lote: número, identificação e ausências.</summary>
public sealed record DetalheDoLoteDeNotas(
    IReadOnlyDictionary<Guid, string> NumerosDeInscricao,
    IReadOnlyDictionary<Guid, IdentificacaoDoInscrito> Identificacoes,
    IReadOnlyDictionary<Guid, IReadOnlyList<string>> AusentesPorInscricao,
    IReadOnlyDictionary<Guid, IReadOnlyList<OpcaoDeCursoDoLoteDto>> OpcoesDeCursoPorInscricao)
{
    public IReadOnlyList<OpcaoDeCursoDoLoteDto>? OpcoesDe(Guid inscricaoId) => OpcoesDeCursoPorInscricao.GetValueOrDefault(inscricaoId);

    public string? NumeroDe(Guid inscricaoId) => NumerosDeInscricao.GetValueOrDefault(inscricaoId);

    public IdentificacaoDoInscrito? Identificar(Guid inscricaoId) => Identificacoes.GetValueOrDefault(inscricaoId);
}

/// <summary>O resultado de importar um arquivo: o lote e se ele foi criado agora ou já era o vigente.</summary>
public sealed record ImportacaoDeNotasDto(LoteDeNotasDoEnemDto Lote, bool Criado);

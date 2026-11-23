namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

public sealed record DataDeReferenciaDoDocumento
{
    private DataDeReferenciaDoDocumento(TipoDaDataDeReferencia tipo, DateOnly? data, Guid? faseId)
    {
        Tipo = tipo;
        Data = data;
        FaseId = faseId;
    }

    public TipoDaDataDeReferencia Tipo { get; }
    public DateOnly? Data { get; }
    public Guid? FaseId { get; }

    public static Result<DataDeReferenciaDoDocumento> Criar(TipoDaDataDeReferencia tipo, DateOnly? data, Guid? faseId)
    {
        if (!Enum.IsDefined(tipo))
            return Falha("tipo", DataDeReferenciaDoDocumentoErrorCodes.TipoInvalido, "O tipo da data de referência não é reconhecido.");
        List<FieldError> erros = [.. ConferirData(tipo, data), .. ConferirFase(tipo, faseId)];
        return erros.Count > 0
            ? Result<DataDeReferenciaDoDocumento>.ValidationFailure(erros)
            : Result<DataDeReferenciaDoDocumento>.Success(new(tipo, data, faseId));
    }

    private static IEnumerable<FieldError> ConferirData(TipoDaDataDeReferencia tipo, DateOnly? data) =>
        (tipo == TipoDaDataDeReferencia.DataEspecifica) == data.HasValue ? []
            : [Recusa("data", DataDeReferenciaDoDocumentoErrorCodes.DataIncoerenteComTipo, "Informe a data somente para uma data específica.")];

    private static IEnumerable<FieldError> ConferirFase(TipoDaDataDeReferencia tipo, Guid? faseId)
    {
        bool usaFase = tipo is TipoDaDataDeReferencia.InicioDaFase or TipoDaDataDeReferencia.FimDaFase;
        return usaFase == faseId.HasValue && faseId != Guid.Empty ? []
            : [Recusa("faseId", DataDeReferenciaDoDocumentoErrorCodes.FaseIncoerenteComTipo, "Informe a fase somente para o início ou o fim de uma fase.")];
    }

    public bool UsaAFase(Guid id) => FaseId == id;

    public DateOnly? DataNoCronograma(IEnumerable<IPeriodoDaFase> fases, TimeZoneInfo fuso)
    {
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(fuso);
        DateTimeOffset? instante = Tipo switch
        {
            TipoDaDataDeReferencia.FimDasInscricoes => fases.Where(static fase => fase.ColetaInscricao).Max(static fase => fase.Fim),
            TipoDaDataDeReferencia.InicioDaFase => fases.FirstOrDefault(fase => fase.Id == FaseId)?.Inicio,
            TipoDaDataDeReferencia.FimDaFase => fases.FirstOrDefault(fase => fase.Id == FaseId)?.Fim,
            _ => null,
        };
        return Data ?? (instante is null ? null : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante.Value, fuso).DateTime));
    }

    private static Result<DataDeReferenciaDoDocumento> Falha(string campo, string codigo, string mensagem) =>
        Result<DataDeReferenciaDoDocumento>.ValidationFailure([Recusa(campo, codigo, mensagem)]);

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}

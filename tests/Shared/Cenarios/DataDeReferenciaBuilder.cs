namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A data de referência do prazo de emissão ou da validade; sem dizer nada, o fim das inscrições.</summary>
internal sealed class DataDeReferenciaBuilder
{
    private TipoDaDataDeReferencia _tipo = TipoDaDataDeReferencia.FimDasInscricoes;
    private DateOnly? _data;
    private Guid? _faseId;

    public DataDeReferenciaBuilder NoInicioDaFase(Guid faseId) { _tipo = TipoDaDataDeReferencia.InicioDaFase; _faseId = faseId; return this; }
    public DataDeReferenciaBuilder NoFimDaFase(Guid faseId) { _tipo = TipoDaDataDeReferencia.FimDaFase; _faseId = faseId; return this; }
    public DataDeReferenciaBuilder NaData(DateOnly data) { _tipo = TipoDaDataDeReferencia.DataEspecifica; _data = data; return this; }
    public DataDeReferenciaBuilder NaApresentacao() { _tipo = TipoDaDataDeReferencia.DataDaApresentacao; return this; }

    public DataDeReferenciaDoDocumento Construir()
    {
        Result<DataDeReferenciaDoDocumento> referencia = DataDeReferenciaDoDocumento.Criar(_tipo, _data, _faseId);
        return referencia.IsSuccess ? referencia.Value! : throw new InvalidOperationException(referencia.Error!.Message);
    }
}

namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// A data a partir da qual se conta o prazo de emissão ou se confere a validade do documento do modelo: o
/// tipo, a data (só para a data específica) e a fase pelo código (só para o início ou o fim de uma fase),
/// que a aplicação do modelo troca pela fase do cronograma do processo. Que a data e a fase combinem com o
/// tipo quem confere é o processo, ao receber a cópia.
/// </summary>
public sealed record DataDeReferenciaDoDocumentoDoModelo(TipoDaDataDeReferencia Tipo, string? FaseCodigo, DateOnly? Data)
{
    /// <summary>A referência preservada como recebida.</summary>
    public DataDeReferenciaDoDocumentoDoModelo NaFormaGravada() =>
        this;
}

/// <summary>
/// O prazo de emissão que o modelo escolhe para o documento: com ou sem prazo e, com prazo, o valor, a
/// unidade, a data de referência e o efeito do documento emitido fora do prazo. O que combina com a escolha
/// quem confere é o processo, ao receber a cópia.
/// </summary>
public sealed record PrazoDeEmissaoDoModelo(
    EscolhaDoPrazoDeEmissao Escolha,
    int? Valor,
    UnidadeDoPrazoDeEmissao? Unidade,
    DataDeReferenciaDoDocumentoDoModelo? Referencia,
    EfeitoNaAnaliseDoDocumento? EfeitoForaDoPrazo)
{
    /// <summary>O prazo com o código da fase da referência na forma gravada.</summary>
    public PrazoDeEmissaoDoModelo NaFormaGravada() => this with { Referencia = Referencia?.NaFormaGravada() };
}

/// <summary>
/// A validade que o modelo escolhe para o documento: exige ou não documento válido e, exigindo, a data de
/// referência e o efeito do documento vencido. O que combina com a escolha quem confere é o processo, ao
/// receber a cópia.
/// </summary>
public sealed record ValidadeDoDocumentoDoModelo(
    EscolhaDaValidade Escolha,
    DataDeReferenciaDoDocumentoDoModelo? Referencia,
    EfeitoNaAnaliseDoDocumento? EfeitoDoVencimento)
{
    /// <summary>A validade com o código da fase da referência na forma gravada.</summary>
    public ValidadeDoDocumentoDoModelo NaFormaGravada() => this with { Referencia = Referencia?.NaFormaGravada() };
}

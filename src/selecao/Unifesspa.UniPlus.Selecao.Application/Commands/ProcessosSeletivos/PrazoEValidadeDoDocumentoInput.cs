namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// A data a partir da qual se conta o prazo de emissão ou se confere a validade do documento.
/// </summary>
/// <param name="Tipo">Código do tipo da data de referência.</param>
/// <param name="Data">A data, só quando o tipo é <c>DATA_ESPECIFICA</c>.</param>
/// <param name="FaseId">A fase do cronograma, só quando o tipo é <c>INICIO_DA_FASE</c> ou <c>FIM_DA_FASE</c>.</param>
public sealed record DataDeReferenciaDoDocumentoInput(
    [AceitaCodigosDe<TipoDaDataDeReferencia>] string? Tipo, DateOnly? Data, Guid? FaseId);

/// <summary>
/// O prazo de emissão do documento. Ausente, a escolha ainda não foi feita; o domínio confere que, com prazo,
/// o valor, a unidade, a referência e o efeito vêm juntos, e que, sem prazo, nenhum deles vem.
/// </summary>
/// <param name="Escolha">Código da escolha: com prazo ou sem prazo.</param>
/// <param name="Valor">Quantidade de dias, meses ou anos, maior que zero.</param>
/// <param name="Unidade">Código da unidade do prazo.</param>
/// <param name="Referencia">A data de referência da contagem.</param>
/// <param name="EfeitoForaDoPrazo">Código do efeito na análise do documento emitido fora do prazo.</param>
public sealed record PrazoDeEmissaoInput(
    [AceitaCodigosDe<EscolhaDoPrazoDeEmissao>] string? Escolha,
    int? Valor,
    [AceitaCodigosDe<UnidadeDoPrazoDeEmissao>] string? Unidade,
    DataDeReferenciaDoDocumentoInput? Referencia,
    [AceitaCodigosDe<EfeitoNaAnaliseDoDocumento>] string? EfeitoForaDoPrazo);

/// <summary>
/// A validade do documento. Ausente, a escolha ainda não foi feita; o domínio confere que, exigindo documento
/// válido, a referência e o efeito vêm juntos, e que, sem a exigência, nenhum deles vem.
/// </summary>
/// <param name="Escolha">Código da escolha: exige ou não exige documento válido.</param>
/// <param name="Referencia">A data de referência da conferência.</param>
/// <param name="EfeitoDoVencimento">Código do efeito na análise do documento vencido.</param>
public sealed record ValidadeDoDocumentoInput(
    [AceitaCodigosDe<EscolhaDaValidade>] string? Escolha,
    DataDeReferenciaDoDocumentoInput? Referencia,
    [AceitaCodigosDe<EfeitoNaAnaliseDoDocumento>] string? EfeitoDoVencimento);

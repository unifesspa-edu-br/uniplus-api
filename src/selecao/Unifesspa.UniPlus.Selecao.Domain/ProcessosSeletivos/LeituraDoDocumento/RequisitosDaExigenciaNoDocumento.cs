namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Uma base legal da exigência ou da regra de comprovação, com a posição na lista enviada: a norma, a abrangência, a situação e a observação.</summary>
public sealed record BaseLegalNoDocumento(
    int PosicaoNoDocumento,
    string? Referencia,
    TipoAbrangencia? Abrangencia,
    StatusBaseLegal? Situacao,
    string? Observacao);

/// <summary>Os formatos aceitos: qualquer um ou uma lista, e o tamanho máximo de cada formato da lista.</summary>
public sealed record FormatosPermitidosNoDocumento(bool Qualquer, IReadOnlyList<FormatoPermitidoNoDocumento> Lista);

/// <summary>Um formato da lista, com a posição que tinha na lista enviada e o tamanho máximo dele em bytes, quando declarado.</summary>
public sealed record FormatoPermitidoNoDocumento(int PosicaoNoDocumento, FormatoPermitido? Formato, int? TamanhoMaximoBytesMax);

/// <summary>O prazo de emissão do documento como o documento da configuração o traz.</summary>
public sealed record PrazoDeEmissaoNoDocumento(
    EscolhaDoPrazoDeEmissao? Escolha,
    int? Valor,
    UnidadeDoPrazoDeEmissao? Unidade,
    DataDeReferenciaNoDocumento? Referencia,
    EfeitoNaAnaliseDoDocumento? EfeitoForaDoPrazo);

/// <summary>A exigência de validade do documento como o documento da configuração a traz.</summary>
public sealed record ValidadeNoDocumento(
    EscolhaDaValidade? Escolha,
    DataDeReferenciaNoDocumento? Referencia,
    EfeitoNaAnaliseDoDocumento? EfeitoDoVencimento);

/// <summary>A data de que o prazo ou a validade parte: o tipo, a data específica e a fase pela chave, como foram escritos.</summary>
public sealed record DataDeReferenciaNoDocumento(TipoDaDataDeReferencia? Tipo, DateOnly? Data, string? FaseChave);

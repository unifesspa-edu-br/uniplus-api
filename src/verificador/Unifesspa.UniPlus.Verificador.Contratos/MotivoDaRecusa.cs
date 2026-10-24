namespace Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>Por que o arquivo foi recusado, no vocabulário das mensagens ao candidato (UNI-REQ-0027).</summary>
public enum MotivoDaRecusa
{
    NaoSeAplica = 0,

    /// <summary>Acima do tamanho máximo do pedido; recusado sem ler.</summary>
    TamanhoExcedido = 1,

    /// <summary>O conteúdo não corresponde ao formato declarado.</summary>
    FormatoNaoCorresponde = 2,

    /// <summary>PDF que exige senha para abrir.</summary>
    PdfComSenha = 3,

    /// <summary>O antivírus apontou ameaça, inclusive por heurística ou limite de varredura.</summary>
    NaoPassouNaVerificacaoDeSeguranca = 4,
}

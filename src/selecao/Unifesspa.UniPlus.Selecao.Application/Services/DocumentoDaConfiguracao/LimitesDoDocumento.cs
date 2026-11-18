namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>Os limites do documento de configuração, que protegem a leitura antes de qualquer avaliação do conteúdo.</summary>
public static class LimitesDoDocumento
{
    public const int TamanhoMaximoEmBytes = 2 * 1024 * 1024;
    public const int ProfundidadeMaxima = 32;
    public const int QuantidadeMaximaDeNos = 10_000;

    /// <summary>
    /// O teto de valores do texto recebido, conferido enquanto ele é percorrido e antes de a árvore ser montada: protege a leitura
    /// do consumo sem limite de memória. É maior que o limite do documento declarado, que se confere depois de retirados os
    /// campos só de leitura.
    /// </summary>
    public const int QuantidadeMaximaDeNosDoTextoRecebido = 100_000;
}

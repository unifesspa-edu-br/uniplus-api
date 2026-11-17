namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>Os limites do documento de configuração, que protegem a leitura antes de qualquer avaliação do conteúdo.</summary>
public static class LimitesDoDocumento
{
    public const int TamanhoMaximoEmBytes = 2 * 1024 * 1024;
    public const int ProfundidadeMaxima = 32;
    public const int QuantidadeMaximaDeNos = 10_000;
}

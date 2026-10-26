namespace Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;

/// <summary>
/// Os limites do arquivo de retorno do INEP, num só lugar para a borda HTTP e o comando usarem o
/// mesmo número.
/// </summary>
public static class LimitesDoArquivoDoInep
{
    /// <summary>
    /// 10 MiB. Uma linha tem cerca de 170 bytes, então o limite comporta perto de 60 mil linhas, ordens
    /// de grandeza acima de qualquer processo seletivo da instituição.
    /// </summary>
    public const long TamanhoMaximoEmBytes = 10L * 1024 * 1024;

    /// <summary>Folga do corpo da requisição sobre o arquivo, para o enquadramento multipart e os campos de texto.</summary>
    public const long FolgaDaRequisicaoEmBytes = 64L * 1024;

    public const long TamanhoMaximoDaRequisicaoEmBytes = TamanhoMaximoEmBytes + FolgaDaRequisicaoEmBytes;
}

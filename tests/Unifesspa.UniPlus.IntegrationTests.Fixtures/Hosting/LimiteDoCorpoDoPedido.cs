namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using Unifesspa.UniPlus.Infrastructure.Core.CorpoDaRequisicao;

/// <summary>
/// O limite do corpo de um pedido que o servidor aplica por padrão e a folga que os maiores pedidos legítimos precisam deixar
/// até ele. O número é o das opções do servidor, sem cópia aqui.
/// </summary>
public static class LimiteDoCorpoDoPedido
{
    public const long Bytes = LimiteDoCorpoDaRequisicaoOptions.PadraoEmBytes;

    public const int FolgaMinima = 4;

    public static bool ComportaComFolga(long bytesDoMaiorPedido) =>
        bytesDoMaiorPedido * FolgaMinima <= Bytes;
}

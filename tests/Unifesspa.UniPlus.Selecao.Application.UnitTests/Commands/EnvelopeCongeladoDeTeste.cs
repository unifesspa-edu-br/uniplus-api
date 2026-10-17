namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

/// <summary>
/// O envelope congelado mínimo da versão vigente nos testes de retificação: a retificação lê dele os
/// anexos que herda, e um documento sem o bloco do edital não é um envelope.
/// </summary>
internal static class EnvelopeCongeladoDeTeste
{
    public static byte[] SemAnexos() => """{"hashesEdital":{"anexos":[]}}"""u8.ToArray();
}

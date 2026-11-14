namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

/// <summary>
/// O envelope congelado da versão vigente nos testes de retificação: a retificação lê dele os anexos que herda, e a
/// divulgação do certame lê dele os blocos públicos.
/// </summary>
internal static class EnvelopeCongeladoDeTeste
{
    public static byte[] SemAnexos() => TestSupport.EnvelopeProjetavelDeTeste.Bytes;
}

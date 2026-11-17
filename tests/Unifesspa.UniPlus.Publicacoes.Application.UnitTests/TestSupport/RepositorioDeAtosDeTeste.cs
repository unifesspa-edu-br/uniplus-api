namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

internal static class RepositorioDeAtosDeTeste
{
    /// <summary>Faz o repositório devolver o ato como um ato que ainda pode ser retificado: existe e ninguém o retificou.</summary>
    public static void ComoRetificavel(this IAtoNormativoRepository atos, AtoNormativo retificado)
    {
        ArgumentNullException.ThrowIfNull(atos);
        ArgumentNullException.ThrowIfNull(retificado);

        atos.ObterPorIdParaLeituraAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(retificado);
        atos.ObterRetificadorAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns((AtoNormativo?)null);
    }
}

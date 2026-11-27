namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class CatalogoDeTiposDeTeste
{
    /// <summary>Faz o catálogo devolver o tipo de ato vigente no dia da publicação.</summary>
    public static void ComTipoVigente(
        this ITipoAtoPublicadoRepository tipos, bool congelaConfiguracao = false, bool unicoPorObjeto = false)
    {
        ArgumentNullException.ThrowIfNull(tipos);

        TipoAtoPublicado tipo = new TipoAtoPublicadoDeTeste
        {
            Codigo = ComandoDeRegistroDeTeste.Tipo,
            CongelaConfiguracao = congelaConfiguracao,
            UnicoPorObjeto = unicoPorObjeto,
        }.Criar().Value!;
        tipos.ObterVigenteAsync(ComandoDeRegistroDeTeste.Tipo, ComandoDeRegistroDeTeste.DiaDaPublicacao, Arg.Any<CancellationToken>())
            .Returns(tipo);
    }
}

namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>O catálogo de tipos de ato dos testes dos gates de publicação.</summary>
internal static class CatalogoDeTiposDeAtoDeTeste
{
    /// <summary>
    /// Faz o catálogo devolver, para qualquer código que a publicação pergunte, um tipo vigente comum: não
    /// congela, não é resultado e não é único. A publicação congela a classificação de todo tipo que o
    /// cronograma declara, e os testes dos outros gates não se importam com ela.
    /// </summary>
    public static ITipoAtoPublicadoReader ComQualquerTipoDeclarado(this ITipoAtoPublicadoReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        reader.ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(chamada => chamada.Arg<IReadOnlyCollection<string>>()
                .Select(static codigo => new TipoAtoPublicadoView(codigo, codigo, CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: false))
                .ToList());
        return reader;
    }
}

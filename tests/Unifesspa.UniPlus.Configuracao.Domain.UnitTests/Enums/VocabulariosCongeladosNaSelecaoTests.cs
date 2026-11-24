namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// A Seleção valida os tokens que congela contra a cópia compartilhada do vocabulário do catálogo; a
/// cópia não pode divergir dos enums da Configuração.
/// </summary>
public sealed class VocabulariosCongeladosNaSelecaoTests
{
    [Fact(DisplayName = "Os tokens de componente de exibição que a Seleção valida são os do catálogo")]
    public void TokensCompartilhados_BatemComOsEnumsDoCatalogo()
    {
        DominioDoCatalogo.ComponentesDeExibicao.Should().BeEquivalentTo(
            CatalogoDe<ComponenteDeExibicao>.Todos().Select(static componente => componente.Codigo));
    }
}

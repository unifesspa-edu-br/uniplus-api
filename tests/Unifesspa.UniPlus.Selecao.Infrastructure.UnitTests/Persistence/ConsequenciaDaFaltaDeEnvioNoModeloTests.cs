namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class ConsequenciaDaFaltaDeEnvioNoModeloTests
{
    private static IProperty Propriedade() =>
        ModeloDaSelecao.Propriedade(typeof(FormularioProcesso), nameof(FormularioProcesso.ConsequenciaDaFaltaDeEnvio));

    [Fact(DisplayName = "A consequência da falta de envio é coluna opcional de texto de até 30 caracteres")]
    public void Coluna_DaConsequencia_EOpcionalDeTextoDeAte30Caracteres()
    {
        IProperty propriedade = Propriedade();

        propriedade.IsNullable.Should().BeTrue();
        propriedade.GetMaxLength().Should().Be(30);
    }

    [Fact(DisplayName = "A consequência vai para a coluna pelo código declarado e volta dele")]
    public void Conversao_DaConsequencia_UsaOCodigoDeclarado()
    {
        ValueConverter conversor = Propriedade().GetValueConverter()!;

        conversor.ConvertToProvider(ConsequenciaDoDescumprimento.RetiraOAcrescimo).Should().Be("RETIRA_O_ACRESCIMO");
        conversor.ConvertFromProvider("ELIMINA_DO_PROCESSO").Should().Be(ConsequenciaDoDescumprimento.EliminaDoProcesso);
    }

    [Fact(DisplayName = "A restrição da coluna aceita nulo e só os códigos do vocabulário")]
    public void Restricao_DaColuna_AceitaNuloEOVocabulario()
    {
        string sql = ModeloDaSelecao.SqlDaRestricao(typeof(FormularioProcesso), "ck_formularios_processo_consequencia_da_falta_de_envio");

        sql.Should().StartWith("consequencia_da_falta_de_envio IS NULL OR consequencia_da_falta_de_envio IN (")
            .And.Contain("'ELIMINA_DO_PROCESSO'").And.Contain("'RETIRA_A_MODALIDADE'").And.Contain("'RETIRA_O_ACRESCIMO'");
    }
}

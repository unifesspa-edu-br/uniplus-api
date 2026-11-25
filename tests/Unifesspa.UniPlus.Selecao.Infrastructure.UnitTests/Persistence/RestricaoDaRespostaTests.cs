namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class RestricaoDaRespostaTests
{
    [Fact]
    public void Modelo_RespostaDaInscricao_RestringeCadaTipoAColunaCorrespondente()
    {
        string sql = ModeloDaSelecao.SqlDaRestricao(typeof(RespostaDaInscricaoRecord), "ck_respostas_da_inscricao_valor_do_tipo");
        sql.Should().Be("num_nonnulls(valor_booleano, valor_numero, valor_codigo, valor_texto, valor_data, valor_cifrado) = 1 AND tipo IN ('SIM_OU_NAO', 'NUMERO', 'OPCAO', 'TEXTO', 'DATA', 'MUNICIPIO') AND ("
            + "valor_cifrado IS NOT NULL OR (tipo = 'SIM_OU_NAO' AND valor_booleano IS NOT NULL)"
            + " OR (tipo = 'NUMERO' AND valor_numero IS NOT NULL) OR (tipo = 'OPCAO' AND valor_codigo IS NOT NULL)"
            + " OR (tipo = 'TEXTO' AND valor_texto IS NOT NULL) OR (tipo = 'DATA' AND valor_data IS NOT NULL)"
            + " OR (tipo = 'MUNICIPIO' AND valor_codigo IS NOT NULL))"
            + " AND (valor_rotulo IS NULL OR (tipo = 'MUNICIPIO' AND valor_codigo IS NOT NULL))");
    }

    [Fact]
    public void Modelo_RespostaDaInscricao_RestringeOrigemAosCodigosDeclarados()
    {
        string sql = ModeloDaSelecao.SqlDaRestricao(typeof(RespostaDaInscricaoRecord), "ck_respostas_da_inscricao_origem");

        sql.Should().Be("origem IN ('INFORMADA', 'CALCULADA')");
    }
}

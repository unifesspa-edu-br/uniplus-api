namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class OperadorNoModeloDaSelecaoTests
{
    [Theory]
    [InlineData("""[[{"fato":"IDADE","operador":"IGUAL","valor":18}]]""")]
    [InlineData("""[[{"codigoDoDado":"IDADE","fato":"IDADE","operador":"IGUAL","valor":18}]]""")]
    public void LeituraDoPredicadoGravado_ComChaveAntiga_LancaExcecao(string json)
    {
        FluentActions.Invoking(() => ConversoresDeRegras.PredicadoObrigatorio.ConvertFromProvider(json))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("Dado inválido no banco ao reidratar PredicadoDnf: PredicadoDnf.FormaJsonInvalida. Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
    }

    public static TheoryData<Type> EntidadesComOperador => new()
    {
        typeof(CondicaoGatilho),
        typeof(CondicaoPrecondicaoFato),
        typeof(CondicaoRegraDerivacao),
    };

    private static ValueConverter ConversorDe(Type entidade)
    {
        ArgumentNullException.ThrowIfNull(entidade);

        return ModeloDaSelecao.Propriedade(entidade, "Operador").GetValueConverter()
            ?? throw new InvalidOperationException($"{entidade.Name}.Operador não tem conversor.");
    }

    [Theory(DisplayName = "A coluna do operador segue texto de até 20 caracteres")]
    [MemberData(nameof(EntidadesComOperador))]
    public void Coluna_DoOperador_ETextoDeAte20Caracteres(Type entidade)
    {
        IProperty propriedade = ModeloDaSelecao.Propriedade(entidade, "Operador");

        propriedade.GetMaxLength().Should().Be(20);
        propriedade.GetColumnType().Should().Be("character varying(20)");
    }

    [Theory(DisplayName = "O operador vai para a coluna pelo código declarado")]
    [MemberData(nameof(EntidadesComOperador))]
    public void Escrita_DoOperador_EOCodigoDeclarado(Type entidade)
    {
        ValueConverter conversor = ConversorDe(entidade);

        Enum.GetValues<Operador>().ToDictionary(static v => v, v => conversor.ConvertToProvider(v))
            .Should().Equal(new Dictionary<Operador, object?>
            {
                [Operador.Igual] = "IGUAL",
                [Operador.AlgumDe] = "ALGUM_DE",
                [Operador.MaiorIgual] = "MAIOR_IGUAL",
                [Operador.MenorIgual] = "MENOR_IGUAL",
                [Operador.Diferente] = "DIFERENTE",
                [Operador.NenhumDe] = "NENHUM_DE",
            });
    }

    [Theory(DisplayName = "A leitura do código declarado devolve o operador")]
    [MemberData(nameof(EntidadesComOperador))]
    public void Leitura_DoCodigoDeclarado_DevolveOOperador(Type entidade) =>
        ConversorDe(entidade).ConvertFromProvider("MENOR_IGUAL").Should().Be(Operador.MenorIgual);

    [Theory(DisplayName = "O operador gravado que não é código declarado falha ao carregar, sem virar operador nenhum")]
    [MemberData(nameof(EntidadesComOperador))]
    public void Leitura_DeOperadorDesconhecido_Falha(Type entidade) =>
        FluentActions.Invoking(() => ConversorDe(entidade).ConvertFromProvider("QUASE_IGUAL"))
            .Should().Throw<ArgumentException>();

    [Fact(DisplayName = "O critério de desempate por fato guarda o operador pelo código no documento")]
    public void Parametros_DoCriterioPorFato_GuardamOOperadorPeloCodigo()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("IDADE", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!;
        ValueConverter conversor = ModeloDaSelecao.Propriedade(typeof(CriterioDesempate), nameof(CriterioDesempate.Parametros)).GetValueConverter()!;

        string gravado = (string)conversor.ConvertToProvider(new ParametrosDesempatePredicadoFato(condicao))!;
        gravado.Should().Be("""{"$tipo":"condicaoSobreDado","condicao":{"codigoDoDado":"IDADE","operador":"MAIOR_IGUAL","valor":18}}""");
        ParametrosDesempatePredicadoFato lido = conversor.ConvertFromProvider(gravado).Should().BeOfType<ParametrosDesempatePredicadoFato>().Subject;
        lido.Condicao.Fato.Should().Be("IDADE");
        lido.Condicao.Operador.Should().Be(Operador.MaiorIgual);
        lido.Condicao.Valor.GetInt32().Should().Be(18);
    }

    [Fact(DisplayName = "O critério de desempate por fato não lê o operador gravado como número")]
    public void Parametros_ComOperadorGravadoComoNumero_Falham()
    {
        ValueConverter conversor = ModeloDaSelecao.Propriedade(typeof(CriterioDesempate), nameof(CriterioDesempate.Parametros)).GetValueConverter()!;

        FluentActions.Invoking(() => conversor.ConvertFromProvider("""{"$tipo":"condicaoSobreDado","condicao":{"codigoDoDado":"IDADE","operador":3,"valor":18}}"""))
            .Should().Throw<JsonException>();
    }

    [Theory(DisplayName = "A leitura do critério recusa a chave antiga ou a ausência do código do dado")]
    [InlineData("""{"$tipo":"condicaoSobreDado","condicao":{"fato":"IDADE","operador":"MAIOR_IGUAL","valor":18}}""")]
    [InlineData("""{"$tipo":"condicaoSobreDado","condicao":{"operador":"MAIOR_IGUAL","valor":18}}""")]
    [InlineData("""{"$tipo":"condicaoSobreDado","condicao":{"codigoDoDado":"IDADE","fato":"IDADE","operador":"MAIOR_IGUAL","valor":18}}""")]
    public void LeituraDosParametros_ComChaveAntigaOuCodigoDoDadoAusente_Recusa(string json)
    {
        ValueConverter conversor = ModeloDaSelecao.Propriedade(typeof(CriterioDesempate), nameof(CriterioDesempate.Parametros)).GetValueConverter()!;

        FluentActions.Invoking(() => conversor.ConvertFromProvider(json)).Should().Throw<JsonException>();
    }

    [Fact(DisplayName = "A leitura do critério aceita o código do dado")]
    public void LeituraDosParametros_ComCodigoDoDado_DevolveACondicao()
    {
        ValueConverter conversor = ModeloDaSelecao.Propriedade(typeof(CriterioDesempate), nameof(CriterioDesempate.Parametros)).GetValueConverter()!;

        ParametrosDesempatePredicadoFato parametros = conversor.ConvertFromProvider(
            """{"$tipo":"condicaoSobreDado","condicao":{"codigoDoDado":"IDADE","operador":"MAIOR_IGUAL","valor":18}}""")
            .Should().BeOfType<ParametrosDesempatePredicadoFato>().Subject;

        parametros.Condicao.Fato.Should().Be("IDADE");
        parametros.Condicao.Operador.Should().Be(Operador.MaiorIgual);
        parametros.Condicao.Valor.GetInt32().Should().Be(18);
    }
}

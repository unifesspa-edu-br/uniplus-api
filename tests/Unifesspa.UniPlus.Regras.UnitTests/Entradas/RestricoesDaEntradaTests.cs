namespace Unifesspa.UniPlus.Regras.UnitTests.Entradas;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

public sealed class RestricoesDaEntradaTests
{
    private static readonly LimiteDeDataInput LimiteValido = new("ENVIO", Anos: 1, Dias: 0);
    private static readonly LimiteDeDataInput LimiteInvalido = new("HOJE");

    [Fact(DisplayName = "Grupo de opções nulo e grupo com operador inválido voltam juntos, cada um no seu campo")]
    public void Restricao_GrupoNuloEOperadorInvalido_InformaOsDoisNoCaminhoDeCadaGrupo()
    {
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> quando =
            [[new CondicaoPrecondicaoInput("FATO_FICTICIO", "OPERADOR_FICTICIO", JsonSerializer.SerializeToElement("x"))]];
        RestricaoValorInput entrada = new("OPCOES_PERMITIDAS", Entradas: [null!, new OpcoesCondicionadasInput(quando, ["A"])]);

        Result<RestricaoValor> resultado = EntradaDeRegras.Restricao(entrada);

        resultado.Errors.Select(static e => e.Field).Should().Equal("entradas[0]", "entradas[1].quando[0][0].operador");
    }

    [Fact(DisplayName = "Com grupo recusado na leitura, os valores dos outros grupos não são conferidos")]
    public void Restricao_GrupoNuloEOutroSemValores_InformaSoOGrupoNulo()
    {
        RestricaoValorInput entrada = new("OPCOES_PERMITIDAS", Entradas: [null!, new OpcoesCondicionadasInput(null, [])]);

        Result<RestricaoValor> resultado = EntradaDeRegras.Restricao(entrada);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("entradas[0]");
    }

    [Fact(DisplayName = "Grupos nulos entre grupos válidos mantêm a posição de cada um")]
    public void Restricao_GruposNulosEntreValidos_MantemAPosicaoDeCada()
    {
        OpcoesCondicionadasInput valido = new(null, ["A"]);
        RestricaoValorInput entrada = new("OPCOES_PERMITIDAS", Entradas: [valido, null!, valido, null!]);

        Result<RestricaoValor> resultado = EntradaDeRegras.Restricao(entrada);

        resultado.Errors.Select(static e => e.Field).Should().Equal("entradas[1]", "entradas[3]");
    }

    [Fact(DisplayName = "Opções sem a lista de grupos são uma recusa de opções vazias sem campo")]
    public void Restricao_SemListaDeGrupos_RecusaOpcoesVaziasSemCampo()
    {
        Result<RestricaoValor> resultado = EntradaDeRegras.Restricao(new RestricaoValorInput("OPCOES_PERMITIDAS"));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(static e =>
            e.Field == null && e.Error.Code == RestricaoValorErrorCodes.OpcoesVazias);
    }

    [Fact(DisplayName = "Só o limite máximo inválido é recusado no campo dataMaxima")]
    public void Restricao_SoOMaximoInvalido_InformaDataMaxima()
    {
        RestricaoValorInput entrada = new("FAIXA_DE_DATA", DataMinima: LimiteValido, DataMaxima: LimiteInvalido);

        Result<RestricaoValor> resultado = EntradaDeRegras.Restricao(entrada);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("dataMaxima");
    }

    [Fact(DisplayName = "Os dois limites inválidos voltam, o mínimo antes do máximo")]
    public void Restricao_OsDoisLimitesInvalidos_InformaDataMinimaAntesDeDataMaxima()
    {
        RestricaoValorInput entrada = new("FAIXA_DE_DATA", DataMinima: LimiteInvalido, DataMaxima: LimiteInvalido);

        Result<RestricaoValor> resultado = EntradaDeRegras.Restricao(entrada);

        resultado.Errors.Select(static e => e.Field).Should().Equal("dataMinima", "dataMaxima");
    }

    [Fact(DisplayName = "Restrição nula é recusada como tipo desconhecido")]
    public void Restricao_Nula_RecusaTipoDesconhecido()
    {
        Result<RestricaoValor> resultado = EntradaDeRegras.Restricao(null);

        resultado.Error!.Code.Should().Be(RestricaoValorErrorCodes.TipoDesconhecido);
        resultado.Error.Message.Should().Be("A restrição de valor é nula.");
    }
}

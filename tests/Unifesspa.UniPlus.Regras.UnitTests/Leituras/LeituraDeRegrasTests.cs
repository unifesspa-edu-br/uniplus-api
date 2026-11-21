namespace Unifesspa.UniPlus.Regras.UnitTests.Leituras;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>As regras do formulário na forma de leitura, e a volta dessa forma à de entrada.</summary>
public sealed class LeituraDeRegrasTests
{
    private static PredicadoDnf PredicadoDoFato(string fato) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;

    [Fact(DisplayName = "O predicado sem cláusula vai à leitura como lista vazia, nunca como ausência")]
    public void ToDto_PredicadoSemClausula_DevolveListaVazia()
    {
        PredicadoDnf semClausula = PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!;

        semClausula.ToDto().Should().BeEmpty();
    }

    [Fact(DisplayName = "A obrigatoriedade QUANDO vai à leitura com o token e o predicado")]
    public void ToDto_ObrigatoriedadeQuando_LevaTokenEPredicado()
    {
        ObrigatoriedadeDto dto = Obrigatoriedade.Quando(PredicadoDoFato("ANTERIOR")).ToDto();

        dto.Tipo.Should().Be("QUANDO");
        dto.Predicado.Should().ContainSingle().Which.Should().ContainSingle().Which.Fato.Should().Be("ANTERIOR");
    }

    [Fact(DisplayName = "A obrigatoriedade SEMPRE vai à leitura sem predicado")]
    public void ToDto_ObrigatoriedadeSempre_VaiSemPredicado()
    {
        ObrigatoriedadeDto dto = Obrigatoriedade.Sempre.ToDto();

        dto.Tipo.Should().Be("SEMPRE");
        dto.Predicado.Should().BeNull();
    }

    [Fact(DisplayName = "As ocorrências do grupo vão à leitura com o mínimo, o máximo e o candidato entre os membros")]
    public void ToDto_Ocorrencias_LevaOsTresValores()
    {
        new OcorrenciasDoGrupo(1, 5, IncluiCandidato: true).ToDto().Should().Be(new OcorrenciasDoGrupoDto(1, 5, true));
    }

    [Fact(DisplayName = "As regras do campo vão à leitura com a exibição ausente como nula e as restrições na ordem recebida")]
    public void ToDto_RegrasDoCampo_LevaExibicaoNulaERestricoes()
    {
        RestricaoValor faixa = RestricoesDeValor.Faixa(1m, 9m).Value!;
        RegrasDoCampoDto dto = new RegrasDoCampo(null, Obrigatoriedade.Nunca, [faixa], null).ToDto();

        dto.Exibicao.Should().BeNull();
        dto.Impedimento.Should().BeNull();
        dto.Restricoes.Should().ContainSingle().Which.Should().Be(new RestricaoValorDto("FAIXA_NUMERICA", 1m, 9m, null, null, null, null));
    }

    [Fact(DisplayName = "O predicado da leitura volta à entrada igual ao que a leitura do domínio produz")]
    public void ParaEntrada_Predicado_IgualAoDoDominio()
    {
        PredicadoDnf predicado = PredicadoDoFato("ANTERIOR");

        LeituraDeRegras.ParaEntrada(predicado.ToDto()).Should().BeEquivalentTo(EntradaDeRegras.ParaEntrada(predicado));
    }

    [Fact(DisplayName = "O predicado nulo da leitura continua nulo na entrada")]
    public void ParaEntrada_PredicadoNulo_ContinuaNulo()
    {
        LeituraDeRegras.ParaEntrada((IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>?)null).Should().BeNull();
    }

    [Fact(DisplayName = "A restrição de opções da leitura volta à entrada com os grupos e a condição de cada um")]
    public void ParaEntrada_RestricaoDeOpcoes_LevaOsGrupos()
    {
        RestricaoValor opcoes = RestricoesDeValor.Opcoes([(PredicadoDoFato("ANTERIOR"), ["B", "A"])]).Value!;

        RestricaoValorInput entrada = LeituraDeRegras.ParaEntrada(opcoes.ToDto());

        entrada.Should().BeEquivalentTo(EntradaDeRegras.ParaEntrada(opcoes));
    }

    [Fact(DisplayName = "A restrição de faixa de data da leitura volta à entrada com os dois limites")]
    public void ParaEntrada_RestricaoDeFaixaDeData_LevaOsLimites()
    {
        LimiteDeData minimo = RestricoesDeValor.Limite("ENVIO", null, -18, 0).Value!;
        LimiteDeData maximo = RestricoesDeValor.Limite("DATA_FIXA", new DateOnly(2030, 1, 31), null, null).Value!;
        RestricaoValor faixa = RestricoesDeValor.FaixaDeData(minimo, maximo).Value!;

        RestricaoValorInput entrada = LeituraDeRegras.ParaEntrada(faixa.ToDto());

        entrada.Should().BeEquivalentTo(EntradaDeRegras.ParaEntrada(faixa));
    }

    [Fact(DisplayName = "O impedimento da leitura volta à entrada com a condição e a mensagem; o ausente continua ausente")]
    public void ParaEntrada_Impedimento_LevaCondicaoEMensagem()
    {
        Impedimento impedimento = new(PredicadoDoFato("ANTERIOR"), "Não pode se inscrever.");

        LeituraDeRegras.ParaEntrada(impedimento.ToDto()).Should().BeEquivalentTo(EntradaDeRegras.ParaEntrada(impedimento));
        LeituraDeRegras.ParaEntrada((ImpedimentoDto?)null).Should().BeNull();
    }
}

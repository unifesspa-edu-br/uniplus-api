namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConceitoDaAvaliacao;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

/// <summary>
/// O critério, os corretores distintos e o tratamento quando o critério não converge são dado da versão da regra de
/// consolidação de conceito categórico (ADR-0150): a versão que não os declara de forma válida não tem declaração, e
/// nenhum valor é presumido.
/// </summary>
public sealed class DeclaracaoDaConsolidacaoDoConceitoTests
{
    private const string EsquemaDaUnanimidade =
        """{"corretores_distintos":2,"criterio":"UNANIMIDADE","formas_do_conceito":["POR_OPCOES"],"quando_nao_consolida":"NOVO_CORRETOR"}""";

    private static RegraCatalogo Regra(string codigo, string declaracaoDaRegra, TipoRegra tipo = TipoRegra.RegraApuracaoAvaliacao)
    {
        using JsonDocument esquema = JsonDocument.Parse(declaracaoDaRegra);
        using JsonDocument invariantes = JsonDocument.Parse("[]");
        return RegraCatalogo.Criar(codigo, "v1", tipo, esquema.RootElement, invariantes.RootElement, "Edital do processo seletivo");
    }

    private static RegraCatalogo DeConsolidacao(string declaracaoDaRegra) =>
        Regra(RegraDeApuracaoDaAvaliacao.ConceitoUnanimidade.Codigo(), declaracaoDaRegra);

    [Fact(DisplayName = "A unanimidade declara dois corretores e o novo corretor quando os valores divergem")]
    public void De_Unanimidade_DevolveACriterioCorretoresETratamento()
    {
        DeclaracaoDaConsolidacaoDoConceito declaracao = DeclaracaoDaConsolidacaoDoConceito.De(DeConsolidacao(EsquemaDaUnanimidade));

        declaracao.Should().Be(new DeclaracaoDaConsolidacaoDoConceito(
            CriterioDeApuracaoDoConceito.Unanimidade, 2, TratamentoDaFaltaDeApuracao.NovoCorretor));
    }

    [Theory(DisplayName = "A maioria declara três corretores e o critério da versão")]
    [InlineData("APURACAO_CONCEITO_MAIORIA_SIMPLES", "MAIORIA_SIMPLES", CriterioDeApuracaoDoConceito.MaioriaSimples)]
    [InlineData("APURACAO_CONCEITO_MAIORIA_ABSOLUTA", "MAIORIA_ABSOLUTA", CriterioDeApuracaoDoConceito.MaioriaAbsoluta)]
    public void De_Maioria_DevolveACriterioCorretoresETratamento(string codigo, string criterio, CriterioDeApuracaoDoConceito esperado)
    {
        RegraCatalogo regra = Regra(
            codigo,
            $$"""{"corretores_distintos":3,"criterio":"{{criterio}}","formas_do_conceito":["POR_OPCOES"],"quando_nao_consolida":"NOVO_CORRETOR"}""");

        DeclaracaoDaConsolidacaoDoConceito.De(regra).Should().Be(
            new DeclaracaoDaConsolidacaoDoConceito(esperado, 3, TratamentoDaFaltaDeApuracao.NovoCorretor));
    }

    [Fact(DisplayName = "O tratamento declarado pela versão é o que a leitura devolve, sem valor fixo")]
    public void De_TratamentoDaBanca_DevolveDecisaoDaBanca()
    {
        RegraCatalogo regra = DeConsolidacao(EsquemaDaUnanimidade.Replace("NOVO_CORRETOR", "DECISAO_DA_BANCA", StringComparison.Ordinal));

        DeclaracaoDaConsolidacaoDoConceito.De(regra).Tratamento.Should().Be(TratamentoDaFaltaDeApuracao.DecisaoDaBanca);
    }

    [Theory(DisplayName = "A versão numérica, sem critério ou tratamento, não tem declaração")]
    [InlineData("""{"corretores_distintos":3,"formas_do_conceito":["NUMERICO"]}""")]
    [InlineData("""{"corretores_distintos":1,"formas_do_conceito":["POR_OPCOES","NUMERICO"]}""")]
    public void Tentar_RegraNumerica_Falha(string declaracaoDaRegra)
    {
        bool declarada = DeclaracaoDaConsolidacaoDoConceito.Tentar(DeConsolidacao(declaracaoDaRegra), out DeclaracaoDaConsolidacaoDoConceito? declaracao);

        declarada.Should().BeFalse();
        declaracao.Should().BeNull();
    }

    [Theory(DisplayName = "A versão sem uma das quatro chaves, ou com chave de tipo errado ou token desconhecido, não tem declaração")]
    [InlineData("""{"criterio":"UNANIMIDADE","formas_do_conceito":["POR_OPCOES"],"quando_nao_consolida":"NOVO_CORRETOR"}""")]
    [InlineData("""{"corretores_distintos":2,"formas_do_conceito":["POR_OPCOES"],"quando_nao_consolida":"NOVO_CORRETOR"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"UNANIMIDADE","formas_do_conceito":["POR_OPCOES"]}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"UNANIMIDADE","quando_nao_consolida":"NOVO_CORRETOR"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"UNANIMIDADE","formas_do_conceito":[],"quando_nao_consolida":"NOVO_CORRETOR"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"UNANIMIDADE","formas_do_conceito":"POR_OPCOES","quando_nao_consolida":"NOVO_CORRETOR"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"UNANIMIDADE","formas_do_conceito":["POR_OPCOES","ordinal"],"quando_nao_consolida":"NOVO_CORRETOR"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"consenso","formas_do_conceito":["POR_OPCOES"],"quando_nao_consolida":"NOVO_CORRETOR"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"Unanimidade","formas_do_conceito":["POR_OPCOES"],"quando_nao_consolida":"NOVO_CORRETOR"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":1,"formas_do_conceito":["POR_OPCOES"],"quando_nao_consolida":"NOVO_CORRETOR"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":null,"formas_do_conceito":["POR_OPCOES"],"quando_nao_consolida":"NOVO_CORRETOR"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"UNANIMIDADE","formas_do_conceito":["POR_OPCOES"],"quando_nao_consolida":"sorteio"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"UNANIMIDADE","formas_do_conceito":["POR_OPCOES"],"quando_nao_consolida":["NOVO_CORRETOR"]}""")]
    [InlineData("""{"corretores_distintos":0,"criterio":"UNANIMIDADE","formas_do_conceito":["POR_OPCOES"],"quando_nao_consolida":"NOVO_CORRETOR"}""")]
    [InlineData("""{"corretores_distintos":"2","criterio":"UNANIMIDADE","formas_do_conceito":["POR_OPCOES"],"quando_nao_consolida":"NOVO_CORRETOR"}""")]
    public void Tentar_DeclaracaoInvalida_FalhaELancaEmDe(string declaracaoDaRegra)
    {
        RegraCatalogo regra = DeConsolidacao(declaracaoDaRegra);

        DeclaracaoDaConsolidacaoDoConceito.Tentar(regra, out DeclaracaoDaConsolidacaoDoConceito? declaracao).Should().BeFalse();
        declaracao.Should().BeNull();
        Func<DeclaracaoDaConsolidacaoDoConceito> ler = () => DeclaracaoDaConsolidacaoDoConceito.De(regra);
        ler.Should().Throw<ArgumentException>().WithMessage("*critério*");
    }

    [Fact(DisplayName = "A regra que não é de consolidação não tem declaração, mesmo com as chaves no esquema")]
    public void De_RegraDeOutroTipo_Lanca()
    {
        RegraCatalogo regra = Regra("FORMULA-DE-TESTE", EsquemaDaUnanimidade, TipoRegra.RegraCalculo);

        Func<DeclaracaoDaConsolidacaoDoConceito> ler = () => DeclaracaoDaConsolidacaoDoConceito.De(regra);

        ler.Should().Throw<ArgumentException>().WithMessage("*consolidação*");
    }

    [Fact(DisplayName = "A regra nula é recusada")]
    public void De_Nula_Lanca()
    {
        Func<DeclaracaoDaConsolidacaoDoConceito> ler = () => DeclaracaoDaConsolidacaoDoConceito.De(null!);

        ler.Should().Throw<ArgumentNullException>();
    }
}

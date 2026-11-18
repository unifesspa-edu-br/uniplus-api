namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConceitoDaAvaliacao;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

/// <summary>
/// O número de corretores distintos é dado da versão da regra de consolidação (ADR-0150): a versão que não o declara
/// como inteiro maior ou igual a um é defeito do catálogo, a leitura lança e nenhum número é presumido.
/// </summary>
public sealed class CorretoresDaRegraDeConsolidacaoTests
{
    private static RegraCatalogo Regra(string codigo, string declaracaoDaRegra, TipoRegra tipo = TipoRegra.RegraConsolidacao)
    {
        using JsonDocument esquema = JsonDocument.Parse(declaracaoDaRegra);
        using JsonDocument invariantes = JsonDocument.Parse("[]");
        return RegraCatalogo.Criar(codigo, "v1", tipo, esquema.RootElement, invariantes.RootElement, "Edital do processo seletivo");
    }

    private static RegraCatalogo DeConsolidacao(string declaracaoDaRegra) =>
        Regra(RegraConsolidacaoCodigo.CorrecaoIndependente, declaracaoDaRegra);

    [Fact(DisplayName = "A avaliação única exige um corretor")]
    public void De_AvaliacaoUnica_DevolveUm()
    {
        RegraCatalogo regra = Regra(
            RegraConsolidacaoCodigo.AvaliacaoUnica,
            """{"corretores_distintos":1,"naturezas_do_conceito":["categorico","numerico"]}""");

        CorretoresDaRegraDeConsolidacao.De(regra).Should().Be(1);
    }

    [Fact(DisplayName = "A correção independente exige três corretores")]
    public void De_CorrecaoIndependente_DevolveTres()
    {
        RegraCatalogo regra = Regra(
            RegraConsolidacaoCodigo.CorrecaoIndependente,
            """{"corretores_distintos":3,"naturezas_do_conceito":["numerico"]}""");

        CorretoresDaRegraDeConsolidacao.De(regra).Should().Be(3);
    }

    [Theory(DisplayName = "A versão sem o número, ou com número que não é inteiro maior ou igual a um, lança")]
    [InlineData("{}")]
    [InlineData("""{"naturezas_do_conceito":["numerico"]}""")]
    [InlineData("""{"corretores_distintos":0}""")]
    [InlineData("""{"corretores_distintos":-2}""")]
    [InlineData("""{"corretores_distintos":2.5}""")]
    [InlineData("""{"corretores_distintos":"3"}""")]
    [InlineData("""{"corretores_distintos":null}""")]
    [InlineData("""{"corretores_distintos":3000000000}""")]
    public void De_DeclaracaoInvalida_Lanca(string declaracaoDaRegra)
    {
        Func<int> ler = () => CorretoresDaRegraDeConsolidacao.De(DeConsolidacao(declaracaoDaRegra));

        ler.Should().Throw<ArgumentException>().WithMessage("*corretores*");
    }

    [Theory(DisplayName = "Tentar devolve falso para a declaração inválida e o número para a válida, sem lançar")]
    [InlineData("{}", false, 0)]
    [InlineData("""{"corretores_distintos":0}""", false, 0)]
    [InlineData("""{"corretores_distintos":"3"}""", false, 0)]
    [InlineData("""{"corretores_distintos":3}""", true, 3)]
    public void Tentar_DevolveSeADeclaracaoEValida(string declaracaoDaRegra, bool valida, int esperado)
    {
        bool declarada = CorretoresDaRegraDeConsolidacao.Tentar(DeConsolidacao(declaracaoDaRegra), out int corretores);

        declarada.Should().Be(valida);
        corretores.Should().Be(esperado);
    }

    [Fact(DisplayName = "A regra que não é de consolidação não declara corretores, mesmo com a chave no esquema")]
    public void De_RegraDeOutroTipo_Lanca()
    {
        RegraCatalogo regra = Regra("FORMULA-DE-TESTE", """{"corretores_distintos":3}""", TipoRegra.RegraCalculo);

        Func<int> ler = () => CorretoresDaRegraDeConsolidacao.De(regra);

        ler.Should().Throw<ArgumentException>().WithMessage("*consolidação*");
    }

    [Theory(DisplayName = "Os corretores em campo são os distintos mais o adicional só quando o tratamento é o novo corretor")]
    [InlineData("""{"corretores_distintos":3,"criterio":"maioria_simples","naturezas_do_conceito":["categorico"],"quando_nao_consolida":"novo_corretor"}""", 4)]
    [InlineData("""{"corretores_distintos":2,"criterio":"unanimidade","naturezas_do_conceito":["categorico"],"quando_nao_consolida":"novo_corretor"}""", 3)]
    [InlineData("""{"corretores_distintos":2,"criterio":"unanimidade","naturezas_do_conceito":["categorico"],"quando_nao_consolida":"decisao_da_banca"}""", 2)]
    [InlineData("""{"corretores_distintos":3,"naturezas_do_conceito":["numerico"]}""", 3)]
    public void TentarEmCampo_DevolveOsDistintosEOAdicionalDoTratamento(string declaracaoDaRegra, int esperado)
    {
        bool lida = CorretoresDaRegraDeConsolidacao.TentarEmCampo(DeConsolidacao(declaracaoDaRegra), out int emCampo);

        lida.Should().BeTrue();
        emCampo.Should().Be(esperado);
    }

    [Theory(DisplayName = "Os corretores em campo não se leem quando os distintos ou o tratamento declarado são inválidos, ou quando há critério sem tratamento")]
    [InlineData("""{"naturezas_do_conceito":["categorico"],"quando_nao_consolida":"novo_corretor"}""")]
    [InlineData("""{"corretores_distintos":0,"quando_nao_consolida":"novo_corretor"}""")]
    [InlineData("""{"corretores_distintos":3,"criterio":"maioria_simples","naturezas_do_conceito":["categorico"]}""")]
    [InlineData("""{"corretores_distintos":3,"quando_nao_consolida":"sorteio"}""")]
    [InlineData("""{"corretores_distintos":3,"quando_nao_consolida":"NovoCorretor"}""")]
    [InlineData("""{"corretores_distintos":3,"quando_nao_consolida":1}""")]
    [InlineData("""{"corretores_distintos":3,"quando_nao_consolida":null}""")]
    [InlineData("""{"corretores_distintos":3,"quando_nao_consolida":["novo_corretor"]}""")]
    public void TentarEmCampo_DeclaracaoInvalida_Falha(string declaracaoDaRegra)
    {
        bool lida = CorretoresDaRegraDeConsolidacao.TentarEmCampo(DeConsolidacao(declaracaoDaRegra), out int emCampo);

        lida.Should().BeFalse();
        emCampo.Should().Be(0);
    }

    [Fact(DisplayName = "A regra que não é de consolidação não põe corretores em campo")]
    public void TentarEmCampo_RegraDeOutroTipo_Falha()
    {
        RegraCatalogo regra = Regra("FORMULA-DE-TESTE", """{"corretores_distintos":3,"quando_nao_consolida":"novo_corretor"}""", TipoRegra.RegraCalculo);

        CorretoresDaRegraDeConsolidacao.TentarEmCampo(regra, out _).Should().BeFalse();
    }

    [Fact(DisplayName = "A regra nula é recusada")]
    public void De_Nula_Lanca()
    {
        Func<int> ler = () => CorretoresDaRegraDeConsolidacao.De(null!);

        ler.Should().Throw<ArgumentNullException>();
    }
}

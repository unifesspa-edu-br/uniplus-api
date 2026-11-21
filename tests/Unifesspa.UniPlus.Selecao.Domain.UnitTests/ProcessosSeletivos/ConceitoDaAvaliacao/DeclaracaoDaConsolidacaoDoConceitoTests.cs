namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConceitoDaAvaliacao;

using System.Text.Json;

using AwesomeAssertions;

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
        """{"corretores_distintos":2,"criterio":"unanimidade","naturezas_do_conceito":["CATEGORICO"],"quando_nao_consolida":"novo_corretor"}""";

    private static RegraCatalogo Regra(string codigo, string declaracaoDaRegra, TipoRegra tipo = TipoRegra.RegraConsolidacao)
    {
        using JsonDocument esquema = JsonDocument.Parse(declaracaoDaRegra);
        using JsonDocument invariantes = JsonDocument.Parse("[]");
        return RegraCatalogo.Criar(codigo, "v1", tipo, esquema.RootElement, invariantes.RootElement, "Edital do processo seletivo");
    }

    private static RegraCatalogo DeConsolidacao(string declaracaoDaRegra) =>
        Regra(RegraConsolidacaoCodigo.ConceitoUnanimidade, declaracaoDaRegra);

    [Fact(DisplayName = "A unanimidade declara dois corretores e o novo corretor quando os valores divergem")]
    public void De_Unanimidade_DevolveACriterioCorretoresETratamento()
    {
        DeclaracaoDaConsolidacaoDoConceito declaracao = DeclaracaoDaConsolidacaoDoConceito.De(DeConsolidacao(EsquemaDaUnanimidade));

        declaracao.Should().Be(new DeclaracaoDaConsolidacaoDoConceito(
            CriterioDeConsolidacaoDoConceito.Unanimidade, 2, TratamentoDaFaltaDeConsolidacao.NovoCorretor));
    }

    [Theory(DisplayName = "A maioria declara três corretores e o critério da versão")]
    [InlineData(RegraConsolidacaoCodigo.ConceitoMaioriaSimples, "maioria_simples", CriterioDeConsolidacaoDoConceito.MaioriaSimples)]
    [InlineData(RegraConsolidacaoCodigo.ConceitoMaioriaAbsoluta, "maioria_absoluta", CriterioDeConsolidacaoDoConceito.MaioriaAbsoluta)]
    public void De_Maioria_DevolveACriterioCorretoresETratamento(string codigo, string criterio, CriterioDeConsolidacaoDoConceito esperado)
    {
        RegraCatalogo regra = Regra(
            codigo,
            $$"""{"corretores_distintos":3,"criterio":"{{criterio}}","naturezas_do_conceito":["CATEGORICO"],"quando_nao_consolida":"novo_corretor"}""");

        DeclaracaoDaConsolidacaoDoConceito.De(regra).Should().Be(
            new DeclaracaoDaConsolidacaoDoConceito(esperado, 3, TratamentoDaFaltaDeConsolidacao.NovoCorretor));
    }

    [Fact(DisplayName = "O tratamento declarado pela versão é o que a leitura devolve, sem valor fixo")]
    public void De_TratamentoDaBanca_DevolveDecisaoDaBanca()
    {
        RegraCatalogo regra = DeConsolidacao(EsquemaDaUnanimidade.Replace("novo_corretor", "decisao_da_banca", StringComparison.Ordinal));

        DeclaracaoDaConsolidacaoDoConceito.De(regra).Tratamento.Should().Be(TratamentoDaFaltaDeConsolidacao.DecisaoDaBanca);
    }

    [Theory(DisplayName = "A versão numérica, sem critério ou tratamento, não tem declaração")]
    [InlineData("""{"corretores_distintos":3,"naturezas_do_conceito":["NUMERICO"]}""")]
    [InlineData("""{"corretores_distintos":1,"naturezas_do_conceito":["CATEGORICO","NUMERICO"]}""")]
    public void Tentar_RegraNumerica_Falha(string declaracaoDaRegra)
    {
        bool declarada = DeclaracaoDaConsolidacaoDoConceito.Tentar(DeConsolidacao(declaracaoDaRegra), out DeclaracaoDaConsolidacaoDoConceito? declaracao);

        declarada.Should().BeFalse();
        declaracao.Should().BeNull();
    }

    [Theory(DisplayName = "A versão sem uma das quatro chaves, ou com chave de tipo errado ou token desconhecido, não tem declaração")]
    [InlineData("""{"criterio":"unanimidade","naturezas_do_conceito":["CATEGORICO"],"quando_nao_consolida":"novo_corretor"}""")]
    [InlineData("""{"corretores_distintos":2,"naturezas_do_conceito":["CATEGORICO"],"quando_nao_consolida":"novo_corretor"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"unanimidade","naturezas_do_conceito":["CATEGORICO"]}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"unanimidade","quando_nao_consolida":"novo_corretor"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"unanimidade","naturezas_do_conceito":[],"quando_nao_consolida":"novo_corretor"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"unanimidade","naturezas_do_conceito":"CATEGORICO","quando_nao_consolida":"novo_corretor"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"unanimidade","naturezas_do_conceito":["CATEGORICO","ordinal"],"quando_nao_consolida":"novo_corretor"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"consenso","naturezas_do_conceito":["CATEGORICO"],"quando_nao_consolida":"novo_corretor"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"Unanimidade","naturezas_do_conceito":["CATEGORICO"],"quando_nao_consolida":"novo_corretor"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":1,"naturezas_do_conceito":["CATEGORICO"],"quando_nao_consolida":"novo_corretor"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":null,"naturezas_do_conceito":["CATEGORICO"],"quando_nao_consolida":"novo_corretor"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"unanimidade","naturezas_do_conceito":["CATEGORICO"],"quando_nao_consolida":"sorteio"}""")]
    [InlineData("""{"corretores_distintos":2,"criterio":"unanimidade","naturezas_do_conceito":["CATEGORICO"],"quando_nao_consolida":["novo_corretor"]}""")]
    [InlineData("""{"corretores_distintos":0,"criterio":"unanimidade","naturezas_do_conceito":["CATEGORICO"],"quando_nao_consolida":"novo_corretor"}""")]
    [InlineData("""{"corretores_distintos":"2","criterio":"unanimidade","naturezas_do_conceito":["CATEGORICO"],"quando_nao_consolida":"novo_corretor"}""")]
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

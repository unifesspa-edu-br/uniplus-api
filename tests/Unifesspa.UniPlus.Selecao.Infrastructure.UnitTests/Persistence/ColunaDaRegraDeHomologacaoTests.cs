namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using static Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport.RegraDeHomologacaoDeTeste;

/// <summary>
/// O documento <c>jsonb</c> da regra de homologação na fase: a ida e volta sem perda, a ordem declarada, o
/// enquadramento presente e ausente e a falha alta diante de conteúdo gravado inválido. Não toca em banco.
/// </summary>
public sealed class ColunaDaRegraDeHomologacaoTests
{
    private const string ResultadoPreliminar = "RESULTADO_PRELIMINAR_HOMOLOGACAO";

    [Theory]
    [InlineData("{\"Alternativas\":[]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\",\"edicaoDoExame\":\"2024\"}]}]}")]
    [InlineData("{\"alternativas\":[],\"desconhecida\":1}")]
    [InlineData("{\"alternativas\":[],\"alternativas\":[]}")]
    public void Ler_JsonComConteudoInvalido_IdentificaDadoCorrompido(string documento)
    {
        Action leitura = () => ColunaDaRegraDeHomologacao.Ler(documento);

        leitura.Should().Throw<InvalidOperationException>()
            .WithMessage("Regra de homologação gravada inválida:*");
    }

    [Fact(DisplayName = "Regra com enquadramento e sem enquadramento volta igual ao que foi gravada")]
    public void RegraCompleta_VoltaIgual()
    {
        RegraDeHomologacaoDasInscricoes regra = Regra(
            ResultadoPreliminar,
            Alternativa(Condicao(2025, "SEM_RESULTADO_INEP", enquadramento: null), Condicao(2024, "INSCRICAO_DE_OUTRA_EDICAO", EnquadramentoDaModalidade("AMPLA"))));

        RegraDeHomologacaoDasInscricoes lida = ColunaDaRegraDeHomologacao.Ler(ColunaDaRegraDeHomologacao.Escrever(regra));

        lida.MesmoConteudo(regra).Should().BeTrue();
        lida.Alternativas[0].Condicoes[1].Enquadramento!.MesmoConteudo(EnquadramentoDaModalidade("AMPLA")).Should().BeTrue();
    }

    [Fact(DisplayName = "Regra sem alternativa volta sem alternativa, e o resultado e o motivo ausentes continuam ausentes")]
    public void RegraSemAlternativaEComAusencias_VoltaIgual()
    {
        RegraDeHomologacaoDasInscricoes semAlternativa = Regra(resultadoAtoCodigo: null);
        RegraDeHomologacaoDasInscricoes semMotivo = Regra(ResultadoPreliminar, Alternativa(Condicao(2025, motivo: null, enquadramento: null)));

        ColunaDaRegraDeHomologacao.Ler(ColunaDaRegraDeHomologacao.Escrever(semAlternativa)).MesmoConteudo(semAlternativa).Should().BeTrue();
        RegraDeHomologacaoDasInscricoes lida = ColunaDaRegraDeHomologacao.Ler(ColunaDaRegraDeHomologacao.Escrever(semMotivo));
        lida.ResultadoAtoCodigo.Should().Be(ResultadoPreliminar);
        lida.Alternativas[0].Condicoes[0].MotivoDesfavoravelCodigo.Should().BeNull();
    }

    [Fact(DisplayName = "A ordem declarada das alternativas e das condições é preservada na ida e volta")]
    public void OrdemDeclarada_EPreservada()
    {
        RegraDeHomologacaoDasInscricoes regra = Regra(
            ResultadoPreliminar,
            Alternativa(Condicao(2026, "MOTIVO_C", enquadramento: null), Condicao(2024, "MOTIVO_A", enquadramento: null), Condicao(2025, "MOTIVO_B", enquadramento: null)),
            Alternativa(Condicao(2023, "MOTIVO_Z", enquadramento: null)),
            Alternativa(Condicao(2022, "MOTIVO_D", enquadramento: null)));

        RegraDeHomologacaoDasInscricoes lida = ColunaDaRegraDeHomologacao.Ler(ColunaDaRegraDeHomologacao.Escrever(regra));

        lida.Alternativas.Select(alternativa => alternativa.Condicoes.Count).Should().Equal(3, 1, 1);
        lida.Alternativas[0].Condicoes.Select(condicao => condicao.MotivoDesfavoravelCodigo).Should().Equal("MOTIVO_C", "MOTIVO_A", "MOTIVO_B");
        lida.Alternativas[1].Condicoes[0].MotivoDesfavoravelCodigo.Should().Be("MOTIVO_Z");
    }

    [Fact(DisplayName = "O enquadramento ausente não entra no documento e o presente entra na forma do predicado")]
    public void Enquadramento_AusenteNaoEntraEPresenteEntra()
    {
        string sem = ColunaDaRegraDeHomologacao.Escrever(Regra(ResultadoPreliminar, Alternativa(Condicao(2025, "MOTIVO_M", enquadramento: null))));
        string com = ColunaDaRegraDeHomologacao.Escrever(Regra(ResultadoPreliminar, Alternativa(Condicao(2025, "MOTIVO_M", EnquadramentoDaModalidade("AMPLA")))));

        sem.Should().NotContain("enquadramento");
        com.Should().Contain("\"enquadramento\":[[{\"codigoDoDado\":\"MODALIDADE\",\"operador\":\"IGUAL\",\"valor\":\"AMPLA\"}]]");
    }

    [Fact(DisplayName = "O conversor grava o documento e lê de volta a mesma regra")]
    public void Conversor_IdaEVolta()
    {
        RegraDeHomologacaoDasInscricoes regra = Regra(ResultadoPreliminar, Alternativa(Condicao(2025, "MOTIVO_M", EnquadramentoDaModalidade("AMPLA"))));

        object? documento = ColunaDaRegraDeHomologacao.Conversor.ConvertToProvider(regra);
        object? lida = ColunaDaRegraDeHomologacao.Conversor.ConvertFromProvider(documento);

        documento.Should().BeOfType<string>();
        ((RegraDeHomologacaoDasInscricoes)lida!).MesmoConteudo(regra).Should().BeTrue();
    }

    [Fact(DisplayName = "O comparador distingue a ordem, o conteúdo e a ausência, e iguala o mesmo conteúdo")]
    public void Comparador_DistingueConteudoEOrdem()
    {
        RegraDeHomologacaoDasInscricoes umDois = Regra(ResultadoPreliminar, Alternativa(Condicao(2025, "MOTIVO_A", null), Condicao(2025, "MOTIVO_B", null)));
        RegraDeHomologacaoDasInscricoes doisUm = Regra(ResultadoPreliminar, Alternativa(Condicao(2025, "MOTIVO_B", null), Condicao(2025, "MOTIVO_A", null)));
        RegraDeHomologacaoDasInscricoes copia = ColunaDaRegraDeHomologacao.Ler(ColunaDaRegraDeHomologacao.Escrever(umDois));

        ColunaDaRegraDeHomologacao.Comparador.Equals(umDois, copia).Should().BeTrue();
        ColunaDaRegraDeHomologacao.Comparador.Equals(umDois, doisUm).Should().BeFalse("a ordem muda os motivos da inscrição");
        ColunaDaRegraDeHomologacao.Comparador.Equals(umDois, null).Should().BeFalse();
        ColunaDaRegraDeHomologacao.Comparador.Equals(null, null).Should().BeTrue();
    }

    [Fact(DisplayName = "O comparador dá o mesmo código ao mesmo conteúdo e código zero à ausência")]
    public void Comparador_CodigoAcompanhaOConteudo()
    {
        RegraDeHomologacaoDasInscricoes regra = Regra(ResultadoPreliminar, Alternativa(Condicao(2025, "MOTIVO_A", null)));
        RegraDeHomologacaoDasInscricoes copia = ColunaDaRegraDeHomologacao.Ler(ColunaDaRegraDeHomologacao.Escrever(regra));

        ColunaDaRegraDeHomologacao.Comparador.GetHashCode(regra).Should().Be(ColunaDaRegraDeHomologacao.Comparador.GetHashCode(copia));
        ColunaDaRegraDeHomologacao.Comparador.GetHashCode(null!).Should().Be(0);
    }

    [Theory(DisplayName = "Conteúdo gravado inválido é dado corrompido: a leitura lança")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"alternativas\":[{}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[]}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"CONDICAO_DESCONHECIDA\",\"edicaoDoExame\":2025}]}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"edicaoDoExame\":2025}]}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\",\"edicaoDoExame\":1997}]}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\"}]}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\",\"edicaoDoExame\":2025,\"motivoDesfavoravelCodigo\":\"motivo mal formado\"}]}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\",\"edicaoDoExame\":2025,\"enquadramento\":[[]]}]}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\",\"edicaoDoExame\":2025,\"enquadramento\":[[{\"codigoDoDado\":\"MODALIDADE\",\"operador\":\"INEXISTENTE\",\"valor\":\"AMPLA\"}]]}]}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\",\"edicaoDoExame\":2025,\"enquadramento\":[]}]}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\",\"edicaoDoExame\":2025,\"enquadrament\":[[{\"codigoDoDado\":\"MODALIDADE\",\"operador\":\"IGUAL\",\"valor\":\"AMPLA\"}]]}]}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\",\"edicaoDoExame\":2025,\"chaveDesconhecida\":1}]}]}")]
    [InlineData("{\"alternativas\":[{\"chaveDesconhecida\":1,\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\",\"edicaoDoExame\":2025}]}]}")]
    [InlineData("{\"chaveDesconhecida\":1,\"alternativas\":[]}")]
    [InlineData("{\"alternativas\":[],\"alternativas\":[]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\",\"edicaoDoExame\":2025,\"edicaoDoExame\":2024}]}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\",\"edicaoDoExame\":2025,\"enquadramento\":[[{\"codigoDoDado\":\"MODALIDADE\",\"operador\":\"IGUAL\",\"valor\":\"AMPLA\"}]],\"enquadramento\":[[{\"codigoDoDado\":\"MODALIDADE\",\"operador\":\"IGUAL\",\"valor\":\"AMPLA\"}]]}]}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\",\"edicaoDoExame\":2025,\"enquadramento\":[[{\"codigoDoDado\":\"MODALIDADE\",\"operador\":\"IGUAL\",\"valor\":\"AMPLA\",\"chaveDesconhecida\":1}]]}]}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\",\"edicaoDoExame\":2025,\"enquadramento\":[[{\"operador\":\"IGUAL\",\"valor\":\"AMPLA\"}]]}]}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\",\"edicaoDoExame\":2025,\"enquadramento\":[[{\"codigoDoDado\":\"MODALIDADE\",\"valor\":\"AMPLA\"}]]}]}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\",\"edicaoDoExame\":2025,\"enquadramento\":[[{\"codigoDoDado\":\"MODALIDADE\",\"operador\":\"IGUAL\"}]]}]}]}")]
    [InlineData("{\"alternativas\":[{\"condicoes\":[{\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\",\"edicaoDoExame\":2025,\"enquadramento\":[[{\"codigoDoDado\":\"MODALIDADE\",\"operador\":\"IGUAL\",\"valor\":\"AMPLA\",\"valor\":\"COTA\"}]]}]}]}")]
    public void ConteudoInvalido_Lanca(string documento)
    {
        Action leitura = () => ColunaDaRegraDeHomologacao.Ler(documento);

        leitura.Should().Throw<Exception>();
    }
    [Fact]
    public void Ler_DuasCondicoesInvalidas_IncluiAsDuasCausasNaMensagem()
    {
        const string documento = """{"alternativas":[{"condicoes":[{"tipo":"RESULTADO_NO_RETORNO_DO_INEP","edicaoDoExame":2025,"enquadramento":[[{"codigoDoDado":"DADO_A","operador":"ALGUM_DE","valor":true},{"codigoDoDado":"DADO_B","operador":"IGUAL","valor":[]}]]}]}]}""";
        Action leitura = () => ColunaDaRegraDeHomologacao.Ler(documento);

        leitura.Should().Throw<InvalidOperationException>().WithMessage(
            "Regra de homologação gravada inválida: Os operadores ALGUM_DE e NENHUM_DE exigem um array JSON, sem itens de texto em branco (array vazio é aceito). Os operadores IGUAL, DIFERENTE, MAIOR_IGUAL e MENOR_IGUAL exigem um valor JSON escalar não branco..");
    }
}

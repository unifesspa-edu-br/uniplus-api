namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using static Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport.RegraDeHomologacaoDeTeste;

/// <summary>
/// A regra de homologação das inscrições no envelope: ida e volta sem perda, ordem declarada preservada, o ausente como
/// <c>null</c> explícito e a leitura fechada, que recusa o que a escrita nunca produz.
/// </summary>
public sealed class RegraDeHomologacaoNoEnvelopeTests
{
    private const string Resultado = "RESULTADO_PRELIMINAR_HOMOLOGACAO";
    private const string CaminhoDaCondicao = "fase.regraDeHomologacao.alternativas[0].condicoes[0]";
    private const string ResultadoNulo = "\"resultadoAtoCodigo\":null";
    private const string Tipo = "\"tipo\":\"RESULTADO_NO_RETORNO_DO_INEP\"";
    private const string Edicao = "\"edicaoDoExame\":2025";
    private const string MotivoNulo = "\"motivoDesfavoravelCodigo\":null";
    private const string EnquadramentoNulo = "\"enquadramento\":null";

    [Fact(DisplayName = "Regra com e sem enquadramento volta igual ao que foi escrita")]
    public void RegraCompleta_VoltaIgual()
    {
        RegraDeHomologacaoDasInscricoes regra = Regra(
            Resultado,
            Alternativa(Condicao(2025, "MOTIVO_A", enquadramento: null), Condicao(2024, "MOTIVO_B", EnquadramentoDaModalidade("AMPLA"))),
            Alternativa(Condicao(2023, "MOTIVO_C", enquadramento: null)));

        RegraDeHomologacaoDasInscricoes? lida = IdaEVolta(regra);

        lida.Should().NotBeNull();
        lida!.MesmoConteudo(regra).Should().BeTrue();
    }

    [Fact(DisplayName = "O resultado e o motivo ausentes saem como null explícito e continuam ausentes na leitura")]
    public void ResultadoEMotivoAusentes_NullExplicito()
    {
        RegraDeHomologacaoDasInscricoes regra = Regra(resultadoAtoCodigo: null, Alternativa(Condicao(2025, motivo: null, enquadramento: null)));

        JsonObject escrita = RegraDeHomologacaoNoEnvelope.Serializar(regra);
        RegraDeHomologacaoDasInscricoes? lida = IdaEVolta(regra);

        escrita.ContainsKey("resultadoAtoCodigo").Should().BeTrue();
        escrita["resultadoAtoCodigo"].Should().BeNull();
        JsonObject condicao = escrita["alternativas"]![0]!["condicoes"]![0]!.AsObject();
        condicao.Select(static par => par.Key).Should().BeEquivalentTo("tipo", "edicaoDoExame", "motivoDesfavoravelCodigo", "enquadramento");
        condicao["motivoDesfavoravelCodigo"].Should().BeNull();
        lida!.ResultadoAtoCodigo.Should().BeNull();
        lida.Alternativas[0].Condicoes[0].MotivoDesfavoravelCodigo.Should().BeNull();
    }

    [Fact(DisplayName = "A regra sem alternativa volta sem alternativa")]
    public void SemAlternativa_VoltaSemAlternativa()
    {
        RegraDeHomologacaoDasInscricoes? lida = IdaEVolta(Regra(Resultado));

        lida!.HomologaTodaInscricaoEnviada.Should().BeTrue();
        lida.ResultadoAtoCodigo.Should().Be(Resultado);
    }

    [Fact(DisplayName = "A ordem declarada das alternativas e das condições é preservada, sem ordenar")]
    public void OrdemDeclarada_EPreservada()
    {
        RegraDeHomologacaoDasInscricoes regra = Regra(
            Resultado,
            Alternativa(Condicao(2026, "MOTIVO_C", null), Condicao(2024, "MOTIVO_A", null), Condicao(2025, "MOTIVO_B", null)),
            Alternativa(Condicao(2023, "MOTIVO_Z", null)));

        JsonObject escrita = RegraDeHomologacaoNoEnvelope.Serializar(regra);
        RegraDeHomologacaoDasInscricoes? lida = IdaEVolta(regra);

        escrita["alternativas"]![0]!["condicoes"]!.AsArray().Select(static c => c!["motivoDesfavoravelCodigo"]!.GetValue<string>())
            .Should().Equal("MOTIVO_C", "MOTIVO_A", "MOTIVO_B");
        lida!.Alternativas.Select(static a => a.Condicoes.Count).Should().Equal(3, 1);
        lida.Alternativas[0].Condicoes.Select(static c => c.EdicaoDoExame).Should().Equal(2026, 2024, 2025);
    }

    [Fact(DisplayName = "Mudar a ordem das condições muda os bytes canônicos")]
    public void OrdemDasCondicoes_MudaOsBytes()
    {
        RegraDeHomologacaoDasInscricoes umDois = Regra(Resultado, Alternativa(Condicao(2025, "MOTIVO_A", null), Condicao(2025, "MOTIVO_B", null)));
        RegraDeHomologacaoDasInscricoes doisUm = Regra(Resultado, Alternativa(Condicao(2025, "MOTIVO_B", null), Condicao(2025, "MOTIVO_A", null)));

        Bytes(umDois).Should().NotEqual(Bytes(doisUm));
    }

    [Fact(DisplayName = "O enquadramento sai na forma canônica do predicado: as cláusulas declaradas em outra ordem dão os mesmos bytes")]
    public void Enquadramento_FormaCanonicaDoPredicado()
    {
        RegraDeHomologacaoDasInscricoes umDois = Regra(Resultado, Alternativa(Condicao(2025, "MOTIVO_A", EnquadramentoDaModalidade("AMPLA", "PCD"))));
        RegraDeHomologacaoDasInscricoes doisUm = Regra(Resultado, Alternativa(Condicao(2025, "MOTIVO_A", EnquadramentoDaModalidade("PCD", "AMPLA"))));

        Bytes(umDois).Should().Equal(Bytes(doisUm));
    }

    [Fact(DisplayName = "O enquadramento ausente sai como null explícito e o presente sai como o predicado")]
    public void Enquadramento_AusenteSaiNulo()
    {
        JsonObject sem = RegraDeHomologacaoNoEnvelope.Serializar(Regra(Resultado, Alternativa(Condicao(2025, "MOTIVO_A", null))));
        JsonObject com = RegraDeHomologacaoNoEnvelope.Serializar(Regra(Resultado, Alternativa(Condicao(2025, "MOTIVO_A", EnquadramentoDaModalidade("AMPLA")))));

        JsonObject condicaoSem = sem["alternativas"]![0]!["condicoes"]![0]!.AsObject();
        condicaoSem.ContainsKey("enquadramento").Should().BeTrue();
        condicaoSem["enquadramento"].Should().BeNull();
        com["alternativas"]![0]!["condicoes"]![0]!["enquadramento"].Should().BeOfType<JsonArray>();
    }

    [Fact(DisplayName = "O texto da regra sai normalizado em NFC")]
    public void Texto_SaiEmNfc()
    {
        string decomposto = "RESULTADO_É";

        JsonObject escrita = RegraDeHomologacaoNoEnvelope.Serializar(Regra(decomposto));

        escrita["resultadoAtoCodigo"]!.GetValue<string>().Should().Be("RESULTADO_É");
    }

    [Fact(DisplayName = "A fase com a regra nula é lida como fase que não homologa")]
    public void RegraNula_FaseNaoHomologa()
    {
        LeitorEnvelope leitor = new();
        JsonObject fase = new() { [RegraDeHomologacaoNoEnvelope.Chave] = null };

        RegraDeHomologacaoDasInscricoes? lida = RegraDeHomologacaoNoEnvelope.Ler(leitor, fase, "fase");

        lida.Should().BeNull();
        leitor.Falhou.Should().BeFalse();
    }

    [Fact(DisplayName = "O resultado, o motivo e o enquadramento nulos são aceitos: é como a escrita grava o ausente")]
    public void NulosExplicitos_Aceitos()
    {
        string condicao = "{" + Tipo + "," + Edicao + "," + MotivoNulo + "," + EnquadramentoNulo + "}";
        LeitorEnvelope leitor = new();
        JsonObject fase = new() { [RegraDeHomologacaoNoEnvelope.Chave] = JsonNode.Parse("{" + ResultadoNulo + ",\"alternativas\":[{\"condicoes\":[" + condicao + "]}]}") };

        RegraDeHomologacaoDasInscricoes? lida = RegraDeHomologacaoNoEnvelope.Ler(leitor, fase, "fase");

        leitor.Falhou.Should().BeFalse(leitor.Falhou ? leitor.Erro.Message : null);
        lida!.ResultadoAtoCodigo.Should().BeNull();
    }

    [Theory(DisplayName = "A leitura recusa, como envelope malformado, a regra fora da forma que a escrita produz")]
    [InlineData("{" + ResultadoNulo + ",\"alternativas\":[],\"intrusa\":1}", "chave desconhecida na regra")]
    [InlineData("{" + ResultadoNulo + "}", "sem as alternativas")]
    [InlineData("{\"alternativas\":[]}", "sem a chave do resultado")]
    [InlineData("{" + ResultadoNulo + ",\"alternativas\":{}}", "alternativas que não são lista")]
    [InlineData("{\"resultadoAtoCodigo\":\"\",\"alternativas\":[]}", "resultado vazio")]
    [InlineData("{" + ResultadoNulo + ",\"alternativas\":[{}]}", "alternativa sem a chave das condições")]
    [InlineData("{" + ResultadoNulo + ",\"alternativas\":[{\"condicoes\":[]}]}", "alternativa sem condição")]
    [InlineData("{" + ResultadoNulo + ",\"alternativas\":[{\"condicoes\":[],\"intrusa\":1}]}", "chave desconhecida na alternativa")]
    public void RegraForaDaForma_Recusa(string regra, string defeito) =>
        ConferirRecusa(LerRegra(regra), defeito);

    [Theory(DisplayName = "A leitura recusa, como envelope malformado, a condição fora da forma que a escrita produz")]
    [InlineData("{" + Tipo + "," + Edicao + "," + MotivoNulo + "," + EnquadramentoNulo + ",\"intrusa\":1}", "chave desconhecida na condição")]
    [InlineData("{\"tipo\":\"CONDICAO_QUE_NAO_EXISTE\"," + Edicao + "," + MotivoNulo + "," + EnquadramentoNulo + "}", "tipo que o sistema não apura")]
    [InlineData("{" + Edicao + "," + MotivoNulo + "," + EnquadramentoNulo + "}", "sem o tipo")]
    [InlineData("{" + Tipo + "," + MotivoNulo + "," + EnquadramentoNulo + "}", "sem a edição do exame")]
    [InlineData("{" + Tipo + ",\"edicaoDoExame\":1997," + MotivoNulo + "," + EnquadramentoNulo + "}", "edição antes de 1998")]
    [InlineData("{" + Tipo + ",\"edicaoDoExame\":2101," + MotivoNulo + "," + EnquadramentoNulo + "}", "edição depois de 2100")]
    [InlineData("{" + Tipo + "," + Edicao + ",\"motivoDesfavoravelCodigo\":\"motivo mal formado\"," + EnquadramentoNulo + "}", "motivo mal formado")]
    [InlineData("{" + Tipo + "," + Edicao + ",\"motivoDesfavoravelCodigo\":\"\"," + EnquadramentoNulo + "}", "motivo vazio")]
    [InlineData("{" + Tipo + "," + Edicao + "," + EnquadramentoNulo + "}", "sem a chave do motivo")]
    [InlineData("{" + Tipo + "," + Edicao + "," + MotivoNulo + "}", "sem a chave do enquadramento")]
    [InlineData("{" + Tipo + "," + Edicao + "," + MotivoNulo + ",\"enquadramento\":[]}", "enquadramento vazio")]
    [InlineData("{" + Tipo + "," + Edicao + "," + MotivoNulo + ",\"enquadramento\":[[{\"fato\":\"MODALIDADE\",\"operador\":\"INEXISTENTE\",\"valor\":\"AMPLA\"}]]}", "operador do enquadramento desconhecido")]
    public void CondicaoForaDaForma_Recusa(string condicao, string defeito) =>
        ConferirRecusa(LerRegra("{" + ResultadoNulo + ",\"alternativas\":[{\"condicoes\":[" + condicao + "]}]}"), defeito);

    [Fact(DisplayName = "A recusa nomeia o campo da condição que o domínio recusou")]
    public void Recusa_NomeiaOCampo()
    {
        LeitorEnvelope leitor = LerRegra(
            "{" + ResultadoNulo + ",\"alternativas\":[{\"condicoes\":[{" + Tipo + ",\"edicaoDoExame\":1997," + MotivoNulo + "," + EnquadramentoNulo + "}]}]}");

        leitor.Erro.Message.Should().Contain($"{CaminhoDaCondicao}.edicaoDoExame");
    }

    private static RegraDeHomologacaoDasInscricoes? IdaEVolta(RegraDeHomologacaoDasInscricoes regra)
    {
        JsonObject fase = new() { [RegraDeHomologacaoNoEnvelope.Chave] = RegraDeHomologacaoNoEnvelope.Serializar(regra) };
        LeitorEnvelope leitor = new();

        RegraDeHomologacaoDasInscricoes? lida = RegraDeHomologacaoNoEnvelope.Ler(leitor, fase, "fase");

        leitor.Falhou.Should().BeFalse(leitor.Falhou ? leitor.Erro.Message : null);
        return lida;
    }

    private static LeitorEnvelope LerRegra(string regra)
    {
        JsonObject fase = new() { [RegraDeHomologacaoNoEnvelope.Chave] = JsonNode.Parse(regra) };
        LeitorEnvelope leitor = new();

        RegraDeHomologacaoNoEnvelope.Ler(leitor, fase, "fase").Should().BeNull();

        return leitor;
    }

    private static void ConferirRecusa(LeitorEnvelope leitor, string defeito)
    {
        leitor.Falhou.Should().BeTrue($"o defeito '{defeito}' não é a forma que a escrita produz");
        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    private static byte[] Bytes(RegraDeHomologacaoDasInscricoes regra) =>
        PerfilCanonicoV1.Instancia.Serializar(RegraDeHomologacaoNoEnvelope.Serializar(regra));
}

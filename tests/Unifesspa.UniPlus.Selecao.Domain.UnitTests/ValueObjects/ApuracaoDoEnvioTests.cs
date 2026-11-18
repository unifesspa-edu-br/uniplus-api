namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ApuracaoDoEnvioTests
{
    [Fact]
    public void Construir_ColecoesAlteradasDepois_PreservaValoresApurados()
    {
        Dictionary<string, JsonElement> derivados = new() { ["UF_RESIDENCIA"] = JsonSerializer.SerializeToElement("PA") };
        Dictionary<string, DefinicaoDaResposta> definicoes = new() { ["UF_RESIDENCIA"] = new(TipoDoValorDeResposta.Codigo, false, false) };
        List<ArquivoConfirmado> arquivos = [];
        ApuracaoDoEnvio apuracao = new(FormularioDeTeste.AvaliacaoSemOcultos, derivados, definicoes, arquivos);
        derivados.Clear();
        definicoes.Clear();
        arquivos.Add(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, FormatoPermitido.Pdf, 1, "hash", DateTimeOffset.UnixEpoch));

        apuracao.Derivados["UF_RESIDENCIA"].GetString().Should().Be("PA");
        apuracao.DefinicoesDosDerivados.Should().ContainKey("UF_RESIDENCIA");
        apuracao.ArquivosConfirmados.Should().BeEmpty();
    }

    [Fact]
    public void RecusasDosDerivados_TipoRecusado_IndicaOFatoSemEcoarValor()
    {
        ApuracaoDoEnvio apuracao = new(FormularioDeTeste.AvaliacaoSemOcultos,
            new Dictionary<string, JsonElement> { ["UF_RESIDENCIA"] = JsonSerializer.SerializeToElement(1234) },
            new Dictionary<string, DefinicaoDaResposta> { ["UF_RESIDENCIA"] = new(TipoDoValorDeResposta.Codigo, false, false) }, []);

        apuracao.RecusasDosDerivados().Should().ContainSingle().Which.Field.Should().Be("UF_RESIDENCIA");
        apuracao.RecusasDosDerivados().Single().Error.Message.Should().NotContain("1234");
    }

    [Fact]
    public void DaAvaliacao_ListasAlteradasDepois_PreservaModalidadesDoEnvio()
    {
        List<ModalidadeDeConcorrencia> itens = [new("AC", "Ampla concorrência", 0)];
        List<string> fatos = ["CONCORRER_PCD"];
        ResultadoDaAvaliacaoDaInscricao avaliacao = FormularioDeTeste.AvaliacaoSemOcultos with
        {
            Modalidades = new(true, SituacaoDasModalidades.Concorre, itens, [], new(fatos, [])),
        };
        ModalidadesDoEnvio modalidades = ModalidadesDoEnvio.DaAvaliacao(avaliacao, OpcoesDeCursoDaInscricao.Reidratar(null, null));
        itens.Clear();
        fatos.Clear();

        modalidades.Primeira.Itens.Should().ContainSingle().Which.Codigo.Should().Be("AC");
        modalidades.Primeira.Dependencias.Fatos.Should().Equal("CONCORRER_PCD");
    }
}

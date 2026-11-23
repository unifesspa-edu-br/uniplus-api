namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConferenciaDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

public sealed class CadastrosDaConfiguracaoTests
{
    [Fact]
    public void Regras_Recebidas_GuardamUmaCopia()
    {
        CadastrosDaConfiguracao padrao = CadastrosDeTeste.Padrao;
        ReferenciaRegra referencia = ReferenciaRegra.De("REGRA", "v1", new string('a', 64));
        Dictionary<(string Codigo, string Versao), RegraDoCatalogoCitada> regras =
            new() { [("REGRA", "v1")] = new(TipoRegra.CriterioDesempate, referencia) };
        CadastrosDaConfiguracao cadastros = new(
            padrao.ContagemDePrazos, padrao.FatosDeModalidade, padrao.TiposDeAtoUnicos,
            padrao.Corretores, padrao.Protecao, padrao.MotivosEscolhidos)
        { Regras = regras, MunicipiosDoGeo = padrao.MunicipiosDoGeo, Precedencias = padrao.Precedencias };

        regras.Clear();

        cadastros.Regras.Should().ContainSingle();
    }

    [Fact]
    public void Regras_NaoInformadas_NascemVazias() => CadastrosDeTeste.Padrao.Regras.Should().BeEmpty();
}

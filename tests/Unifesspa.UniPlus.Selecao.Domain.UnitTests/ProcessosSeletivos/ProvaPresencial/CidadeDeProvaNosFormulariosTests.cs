namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ProvaPresencial;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class CidadeDeProvaNosFormulariosTests
{
    [Theory]
    [InlineData(true, true, null)]
    [InlineData(true, false, ProvaPresencialErrorCodes.InscricaoSemCidadeDeProva)]
    [InlineData(false, true, ProvaPresencialErrorCodes.CidadeDeProvaSemProvaPresencial)]
    [InlineData(false, false, null)]
    public void Conferir_DeclaracaoECampo_AvaliaCoerencia(bool temProva, bool temCampo, string? codigo)
    {
        CidadeDeProvaBuilder builder = new CidadeDeProvaBuilder().ComDeclaracao(temProva);
        if (temCampo)
        {
            builder.ComCampo(Obrigatoriedade.Sempre);
        }
        ProcessoSeletivo processo = builder.Construir();
        CidadeDeProvaNosFormularios.Conferir(processo.Declaracao()).Select(e => e.Error.Code)
            .Should().Equal(codigo is null ? [] : [codigo]);
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Single(i => i.Codigo == "cidade_de_prova_nos_formularios").Ok.Should().Be(codigo is null);
    }

    [Fact]
    public void Conferir_ProvaPorDeclarar_NaoRecusaCampo()
    {
        ProcessoSeletivo processo = new CidadeDeProvaBuilder().ComCampo(Obrigatoriedade.Sempre).Construir();
        CidadeDeProvaNosFormularios.Conferir(processo.Declaracao()).Should().BeEmpty();
    }

    [Fact]
    public void Conferir_CampoOpcional_Recusa()
    {
        ProcessoSeletivo processo = new CidadeDeProvaBuilder().ComDeclaracao(true).ComCampo(Obrigatoriedade.Nunca).Construir();
        CidadeDeProvaNosFormularios.Conferir(processo.Declaracao()).Should().ContainSingle().Which.Error.Code
            .Should().Be(ProvaPresencialErrorCodes.InscricaoSemCidadeDeProva);
    }

    [Fact]
    public void Conferir_CampoComExibicaoCondicional_Recusa()
    {
        PredicadoDnf seCasado = ExibicaoDeTeste.Quando("ESTADO_CIVIL", Operador.Igual, "CASADO");
        ProcessoSeletivo processo = new CidadeDeProvaBuilder().ComDeclaracao(true).ComCampo(Obrigatoriedade.Sempre, seCasado).Construir();
        CidadeDeProvaNosFormularios.Conferir(processo.Declaracao()).Should().ContainSingle().Which.Error.Code
            .Should().Be(ProvaPresencialErrorCodes.InscricaoSemCidadeDeProva);
    }

    [Fact]
    public void Conferir_ProvaSemFormularioDeInscricao_NaoRecusa()
    {
        ProcessoSeletivo processo = new CidadeDeProvaBuilder(comFormularioDeInscricao: false).ComDeclaracao(true).Construir();
        CidadeDeProvaNosFormularios.Conferir(processo.Declaracao()).Should().BeEmpty();
    }

    [Fact]
    public void Causas_ProvaPresencial_MunicipioNoGeoVemAntesDaCidadeDeProva()
    {
        ProcessoSeletivo processo = new CidadeDeProvaBuilder().ComDeclaracao(true).Construir();
        string[] esperados = [ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo, ProvaPresencialErrorCodes.InscricaoSemCidadeDeProva];
        CausasQueImpedemCongelar.De(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Select(e => e.Error.Code)
            .Where(esperados.Contains).Should().Equal(esperados);
    }
}

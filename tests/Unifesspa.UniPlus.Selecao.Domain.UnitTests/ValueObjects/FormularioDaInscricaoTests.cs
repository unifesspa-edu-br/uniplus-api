namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class FormularioDaInscricaoTests
{
    [Fact]
    public void Equals_FormulariosIndependentesComMesmoConteudo_SaoIguais()
    {
        FormularioDaInscricao primeiro = Completo();
        FormularioDaInscricao segundo = Completo();
        primeiro.Should().Be(segundo);
        primeiro.GetHashCode().Should().Be(segundo.GetHashCode());
    }

    [Theory]
    [InlineData("versao")]
    [InlineData("derivado")]
    [InlineData("definicao")]
    [InlineData("preenchimento")]
    public void Equals_ConteudoDiferente_SaoDiferentes(string alteracao)
    {
        Completo().Should().NotBe(Completo(alteracao));
    }

    private static FormularioDaInscricao Completo(string? alteracao = null) => FormularioDaInscricao.Reidratar(
        alteracao == "versao" ? Guid.NewGuid() : Guid.Empty,
        PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null),
            new Dictionary<string, JsonElement> { ["A"] = JsonSerializer.SerializeToElement(alteracao == "preenchimento" ? 2 : 1) },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> { ["G"] = [new("1", new Dictionary<string, JsonElement> { ["B"] = JsonSerializer.SerializeToElement(1) })] },
            [new(FormularioDeTeste.CodigoDaInscricao, "T", Guid.Empty, "hash")]),
        new Dictionary<string, JsonElement> { ["D"] = JsonSerializer.SerializeToElement(alteracao == "derivado" ? 2 : 1) },
        new Dictionary<string, DefinicaoDaResposta> { ["A"] = new(TipoDoValorDeResposta.Texto, alteracao == "definicao", false) });

    [Fact]
    public void Reidratar_ColecoesAlteradas_PreservaOsDados()
    {
        Dictionary<string, JsonElement> derivados = new() { ["A"] = JsonSerializer.SerializeToElement(1) };
        Dictionary<string, DefinicaoDaResposta> definicoes = new() { ["A"] = new(TipoDoValorDeResposta.Texto, false, false) };
        PreenchimentoDoCandidato preenchimento = PreenchimentoDoCandidato.Criar(
            OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), []);
        FormularioDaInscricao formulario = FormularioDaInscricao.Reidratar(Guid.NewGuid(), preenchimento, derivados, definicoes);
        derivados.Clear();
        definicoes.Clear();
        formulario.Derivados["A"].GetInt32().Should().Be(1);
        formulario.DefinicoesDasRespostas.Should().ContainKey("A");
        ((IDictionary<string, JsonElement>)formulario.Derivados).Invoking(d => d.Clear()).Should().Throw<NotSupportedException>();
    }
    [Fact]
    public void Reidratar_MesmoConteudo_TemIgualdadeDeValor()
    {
        PreenchimentoDoCandidato preenchimento = PreenchimentoDoCandidato.Criar(
            OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), []);
        Guid versao = Guid.NewGuid();
        Dictionary<string, JsonElement> derivados = new() { ["A"] = JsonSerializer.SerializeToElement(1) };
        Dictionary<string, DefinicaoDaResposta> definicoes = new();
        FormularioDaInscricao primeiro = FormularioDaInscricao.Reidratar(versao, preenchimento, derivados, definicoes);
        FormularioDaInscricao segundo = FormularioDaInscricao.Reidratar(versao, preenchimento, derivados, definicoes);
        primeiro.Should().Be(segundo);
        primeiro.GetHashCode().Should().Be(segundo.GetHashCode());
    }

}

namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O metadado dos fatos dos gatilhos e a operação dos agregados que a versão nova congela não
/// trocam o que a versão publicada congelou (UNI-REQ-0144); texto de valor não é eixo.
/// </summary>
public sealed class ConferenciaDosEixosDosFatosColetadosTests
{
    private static readonly FatosDasVersoesPublicadas Publicadas = FatosDasVersoesPublicadas.De(
        [Testes.Compartilhado.VersoesPublicadasDeTeste.Versao([], [Item("SEXO")])],
        [new EixosDeVersaoPublicada(
            new Dictionary<string, EixosDoMetadadoCongelado> { ["SEXO"] = new("CATEGORICO", "ESCALAR", "INSCRICAO") },
            new Dictionary<string, string>())]);

    [Fact(DisplayName = "Troca de domínio do fato citado por condição de documento recusa e nomeia o fato e o eixo")]
    public void TrocaDeDominio_Recusa()
    {
        DomainError? erro = ConferenciaDosEixosDosFatosColetados.Conferir(Publicadas, Metadado("BOOLEANO", "Sim"), []);

        erro.Should().NotBeNull();
        erro!.Code.Should().Be("RascunhoRetificacao.EixoDoFatoJaColetadoAlterado");
        erro.Message.Should().Contain("SEXO").And.Contain("domínio").And.Contain("processo novo");
    }

    [Fact(DisplayName = "Troca da descrição de um valor de domínio passa: não é eixo")]
    public void TrocaDeDescricaoDeValor_Passa()
    {
        ConferenciaDosEixosDosFatosColetados.Conferir(Publicadas, Metadado("CATEGORICO", "Descrição nova"), []).Should().BeNull();
    }

    [Fact(DisplayName = "Troca da operação do agregado recusa falando do resumo de grupo, não de fato")]
    public void TrocaDeOperacaoDoAgregado_RecusaNomeandoOResumoDeGrupo()
    {
        FatosDasVersoesPublicadas publicadas = FatosDasVersoesPublicadas.De(
            [Testes.Compartilhado.VersoesPublicadasDeTeste.Versao([], [Item("SEXO")])],
            [new EixosDeVersaoPublicada(new Dictionary<string, EixosDoMetadadoCongelado>(), new Dictionary<string, string> { ["RENDA_FAMILIAR"] = "EXISTE" })]);

        DomainError? erro = ConferenciaDosEixosDosFatosColetados.Conferir(
            publicadas, null, [new DefinicaoAgregado("RENDA_FAMILIAR", "G", "M", OperacaoAgregado.ValoresPresentes)]);

        erro!.Message.Should().StartWith("O resumo de grupo 'RENDA_FAMILIAR'").And.Contain("'EXISTE' para 'VALORES_PRESENTES'").And.NotContain("O fato");
    }

    [Fact(DisplayName = "A origem dos valores congelada no item é o token canônico da fonte, não o nome do enum")]
    public void OrigemDosValores_GravaOTokenCanonico()
    {
        EixosDoItemCongelado.De(Item("SEXO")).OrigemValores.Should().Be("CATALOGO");
        Domain.Enums.OrigemValoresColeta.OpcoesDoProcesso.ToCodigo().Should().Be("PROCESSO");
        Domain.Enums.OrigemValoresColeta.MunicipiosDoBonus.ToCodigo().Should().Be("MUNICIPIOS_BONUS");
        Domain.Enums.OrigemValoresColeta.UnidadesFederativas.ToCodigo().Should().Be("GEO_UF");
    }

    private static Dictionary<string, MetadadoFatoCongelado> Metadado(string dominio, string descricao) =>
        new() { ["SEXO"] = new("SEXO", dominio, "DECLARADO", "ESCALAR", "INSCRICAO", "NENHUM", ["F"], [new ValorDominioDeclaradoCongelado("F", descricao, 0)]) };

    private static Domain.Entities.FatoColetado Item(string codigo) =>
        Domain.Entities.FatoColetado.Criar(codigo, 0, codigo, Unifesspa.UniPlus.Regras.Enums.TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL").Value!;
}

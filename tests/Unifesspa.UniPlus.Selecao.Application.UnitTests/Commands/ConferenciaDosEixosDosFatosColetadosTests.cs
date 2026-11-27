namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O metadado dos fatos dos gatilhos e a operação dos agregados que a versão nova congela não
/// trocam o que a versão publicada congelou (UNI-REQ-0144); texto de valor não é eixo.
/// </summary>
public sealed class ConferenciaDosEixosDosFatosColetadosTests
{
    private static readonly FatosDasVersoesPublicadas Publicadas = FatosDasVersoesPublicadas.De(
        [Testes.Compartilhado.VersoesPublicadasDeTeste.Versao([], [Item("SEXO")])],
        [new EixosDeVersaoPublicada(
            new Dictionary<string, EixosDoMetadadoCongelado> { ["SEXO"] = new(TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, "INSCRICAO") },
            new Dictionary<string, string>())]);

    [Fact(DisplayName = "Troca de domínio do fato citado por condição de documento recusa e nomeia o fato e o eixo")]
    public void TrocaDeDominio_Recusa()
    {
        IReadOnlyList<FieldError> erros = ConferenciaDosEixosDosFatosColetados.Conferir(Publicadas, [], Metadado(TipoDoDado.SimOuNao, "Sim"), []);

        DomainError erro = erros.Should().ContainSingle().Subject.Error;
        erro.Code.Should().Be("Retificacao.EixoDoFatoJaColetadoAlterado");
        erro.Message.Should().Contain("SEXO").And.Contain("domínio").And.Contain("processo novo");
    }

    [Fact(DisplayName = "Troca da descrição de um valor de domínio passa: não é eixo")]
    public void TrocaDeDescricaoDeValor_Passa()
    {
        ConferenciaDosEixosDosFatosColetados.Conferir(Publicadas, [], Metadado(TipoDoDado.Opcao, "Descrição nova"), []).Should().BeEmpty();
    }

    [Fact(DisplayName = "Troca da operação do agregado recusa falando do resumo de grupo, não de fato")]
    public void TrocaDeOperacaoDoAgregado_RecusaNomeandoOResumoDeGrupo()
    {
        FatosDasVersoesPublicadas publicadas = FatosDasVersoesPublicadas.De(
            [Testes.Compartilhado.VersoesPublicadasDeTeste.Versao([], [Item("SEXO")])],
            [new EixosDeVersaoPublicada(new Dictionary<string, EixosDoMetadadoCongelado>(), new Dictionary<string, string> { ["RENDA_FAMILIAR"] = "EXISTE" })]);

        IReadOnlyList<FieldError> erros = ConferenciaDosEixosDosFatosColetados.Conferir(
            publicadas, [], null, [new DefinicaoAgregado("RENDA_FAMILIAR", "G", "M", OperacaoAgregado.ValoresPresentes)]);

        erros.Should().ContainSingle().Which.Error.Message.Should().StartWith("O resumo de grupo 'RENDA_FAMILIAR'").And.Contain("valor publicado 'EXISTE'").And.NotContain("VALORES_PRESENTES").And.NotContain("O fato");
    }

    [Fact(DisplayName = "A origem dos valores congelada no item é o código do enum, não o nome do membro")]
    public void OrigemDosValores_GravaOTokenCanonico()
    {
        EixosDoItemCongelado.De(Item("SEXO")).OrigemValores.Should().Be("CATALOGO");
        Domain.Enums.OrigemValoresColeta.OpcoesDoProcesso.Codigo().Should().Be("OPCOES_DO_PROCESSO");
        Domain.Enums.OrigemValoresColeta.MunicipiosDoFada.Codigo().Should().Be("MUNICIPIOS_DO_FADA");
        Domain.Enums.OrigemValoresColeta.UnidadesFederativas.Codigo().Should().Be("UNIDADES_FEDERATIVAS");
    }

    private static Dictionary<string, MetadadoFatoCongelado> Metadado(TipoDoDado dominio, string descricao) =>
        new() { ["SEXO"] = new("SEXO", dominio, OrigemFato.Declarado, QuantidadeDeValores.ValorUnico, "INSCRICAO", "NENHUM", ["F"], [new ValorDominioDeclaradoCongelado("F", descricao, 0)]) };

    private static Domain.Entities.FatoColetado Item(string codigo) =>
        FatoColetadoDeTeste.Coletando(codigo, 0).ComObrigatoriedade(Obrigatoriedade.Sempre).Construir();
}

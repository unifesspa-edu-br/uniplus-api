namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;
using Unifesspa.UniPlus.Regras.Serializacao;

using static Unifesspa.UniPlus.Regras.UnitTests.Formularios.PortavelDeTeste;

/// <summary>
/// O formulário portável vindo de fora — um arquivo importado, um caso do corpus compartilhado —: a avaliação e a
/// oferta, que a conversão de ida não exercita, porque a definição montada pelo módulo nunca chega assim.
/// </summary>
public sealed class FormularioPortavelTests
{
    [Fact]
    public void PredicadoSemClausula_ContinuaFalso_ENaoViraSemCondicao()
    {
        FormularioPortavel formulario = Formulario(Item("DETALHE", exibicao: []));

        AvaliacaoFormulario avaliacao = Avaliar(formulario, Respostas());

        avaliacao.Itens.Single().Visivel.Should().Be(Ternario.Falso, "a lista vazia é o predicado sem cláusula, que avalia falso");
    }

    [Fact]
    public void RespostaForaDaOferta_NaoVale()
    {
        FormularioPortavel formulario = Formulario(
            Item("TIPO_ENDERECO", oferta: ["URBANO", "ALDEIA"]),
            Item("NOME_ALDEIA", exibicao: [[new CondicaoPrecondicaoDto("TIPO_ENDERECO", "IGUAL", Texto("ALDEIA"))]]));

        AvaliacaoFormulario dentro = Avaliar(formulario, Respostas(("TIPO_ENDERECO", Texto("ALDEIA"))));
        AvaliacaoFormulario fora = Avaliar(formulario, Respostas(("TIPO_ENDERECO", Texto("QUILOMBO"))));

        dentro.Itens[1].Visivel.Should().Be(Ternario.Verdadeiro);
        fora.Fatos["TIPO_ENDERECO"].Estado.Should().Be(EstadoFato.Indeterminado, "o código fora da oferta é descartado como se não houvesse resposta");
    }
}

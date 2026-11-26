namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A consequência da falta de envio é uma declaração opcional do formulário: a leitura do código recusa o que não é do
/// vocabulário, e a definição do formulário guarda, troca e retira a declaração, sem tocar nos demais formulários.
/// </summary>
public sealed class ConsequenciaDaFaltaDeEnvioNoFormularioTests
{
    [Fact]
    public void Ler_SemCodigo_NaoTemConsequencia()
    {
        Result<ConsequenciaDoDescumprimento?> lida = LeituraDaConsequenciaDaFaltaDeEnvio.Ler(null, FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Habilitacao));

        lida.IsSuccess.Should().BeTrue();
        lida.Value.Should().BeNull();
    }

    [Theory]
    [InlineData("ELIMINA_DO_PROCESSO", ConsequenciaDoDescumprimento.EliminaDoProcesso)]
    [InlineData("RETIRA_A_MODALIDADE", ConsequenciaDoDescumprimento.RetiraAModalidade)]
    [InlineData("RETIRA_O_ACRESCIMO", ConsequenciaDoDescumprimento.RetiraOAcrescimo)]
    public void Ler_CodigoDoVocabulario_DevolveAConsequencia(string codigo, ConsequenciaDoDescumprimento esperada)
    {
        LeituraDaConsequenciaDaFaltaDeEnvio.Ler(codigo, FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Habilitacao)).Value.Should().Be(esperada);
    }

    [Theory]
    [InlineData("ABRE_PRAZO_PARA_REENVIO")]
    [InlineData("elimina_do_processo")]
    [InlineData("")]
    public void Ler_CodigoForaDoVocabulario_RecusaCitandoOFormulario(string codigo)
    {
        Result<ConsequenciaDoDescumprimento?> lida = LeituraDaConsequenciaDaFaltaDeEnvio.Ler(codigo, FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Habilitacao));

        FieldError recusa = lida.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("consequenciaDaFaltaDeEnvio");
        recusa.Error.Code.Should().Be(FormularioProcessoErrorCodes.ConsequenciaDaFaltaDeEnvioInvalida);
        recusa.Error.Message.Should().Contain("'HABILITACAO'");
    }

    [Fact]
    public void DefinirFormulario_ComConsequencia_GuardaADeclaracaoNoFormulario()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        processo.DefinirFormulario(PapelDoFormularioDeTeste.Habilitacao, null, null, FormularioDeTeste.Paginas(), null, ConsequenciaDoDescumprimento.EliminaDoProcesso)
            .IsSuccess.Should().BeTrue();

        processo.FormularioDe(FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Habilitacao))!.ConsequenciaDaFaltaDeEnvio.Should().Be(ConsequenciaDoDescumprimento.EliminaDoProcesso);
    }

    [Fact]
    public void DefinirFormulario_SemConsequencia_RetiraADeclaracaoAnterior()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirFormulario(PapelDoFormularioDeTeste.Habilitacao, null, null, FormularioDeTeste.Paginas(), null, ConsequenciaDoDescumprimento.EliminaDoProcesso)
            .IsSuccess.Should().BeTrue();

        processo.DefinirFormulario(PapelDoFormularioDeTeste.Habilitacao, null, null, FormularioDeTeste.Paginas()).IsSuccess.Should().BeTrue();

        processo.FormularioDe(FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Habilitacao))!.ConsequenciaDaFaltaDeEnvio.Should().BeNull();
    }

    [Fact]
    public void DefinirFormulario_ConsequenciaDeUmFormulario_NaoMudaADeOutro()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirFormulario(PapelDoFormularioDeTeste.Habilitacao, null, null, FormularioDeTeste.Paginas(), null, ConsequenciaDoDescumprimento.EliminaDoProcesso)
            .IsSuccess.Should().BeTrue();

        processo.DefinirFormulario(PapelDoFormularioDeTeste.IsencaoTaxa, null, null, FormularioDeTeste.Paginas()).IsSuccess.Should().BeTrue();

        processo.FormularioDe(FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Habilitacao))!.ConsequenciaDaFaltaDeEnvio.Should().Be(ConsequenciaDoDescumprimento.EliminaDoProcesso);
        processo.FormularioDe(PapelDoFormularioDeTeste.IsencaoTaxa)!.ConsequenciaDaFaltaDeEnvio.Should().BeNull();
    }

    [Fact]
    public void DefinirFormulario_FormularioRecusado_NaoGuardaAConsequencia()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        Result recusado = processo.DefinirFormulario(PapelDoFormularioDeTeste.Habilitacao, null, null, [], null, ConsequenciaDoDescumprimento.EliminaDoProcesso);

        recusado.IsFailure.Should().BeTrue();
        processo.FormularioDe(FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Habilitacao)).Should().BeNull();
    }
}

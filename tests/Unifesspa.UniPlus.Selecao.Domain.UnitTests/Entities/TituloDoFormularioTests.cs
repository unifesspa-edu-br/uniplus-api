namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class TituloDoFormularioTests
{
    [Fact(DisplayName = "Definir o formulário em rascunho apara o título e trata o título em branco como ausência")]
    public void DefinirFormulario_EmRascunho_AparaOTituloETrataBrancoComoNulo()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, null, "  Formulário  ", FormularioDeTeste.Etapas())
            .IsSuccess.Should().BeTrue();
        processo.FormularioDe(FinalidadeFormulario.Habilitacao)!.Titulo.Should().Be("Formulário");

        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, null, "   ", FormularioDeTeste.Etapas())
            .IsSuccess.Should().BeTrue();
        processo.FormularioDe(FinalidadeFormulario.Habilitacao)!.Titulo.Should().BeNull();
    }

    [Fact(DisplayName = "Título de 301 caracteres é recusado no campo titulo e não cria o formulário")]
    public void DefinirFormulario_TituloExcedeLimite_RecusaSemCriarFormulario()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.FormularioDe(FinalidadeFormulario.Habilitacao).Should().BeNull();

        Result resultado = processo.DefinirFormulario(
            FinalidadeFormulario.Habilitacao, null, new string('a', 301), FormularioDeTeste.Etapas());

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "titulo",
            Error = new { Code = EstruturaFormularioErrorCodes.TituloTamanho },
        });
        processo.FormularioDe(FinalidadeFormulario.Habilitacao).Should().BeNull();
    }
}

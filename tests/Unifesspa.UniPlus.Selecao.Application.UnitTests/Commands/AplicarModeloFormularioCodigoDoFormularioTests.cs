namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaAplicacaoDoModeloDeFormulario;

/// <summary>
/// A aplicação do modelo é feita sobre o formulário do código informado, que o processo já precisa ter; a
/// finalidade do modelo não escolhe o formulário.
/// </summary>
public sealed class AplicarModeloFormularioCodigoDoFormularioTests
{
    private readonly CenarioDaAplicacaoDoModeloDeFormulario _cenario = new();

    [Fact(DisplayName = "Aplicar o modelo a formulário que o processo não tem é recusado antes de ler o modelo")]
    public async Task Handle_FormularioInexistente_RecusaAntesDoModelo()
    {
        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Guid.NewGuid(), "FORMULARIO_QUE_NAO_EXISTE");

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            recusa => recusa.Field == "formularioCodigo" && recusa.Error.Code == FormularioProcessoErrorCodes.FormularioInexistente);
    }

    [Fact(DisplayName = "Aplicar o modelo a código fora do formato é recusado no próprio código")]
    public async Task Handle_CodigoForaDoFormato_RecusaOCodigo()
    {
        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Guid.NewGuid(), "inscricao");

        resultado.Error!.Code.Should().Be(CodigoDoFormularioErrorCodes.FormatoInvalido);
    }

    [Fact(DisplayName = "Aplicar o modelo a código em branco é recusado como código obrigatório")]
    public async Task Handle_CodigoEmBranco_RecusaOCodigo()
    {
        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Guid.NewGuid(), " ");

        resultado.Error!.Code.Should().Be(CodigoDoFormularioErrorCodes.Obrigatorio);
    }

    [Fact(DisplayName = "O relatório da aplicação devolve o código do formulário que recebeu a cópia")]
    public async Task Handle_Aplicado_DevolveOCodigoDoFormulario()
    {
        ModeloFormularioView modelo = Modelo(FinalidadeFormulario.Habilitacao, [Item("CERTIFICADO", 0)]);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(modelo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.FormularioCodigo.Should().Be(FinalidadeFormulario.Habilitacao.Codigo());
    }
}

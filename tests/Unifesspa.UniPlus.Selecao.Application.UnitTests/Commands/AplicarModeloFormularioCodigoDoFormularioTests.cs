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
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaAplicacaoDoModeloDeFormulario;

/// <summary>
/// A aplicação do modelo é feita sobre o formulário do código informado, que o processo já precisa ter; o
/// modelo não escolhe o formulário.
/// </summary>
public sealed class AplicarModeloFormularioCodigoDoFormularioTests
{
    private readonly CenarioDaAplicacaoDoModeloDeFormulario _cenario = new();

    [Fact(DisplayName = "Aplicar o modelo a formulário que o processo não tem é recusado junto com o modelo inexistente")]
    public async Task Handle_FormularioInexistente_AcumulaComOModelo()
    {
        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Guid.NewGuid(), "FORMULARIO_QUE_NAO_EXISTE");

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("formularioCodigo", FormularioProcessoErrorCodes.FormularioInexistente),
            ("modeloId", AplicacaoDeModeloErrorCodes.ModeloInexistente));
    }

    [Fact(DisplayName = "Aplicar o modelo a código fora do formato é recusado no próprio código")]
    public async Task Handle_CodigoForaDoFormato_RecusaOCodigo()
    {
        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Guid.NewGuid(), "inscricao");

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("formularioCodigo", CodigoDoFormularioErrorCodes.FormatoInvalido),
            ("modeloId", AplicacaoDeModeloErrorCodes.ModeloInexistente));
    }

    [Theory(DisplayName = "Aplicar o modelo a código ausente, nulo, vazio ou em branco é recusado no campo, como código obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Handle_CodigoAusenteOuEmBranco_RecusaOCampo(string? codigo)
    {
        ModeloFormularioView modelo = Modelo(PapelDoFormularioDeTeste.Habilitacao, [Item("CERTIFICADO", 0)]);
        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(modelo.Id, codigo);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("formularioCodigo", CodigoDoFormularioErrorCodes.Obrigatorio),
            ("modeloId", AplicacaoDeModeloErrorCodes.ModeloInexistente));
    }

    [Fact(DisplayName = "O código ausente com o modelo existente é a única recusa do pedido")]
    public async Task Handle_CodigoAusenteComModeloExistente_RecusaSoOCodigo()
    {
        ModeloFormularioView modelo = Modelo(PapelDoFormularioDeTeste.Habilitacao, [Item("CERTIFICADO", 0)]);
        _ = await _cenario.AplicarAsync(PapelDoFormularioDeTeste.Habilitacao, modelo);
        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(modelo.Id, null);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("formularioCodigo", CodigoDoFormularioErrorCodes.Obrigatorio));
    }

    [Fact(DisplayName = "O relatório da aplicação devolve o código do formulário que recebeu a cópia")]
    public async Task Handle_Aplicado_DevolveOCodigoDoFormulario()
    {
        ModeloFormularioView modelo = Modelo(PapelDoFormularioDeTeste.Habilitacao, [Item("CERTIFICADO", 0)]);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(PapelDoFormularioDeTeste.Habilitacao, modelo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.FormularioCodigo.Should().Be(PapelDoFormularioDeTeste.Habilitacao.Codigo());
    }
}

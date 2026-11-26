namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O código do formulário que vem da rota é conferido antes de qualquer leitura de cadastro: o que está fora do
/// formato ou em branco é recusado no próprio código, e nada é gravado.
/// </summary>
public sealed class CodigoDoFormularioNaEscritaTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFatoCandidatoReader _catalogo = Substitute.For<IFatoCandidatoReader>();
    private readonly IProcessoSeletivoRepository _repository = Substitute.For<IProcessoSeletivoRepository>();
    private readonly ProcessoSeletivo _processo = ProcessoSeletivoConformeBuilder.Criar("PS Código", out Guid _);

    public CodigoDoFormularioNaEscritaTests() =>
        _repository.ObterParaMutacaoAsync(_processo.Id, Arg.Any<CancellationToken>()).Returns(_processo);

    [Theory(DisplayName = "Definir o formulário com código fora do formato é recusado e nada é gravado")]
    [InlineData("inscricao")]
    [InlineData("COM ESPACO")]
    [InlineData("_INICIA_COM_SUBLINHADO")]
    [InlineData("TERMINA_COM_SUBLINHADO_")]
    public async Task Definir_CodigoForaDoFormato_RecusaOCodigo(string codigo)
    {
        Result resultado = await DefinirAsync(codigo);

        resultado.Error!.Code.Should().Be(CodigoDoFormularioErrorCodes.FormatoInvalido);
        await ConferirQueNadaFoiLidoNemGravadoAsync();
    }

    [Theory(DisplayName = "Definir o formulário com código em branco é recusado como código obrigatório")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Definir_CodigoEmBranco_RecusaOCodigo(string codigo)
    {
        Result resultado = await DefinirAsync(codigo);

        resultado.Error!.Code.Should().Be(CodigoDoFormularioErrorCodes.Obrigatorio);
        await ConferirQueNadaFoiLidoNemGravadoAsync();
    }

    [Fact(DisplayName = "Definir o formulário com código de mais de sessenta caracteres é recusado")]
    public async Task Definir_CodigoComMaisDeSessentaCaracteres_RecusaOCodigo()
    {
        Result resultado = await DefinirAsync(new string('A', 61));

        resultado.Error!.Code.Should().Be(CodigoDoFormularioErrorCodes.FormatoInvalido);
    }

    [Fact(DisplayName = "Remover o formulário com código fora do formato é recusado e nada é gravado")]
    public async Task Remover_CodigoForaDoFormato_RecusaOCodigo()
    {
        Result resultado = await RemoverFormularioCommandHandler.Handle(
            new RemoverFormularioCommand(_processo.Id, "inscricao"), _repository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(CodigoDoFormularioErrorCodes.FormatoInvalido);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Definir os itens com código fora do formato é recusado antes de ler o catálogo")]
    public async Task DefinirItens_CodigoForaDoFormato_RecusaAntesDoCatalogo()
    {
        Result resultado = await DefinirFatosColetadosCommandHandler.Handle(
            new DefinirFatosColetadosCommand(_processo.Id, "inscricao", []), _repository, _catalogo, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(CodigoDoFormularioErrorCodes.FormatoInvalido);
        await ConferirQueNadaFoiLidoNemGravadoAsync();
    }

    private Task<Result> DefinirAsync(string codigo) =>
        DefinirFormularioCommandHandler.Handle(
            new DefinirFormularioCommand(_processo.Id, codigo, null, null, true, []), _repository, _catalogo, _unitOfWork, CancellationToken.None);

    private async Task ConferirQueNadaFoiLidoNemGravadoAsync()
    {
        await _catalogo.DidNotReceive().ListarAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}

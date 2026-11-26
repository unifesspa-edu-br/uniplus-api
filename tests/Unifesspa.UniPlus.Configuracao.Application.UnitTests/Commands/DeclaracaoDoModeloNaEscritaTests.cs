namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;
using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.DeclaracoesDeModeloDeTeste;

/// <summary>A declaração do modelo na criação: se serve à fase de inscrição e se é obrigatório (UNI-REQ-0144, ADR-0171).</summary>
public sealed class DeclaracaoDoModeloNaEscritaTests
{
    private readonly CenarioDoHandlerDoModelo _cenario = new();

    [Theory(DisplayName = "A declaração que falta, inteira ou em parte, é recusada no caminho da declaração e nada é gravado")]
    [InlineData(null, null)]
    [InlineData(true, null)]
    [InlineData(null, true)]
    public async Task Criar_DeclaracaoIncompleta_RecusaNoCaminhoDaDeclaracao(bool? serveAFaseDeInscricao, bool? obrigatorio)
    {
        DeclaracaoDoModeloInput? declaracao = serveAFaseDeInscricao is null && obrigatorio is null ? null : new(serveAFaseDeInscricao, obrigatorio);

        Result<Guid> resultado = await CriarAsync(declaracao);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "declaracao" && e.Error.Code == ModeloFormularioErrorCodes.DeclaracaoIncompleta);
        await _cenario.Repository.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    [Theory(DisplayName = "O modelo de outra fase é gravado com a declaração informada, obrigatório ou facultativo")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Criar_OutraFase_GravaADeclaracaoInformada(bool obrigatorio)
    {
        ModeloFormulario? gravado = null;
        await _cenario.Repository.AdicionarAsync(Arg.Do<ModeloFormulario>(m => gravado = m), Arg.Any<CancellationToken>());

        Result<Guid> resultado = await CriarAsync(new DeclaracaoDoModeloInput(ServeAFaseDeInscricao: false, obrigatorio));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        gravado!.Declaracao.Should().Be(new DeclaracaoDoFormulario(ServeAFaseDeInscricao: false, obrigatorio));
    }

    [Fact(DisplayName = "A declaração só é completa quando traz os dois valores")]
    public void ParaDeclaracao_SoCompletaQuandoTrazOsDoisValores()
    {
        new DeclaracaoDoModeloInput(true, false).ParaDeclaracao().Should().Be(new DeclaracaoDoFormulario(true, false));
        new DeclaracaoDoModeloInput(true, null).ParaDeclaracao().Should().BeNull();
        new DeclaracaoDoModeloInput(null, false).ParaDeclaracao().Should().BeNull();
    }

    private Task<Result<Guid>> CriarAsync(DeclaracaoDoModeloInput? declaracao) =>
        CriarModeloFormularioCommandHandler.Handle(
            new CriarModeloFormularioCommand(
                "HABILITACAO_MEDICINA", declaracao, new DescritivoDoModeloInput("Habilitação Medicina", null, null), Conteudo(Item("CERTIFICADO", 0))),
            _cenario.Repository, _cenario.Fatos, _cenario.Termos, _cenario.Tipos, _cenario.UnitOfWork, CancellationToken.None);
}

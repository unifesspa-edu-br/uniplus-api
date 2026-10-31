namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class AcessoAoProcessoInexistenteTests
{
    [Fact(DisplayName = "Processo inexistente é negado sem consultar o ponto de decisão, que não registra decisão sobre o que não existe")]
    public async Task ProcessoInexistente_NegaSemConsultarOVerificador()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        processos.ObterUnidadeAdministradoraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Guid?)null);

        ResultadoDoAcesso resultado = await AcessoAoProcesso.VerificarAsync(
            verificador, processos, Guid.CreateVersion7(), PermissaoSelecao.ProcessosConfigurar,
            ResourceTiposSelecao.ProcessoSeletivo, sensibilidadeDoResource: null, CancellationToken.None);

        resultado.Should().Be(ResultadoDoAcesso.Negado);
        await verificador.DidNotReceiveWithAnyArgs().VerificarAsync(default!, default, default);
    }

    [Fact(DisplayName = "Tipo de resource fora da convenção é erro de programação e lança, em vez de negar em silêncio")]
    public async Task TipoForaDaConvencao_Lanca()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();

        Func<Task> decidir = () => AcessoAoProcesso.VerificarNaUnidadeAsync(
            verificador, Guid.CreateVersion7(), Guid.CreateVersion7(), PermissaoSelecao.ProcessosConfigurar,
            "ProcessoSeletivo", sensibilidadeDoResource: null, CancellationToken.None);

        await decidir.Should().ThrowAsync<ArgumentException>().WithMessage("*kebab-case*");
    }

    [Fact(DisplayName = "Tipo de resource nulo lança a mesma exceção orientadora")]
    public async Task TipoNulo_Lanca()
    {
        Func<Task> decidir = () => AcessoAoProcesso.VerificarNaUnidadeAsync(
            Substitute.For<IVerificadorDeAcesso>(), Guid.CreateVersion7(), Guid.CreateVersion7(), PermissaoSelecao.ProcessosConfigurar,
            null!, sensibilidadeDoResource: null, CancellationToken.None);

        await decidir.Should().ThrowAsync<ArgumentException>().WithMessage("*kebab-case*");
    }

    [Fact(DisplayName = "Tipo fora da convenção lança também quando não há unidade")]
    public async Task TipoForaDaConvencao_SemUnidade_Lanca()
    {
        Func<Task> decidir = () => AcessoAoProcesso.VerificarNaUnidadeAsync(
            Substitute.For<IVerificadorDeAcesso>(), unidadeId: null, Guid.CreateVersion7(), PermissaoSelecao.ProcessosConfigurar,
            "ProcessoSeletivo", sensibilidadeDoResource: null, CancellationToken.None);

        await decidir.Should().ThrowAsync<ArgumentException>();
    }
}

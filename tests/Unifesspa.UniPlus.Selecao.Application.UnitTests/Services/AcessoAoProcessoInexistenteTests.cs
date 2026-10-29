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
            AcessoAoProcesso.RecursoProcesso, sensibilidadeDoRecurso: null, CancellationToken.None);

        resultado.Should().Be(ResultadoDoAcesso.Negado);
        await verificador.DidNotReceiveWithAnyArgs().VerificarAsync(default!, default, default);
    }
}

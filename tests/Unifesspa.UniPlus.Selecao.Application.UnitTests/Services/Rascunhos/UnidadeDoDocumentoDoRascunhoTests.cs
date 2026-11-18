namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

public sealed class UnidadeDoDocumentoDoRascunhoTests
{
    private readonly CenarioDoRascunho _cenario = new();

    [Fact(DisplayName = "A identidade incompleta não vira recusa de unidade: é a do ponto de decisão")]
    public async Task IdentidadeIncompleta_EARecusaDoPontoDeDecisao()
    {
        _cenario.Verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(ResultadoDoAcesso.IdentidadeIncompleta);

        Result<UnidadeView> resultado = await _cenario.UnidadesDoDocumento
            .ResolverEExigirAsync(CenarioDoRascunho.CodigoDaUnidade, CancellationToken.None);

        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentidadeIncompleta);
    }

    [Fact(DisplayName = "O código sem unidade viva não consulta o ponto de decisão")]
    public async Task CodigoSemUnidade_NaoConsultaOPontoDeDecisao()
    {
        Result<UnidadeView> resultado = await _cenario.UnidadesDoDocumento.ResolverEExigirAsync("NAOEXISTE", CancellationToken.None);

        resultado.Error!.Code.Should().Be(RascunhoDeProcessoSeletivoErrorCodes.UnidadeForaDaPermissao);
        await _cenario.Verificador.DidNotReceiveWithAnyArgs().ExigirAsync(default!, default, default);
    }

    [Fact(DisplayName = "Manter a unidade atual devolve a própria unidade sem decidir o acesso")]
    public async Task MesmaUnidade_DevolveAAtual()
    {
        Result<Guid> resultado = await _cenario.UnidadesDoDocumento
            .ExigirParaTrocaAsync(CenarioDoRascunho.Ceps, CenarioDoRascunho.Ceps.Id, CancellationToken.None);

        resultado.Value.Should().Be(CenarioDoRascunho.Ceps.Id);
        await _cenario.Verificador.DidNotReceiveWithAnyArgs().ExigirAsync(default!, default, default);
    }

    [Fact(DisplayName = "Trocar para unidade inexistente recusa como unidade fora da permissão")]
    public async Task TrocaParaInexistente_ForaDaPermissao()
    {
        Result<Guid> resultado = await _cenario.UnidadesDoDocumento
            .ExigirParaTrocaAsync(nova: null, CenarioDoRascunho.Ceps.Id, CancellationToken.None);

        resultado.Error!.Code.Should().Be(RascunhoDeProcessoSeletivoErrorCodes.UnidadeForaDaPermissao);
    }
}

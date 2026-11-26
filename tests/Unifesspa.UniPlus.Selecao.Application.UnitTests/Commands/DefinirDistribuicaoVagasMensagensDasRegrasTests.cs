namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaDistribuicaoDeVagas;

public sealed class DefinirDistribuicaoVagasMensagensDasRegrasTests
{
    private const string CodigoDaDistribuicao = "CODIGO_DE_DISTRIBUICAO_ESCRITO_NO_TESTE";
    private const string VersaoDaDistribuicao = "versao-de-distribuicao-escrita-no-teste";
    private const string CodigoDoAjuste = "CODIGO_DE_AJUSTE_ESCRITO_NO_TESTE";
    private const string VersaoDoAjuste = "versao-de-ajuste-escrita-no-teste";

    [Fact(DisplayName = "As regras de distribuição e de ajuste ausentes do catálogo são recusadas sem citar código nem versão")]
    public async Task RegrasNaoEncontradas_RecusamSemCitarCodigoNemVersao()
    {
        Mocks mocks = MocksDoPedido(out ProcessoSeletivo processo);
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((RegraCatalogo?)null);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await mocks.DefinirAsync(ComandoDaDistribuicao(processo, PedidoComAjuste()));

        DeveRecusarSemCitarOValorRecebido(
            result,
            ("ConfiguracaoDistribuicaoVagas.RegraDistribuicaoNaoEncontrada", "distribuicaoVagas[0].regraDistribuicaoCodigo"),
            ("ConfiguracaoDistribuicaoVagas.RegraAjusteNaoEncontrada", "distribuicaoVagas[0].regraAjusteCodigo"));
    }

    [Fact(DisplayName = "As regras de distribuição e de ajuste de tipo diferente do esperado são recusadas sem citar código nem versão")]
    public async Task RegrasDeTipoInvalido_RecusamSemCitarCodigoNemVersao()
    {
        Mocks mocks = MocksDoPedido(out ProcessoSeletivo processo);
        RegraCatalogo regraDeOutroTipo = RegraCatalogo.Criar("FORMULA_MEDIA_PONDERADA", "v1", TipoRegra.RegraCalculo, Json("{}"), Json("[]"), "base");
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(regraDeOutroTipo);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await mocks.DefinirAsync(ComandoDaDistribuicao(processo, PedidoComAjuste()));

        DeveRecusarSemCitarOValorRecebido(
            result,
            ("ConfiguracaoDistribuicaoVagas.RegraDistribuicaoTipoInvalido", "distribuicaoVagas[0].regraDistribuicaoCodigo"),
            ("ConfiguracaoDistribuicaoVagas.RegraAjusteTipoInvalido", "distribuicaoVagas[0].regraAjusteCodigo"));
    }

    private static Mocks MocksDoPedido(out ProcessoSeletivo processo)
    {
        processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(NovaOferta(Guid.CreateVersion7()));
        return mocks;
    }

    private static ConfiguracaoDistribuicaoVagasInput PedidoComAjuste() => new(
        Guid.CreateVersion7(), 50, 1m, CodigoDaDistribuicao, VersaoDaDistribuicao, CodigoDoAjuste, VersaoDoAjuste, null,
        [Guid.CreateVersion7()], []);

    private static void DeveRecusarSemCitarOValorRecebido(
        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result,
        params (string Codigo, string? Campo)[] recusasEsperadas)
    {
        result.Errors.Where(e => recusasEsperadas.Contains((e.Error.Code, e.Field)))
            .Should().HaveCount(recusasEsperadas.Length);
        result.Errors.Select(static e => e.Error.Message).Should().NotContain(mensagem =>
            mensagem.Contains(CodigoDaDistribuicao, StringComparison.Ordinal)
            || mensagem.Contains(VersaoDaDistribuicao, StringComparison.Ordinal)
            || mensagem.Contains(CodigoDoAjuste, StringComparison.Ordinal)
            || mensagem.Contains(VersaoDoAjuste, StringComparison.Ordinal));
    }
}

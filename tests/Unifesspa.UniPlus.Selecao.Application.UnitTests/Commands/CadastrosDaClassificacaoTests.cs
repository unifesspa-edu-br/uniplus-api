namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaClassificacao;

public sealed class CadastrosDaClassificacaoTests
{
    [Theory]
    [InlineData(false, "RegraEliminacao.RegraNaoEncontrada")]
    [InlineData(true, "RegraEliminacao.RegraTipoInvalido")]
    public async Task Handle_RegraDeEliminacaoAusenteOuDeOutroTipo_Recusa(bool outroTipo, string codigoEsperado)
    {
        ProcessoSeletivo processo = NovoProcesso("Seleção", TiposDeProcessoDeTeste.IndigenaEQuilombola);
        string codigo = RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem.Codigo();
        CenarioDaClassificacao cenario = Com(processo).ComRegrasBasicas();
        if (outroTipo)
        {
            cenario.ComRegra(codigo, TipoRegra.RegraCalculo);
        }

        Result resultado = await cenario.DefinirAsync(ComandoComEliminacao(processo.Id, new(codigo, "v1", null, null, null, null)));
        resultado.Error!.Code.Should().Be(codigoEsperado);
        resultado.Errors.Single().Field.Should().Be("regrasEliminacao[0].regraCodigo");
        resultado.Error.Message.Should().NotContain(codigo).And.NotContain("v1").And.NotContain("regra_eliminacao");
    }

    [Fact]
    public async Task Handle_RegraDeEliminacaoForaDoCatalogoDeTipos_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso("Seleção", TiposDeProcessoDeTeste.IndigenaEQuilombola);
        CenarioDaClassificacao cenario = Com(processo).ComRegrasBasicas().ComRegra("INEXISTENTE", TipoRegra.RegraEliminacao);

        Result resultado = await cenario.DefinirAsync(ComandoComEliminacao(processo.Id, new("INEXISTENTE", "v1", null, null, null, null)));

        resultado.Error!.Code.Should().Be("RegraEliminacao.RegraTipoInvalido");
        resultado.Errors.Single().Field.Should().Be("regrasEliminacao[0].regraCodigo");
        resultado.Error.Message.Should().Be("O código da regra de eliminação não é conhecido.");
        resultado.Error.Message.Should().NotContain("INEXISTENTE");
    }
}

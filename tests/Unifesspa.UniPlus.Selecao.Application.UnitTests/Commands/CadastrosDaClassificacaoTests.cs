namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
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
        ProcessoSeletivo processo = NovoProcesso("Seleção", TiposDeProcessoDeTeste.PSIQ);
        string codigo = RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem.Codigo();
        CenarioDaClassificacao cenario = Com(processo).ComRegrasBasicas();
        if (outroTipo)
        {
            cenario.ComRegra(codigo, TipoRegra.RegraCalculo);
        }

        Result resultado = await cenario.DefinirAsync(ComandoComEliminacao(processo.Id, new(codigo, "v1", null, null, null, null)));
        resultado.Error!.Code.Should().Be(codigoEsperado);
    }

    [Fact]
    public async Task Handle_RegraDeEliminacaoForaDoCatalogoDeTipos_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso("Seleção", TiposDeProcessoDeTeste.PSIQ);
        CenarioDaClassificacao cenario = Com(processo).ComRegrasBasicas().ComRegra("INEXISTENTE", TipoRegra.RegraEliminacao);

        Result resultado = await cenario.DefinirAsync(ComandoComEliminacao(processo.Id, new("INEXISTENTE", "v1", null, null, null, null)));

        resultado.Error!.Code.Should().Be("RegraEliminacao.RegraTipoInvalido");
        resultado.Error.Message.Should().Be("Código de regra de eliminação desconhecido: INEXISTENTE.");
    }

    [Fact]
    public void DaResolucaoCadastrada_LinhaInvalida_RecusaSemCopiarQuadro()
    {
        PesoAreaEnemView linha = new(Guid.NewGuid(), "Resolução", new GrupoAreaEnemView("", "Grupo"), [], "Base legal");
        ResolucaoPesoAreaEnemView cadastrada = new("Resolução cadastrada", [linha], []);

        QuadroDePesosResolvido resolvido = QuadroDePesosResolvido.DaResolucaoCadastrada("Resolução informada", cadastrada);

        resolvido.Resolucao.Should().Be("Resolução informada");
        resolvido.Quadro.Should().BeEmpty();
        resolvido.Recusas.Should().NotBeEmpty().And.OnlyContain(erro => erro.Field == ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem);
    }
}

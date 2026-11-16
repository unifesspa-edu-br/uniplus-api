namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O número de corretores de cada etapa vem do catálogo, pela referência que a etapa guarda, e nunca de um padrão: a
/// etapa sem regra de consolidação nem consulta o catálogo, e a referência que o catálogo não confirma volta como não resolvida.
/// </summary>
public sealed class LeituraDosCorretoresDasEtapasTests
{
    private static Task<CorretoresDasEtapas> Ler(IRegraCatalogoReader catalogo, ProcessoSeletivo processo) =>
        new LeituraDosCorretoresDasEtapas(catalogo).LerAsync(processo, CancellationToken.None);

    [Fact(DisplayName = "A etapa sem regra de consolidação não consulta o catálogo e fica sem corretores")]
    public async Task EtapaSemConsolidacao_NaoConsultaOCatalogo()
    {
        IRegraCatalogoReader catalogo = RegrasDeConsolidacaoDeTeste.Catalogo();
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS leitura sem consolidação");

        CorretoresDasEtapas corretores = await Ler(catalogo, processo);

        corretores.De(processo.Etapas.Single()).Should().Be(0);
        await catalogo.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, CancellationToken.None);
    }

    [Fact(DisplayName = "Cada etapa recebe o número da regra de consolidação que referencia")]
    public async Task CadaEtapa_RecebeONumeroDaPropriaRegra()
    {
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComRegras(
            ProcessoComCorrecaoIndependente.CorrecaoIndependente, ProcessoComCorrecaoIndependente.AvaliacaoUnica);

        CorretoresDasEtapas corretores = await Ler(RegrasDeConsolidacaoDeTeste.Catalogo(), processo);

        corretores.De(processo.Etapas.Single(etapa => etapa.Ordem == 1)).Should().Be(3);
        corretores.De(processo.Etapas.Single(etapa => etapa.Ordem == 2)).Should().Be(1);
    }

    [Fact(DisplayName = "Cada referência distinta é lida uma só vez, ainda que várias etapas a compartilhem")]
    public async Task ReferenciaCompartilhada_ELidaUmaVez()
    {
        IRegraCatalogoReader catalogo = RegrasDeConsolidacaoDeTeste.Catalogo();
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComRegras(
            ProcessoComCorrecaoIndependente.CorrecaoIndependente,
            ProcessoComCorrecaoIndependente.AvaliacaoUnica,
            ProcessoComCorrecaoIndependente.CorrecaoIndependente);

        CorretoresDasEtapas corretores = await Ler(catalogo, processo);

        corretores.De(processo.Etapas.Single(etapa => etapa.Ordem == 3)).Should().Be(3);
        await catalogo.Received(1).ObterAsync(RegraConsolidacaoCodigo.CorrecaoIndependente, "v1", Arg.Any<CancellationToken>());
        await catalogo.Received(1).ObterAsync(RegraConsolidacaoCodigo.AvaliacaoUnica, "v1", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A regra ausente do catálogo volta como não resolvida, sem lançar nem presumir um número")]
    public async Task RegraAusente_VoltaNaoResolvida()
    {
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComRegras(
            ProcessoComCorrecaoIndependente.CorrecaoIndependente, ProcessoComCorrecaoIndependente.AvaliacaoUnica);

        CorretoresDasEtapas corretores = await Ler(RegrasDeConsolidacaoDeTeste.Catalogo(RegrasDeConsolidacaoDeTeste.AvaliacaoUnica), processo);

        corretores.Resolvida(processo.Etapas.Single(etapa => etapa.Ordem == 1)).Should().BeFalse();
        corretores.Resolvida(processo.Etapas.Single(etapa => etapa.Ordem == 2)).Should().BeTrue();
        corretores.De(processo.Etapas.Single(etapa => etapa.Ordem == 2)).Should().Be(1);
        Action ler = () => corretores.De(processo.Etapas.Single(etapa => etapa.Ordem == 1));
        ler.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "A referência com o hash diferente do catálogo volta como não resolvida, sem lançar")]
    public async Task HashDivergente_VoltaNaoResolvida()
    {
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComRegras(ProcessoComCorrecaoIndependente.CorrecaoIndependente);
        RegraCatalogo outraDefinicao = RegrasDeConsolidacaoDeTeste.Regra(
            RegraConsolidacaoCodigo.CorrecaoIndependente, """{"corretores_distintos":4,"naturezas_do_conceito":["numerico"]}""");

        CorretoresDasEtapas corretores = await Ler(RegrasDeConsolidacaoDeTeste.Catalogo(outraDefinicao), processo);

        corretores.Resolvida(processo.Etapas.Single()).Should().BeFalse();
    }

    [Theory(DisplayName = "A versão sem o número de corretores, ou com número inválido, volta como não resolvida, sem lançar")]
    [InlineData("{}")]
    [InlineData("""{"corretores_distintos":0}""")]
    [InlineData("""{"corretores_distintos":"3"}""")]
    public async Task EsquemaInvalido_VoltaNaoResolvida(string esquemaArgs)
    {
        RegraCatalogo invalida = RegrasDeConsolidacaoDeTeste.Regra(RegraConsolidacaoCodigo.CorrecaoIndependente, esquemaArgs);
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComRegras(invalida.Referencia());

        CorretoresDasEtapas corretores = await Ler(RegrasDeConsolidacaoDeTeste.Catalogo(invalida), processo);

        corretores.Resolvida(processo.Etapas.Single()).Should().BeFalse();
    }
}

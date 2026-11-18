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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O número de corretores em campo de cada etapa vem do catálogo, pela referência que a etapa guarda, e nunca de um padrão: a
/// etapa sem regra de consolidação nem consulta o catálogo, e a referência que o catálogo não confirma volta como não resolvida.
/// </summary>
public sealed class LeituraDasRegrasCitadasNaConfiguracaoTests
{
    private static async Task<CorretoresDasEtapas> Ler(IRegraCatalogoReader catalogo, ProcessoSeletivo processo) =>
        (await new LeituraDasRegrasCitadasNaConfiguracao(catalogo).LerAsync(processo, CancellationToken.None)).Corretores;

    [Fact(DisplayName = "A etapa sem regra de consolidação fica sem corretores")]
    public async Task EtapaSemConsolidacao_FicaSemCorretores()
    {
        IRegraCatalogoReader catalogo = RegrasDeConsolidacaoDeTeste.Catalogo();
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS leitura sem consolidação");

        CorretoresDasEtapas corretores = await Ler(catalogo, processo);

        corretores.EmCampo(processo.Etapas.Single()).Should().Be(0);
        await catalogo.DidNotReceive().ObterAsync(RegraConsolidacaoCodigo.CorrecaoIndependente, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Cada etapa recebe o número da regra de consolidação que referencia")]
    public async Task CadaEtapa_RecebeONumeroDaPropriaRegra()
    {
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComRegras(
            ProcessoComCorrecaoIndependente.CorrecaoIndependente, ProcessoComCorrecaoIndependente.AvaliacaoUnica);

        CorretoresDasEtapas corretores = await Ler(RegrasDeConsolidacaoDeTeste.Catalogo(), processo);

        corretores.EmCampo(processo.Etapas.Single(etapa => etapa.Ordem == 1)).Should().Be(3);
        corretores.EmCampo(processo.Etapas.Single(etapa => etapa.Ordem == 2)).Should().Be(1);
    }

    [Theory(DisplayName = "A regra que declara o novo corretor põe em campo os distintos e mais um; a que declara a decisão da banca, só os distintos")]
    [InlineData("novo_corretor", 4)]
    [InlineData("decisao_da_banca", 3)]
    public async Task TratamentoDeclarado_DefineOAdicional(string tratamento, int esperado)
    {
        RegraCatalogo regra = RegrasDeConsolidacaoDeTeste.Regra(
            RegraConsolidacaoCodigo.ConceitoMaioriaSimples,
            $$"""{"corretores_distintos":3,"criterio":"maioria_simples","naturezas_do_conceito":["categorico"],"quando_nao_consolida":"{{tratamento}}"}""");
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComRegras(regra.Referencia());

        CorretoresDasEtapas corretores = await Ler(RegrasDeConsolidacaoDeTeste.Catalogo(regra), processo);

        corretores.EmCampo(processo.Etapas.Single()).Should().Be(esperado);
    }

    [Fact(DisplayName = "A regra de maioria simples do catálogo põe em campo os três corretores e o adicional")]
    public async Task MaioriaSimplesDoCatalogo_PoeEmCampoQuatro()
    {
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComRegras(ProcessoComCorrecaoIndependente.MaioriaSimples);

        CorretoresDasEtapas corretores = await Ler(RegrasDeConsolidacaoDeTeste.Catalogo(), processo);

        corretores.EmCampo(processo.Etapas.Single()).Should().Be(4);
        corretores.Distintos(processo.Etapas.Single()).Should().Be(3);
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

        corretores.EmCampo(processo.Etapas.Single(etapa => etapa.Ordem == 3)).Should().Be(3);
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
        corretores.EmCampo(processo.Etapas.Single(etapa => etapa.Ordem == 2)).Should().Be(1);
        Action ler = () => corretores.EmCampo(processo.Etapas.Single(etapa => etapa.Ordem == 1));
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

    [Fact(DisplayName = "Hashes distintos da mesma versão compartilham a consulta e preservam a regra vigente")]
    public async Task HashesDistintos_ConsultaUmaVezEPreservaRegraVigente()
    {
        RegraCatalogo vigente = RegrasDeConsolidacaoDeTeste.CorrecaoIndependente;
        ReferenciaRegra antiga = ReferenciaRegra.De(vigente.Codigo, vigente.Versao, new string('a', 64));
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComRegras(antiga, vigente.Referencia());
        IRegraCatalogoReader catalogo = RegrasDeConsolidacaoDeTeste.Catalogo(vigente);

        RegrasCitadasNaConfiguracao resultado = await new LeituraDasRegrasCitadasNaConfiguracao(catalogo)
            .LerAsync(processo, CancellationToken.None);

        resultado.Regras[(vigente.Codigo, vigente.Versao)].Referencia.Should().Be(vigente.Referencia());
        resultado.Corretores.Resolvida(processo.Etapas.Single(e => e.Ordem == 1)).Should().BeFalse();
        resultado.Corretores.Resolvida(processo.Etapas.Single(e => e.Ordem == 2)).Should().BeTrue();
        await catalogo.Received(1).ObterAsync(vigente.Codigo, vigente.Versao, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Versões diferentes do mesmo código são consultadas separadamente")]
    public async Task MesmoCodigoComVersoesDiferentes_ConsultaCadaVersao()
    {
        ReferenciaRegra vigente = ProcessoComCorrecaoIndependente.CorrecaoIndependente;
        ReferenciaRegra outraVersao = ReferenciaRegra.De(vigente.Codigo, "v999", vigente.Hash);
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComRegras(vigente, outraVersao);
        IRegraCatalogoReader catalogo = RegrasDeConsolidacaoDeTeste.Catalogo();

        RegrasCitadasNaConfiguracao resultado = await new LeituraDasRegrasCitadasNaConfiguracao(catalogo)
            .LerAsync(processo, CancellationToken.None);

        resultado.Regras.Should().ContainKey((vigente.Codigo, vigente.Versao)).And.NotContainKey((vigente.Codigo, "v999"));
        await catalogo.Received(1).ObterAsync(vigente.Codigo, vigente.Versao, Arg.Any<CancellationToken>());
        await catalogo.Received(1).ObterAsync(vigente.Codigo, "v999", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A leitura encaminha o cancelamento à consulta do catálogo")]
    public async Task LerAsync_RecebeCancelamento_EncaminhaAoCatalogo()
    {
        RegraCatalogo regra = RegrasDeConsolidacaoDeTeste.CorrecaoIndependente;
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComRegras(regra.Referencia());
        IRegraCatalogoReader catalogo = RegrasDeConsolidacaoDeTeste.Catalogo(regra);
        using CancellationTokenSource cancelamento = new();

        await new LeituraDasRegrasCitadasNaConfiguracao(catalogo).LerAsync(processo, cancelamento.Token);

        await catalogo.Received(1).ObterAsync(regra.Codigo, regra.Versao, cancelamento.Token);
    }

    [Fact(DisplayName = "Referências com hashes distintos à mesma regra ausente compartilham a consulta")]
    public async Task RegraAusenteComHashesDistintos_ConsultaUmaVezESemCorretores()
    {
        ReferenciaRegra original = ProcessoComCorrecaoIndependente.CorrecaoIndependente;
        ReferenciaRegra outra = ReferenciaRegra.De(original.Codigo, original.Versao, new string('a', 64));
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComRegras(original, outra);
        IRegraCatalogoReader catalogo = RegrasDeConsolidacaoDeTeste.Catalogo(RegrasDeConsolidacaoDeTeste.AvaliacaoUnica);

        RegrasCitadasNaConfiguracao resultado = await new LeituraDasRegrasCitadasNaConfiguracao(catalogo)
            .LerAsync(processo, CancellationToken.None);

        resultado.Regras.Should().NotContainKey((original.Codigo, original.Versao));
        resultado.Corretores.Resolvida(processo.Etapas.Single(e => e.Ordem == 1)).Should().BeFalse();
        resultado.Corretores.Resolvida(processo.Etapas.Single(e => e.Ordem == 2)).Should().BeFalse();
        await catalogo.Received(1).ObterAsync(original.Codigo, original.Versao, Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "A versão sem o número de corretores, com número ou tratamento inválido, volta como não resolvida, sem lançar")]
    [InlineData("{}")]
    [InlineData("""{"corretores_distintos":0}""")]
    [InlineData("""{"corretores_distintos":"3"}""")]
    [InlineData("""{"corretores_distintos":3,"quando_nao_consolida":"sorteio"}""")]
    [InlineData("""{"corretores_distintos":3,"quando_nao_consolida":1}""")]
    public async Task EsquemaInvalido_VoltaNaoResolvida(string esquemaArgs)
    {
        RegraCatalogo invalida = RegrasDeConsolidacaoDeTeste.Regra(RegraConsolidacaoCodigo.CorrecaoIndependente, esquemaArgs);
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComRegras(invalida.Referencia());

        CorretoresDasEtapas corretores = await Ler(RegrasDeConsolidacaoDeTeste.Catalogo(invalida), processo);

        corretores.Resolvida(processo.Etapas.Single()).Should().BeFalse();
    }
}

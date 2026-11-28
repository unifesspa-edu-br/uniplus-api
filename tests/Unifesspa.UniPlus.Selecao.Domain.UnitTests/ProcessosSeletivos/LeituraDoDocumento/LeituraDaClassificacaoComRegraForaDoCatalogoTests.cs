namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.ClassificacaoDoDocumentoDeTeste;

public sealed class LeituraDaClassificacaoComRegraForaDoCatalogoTests
{
    private static readonly ReferenciaDeRegraNoDocumento ForaDoCatalogo = new(RegraDigitada, "v1");

    private static ClassificacaoLida Ler(ClassificacaoNoDocumento classificacao) =>
        LeituraDaClassificacao.Ler(classificacao, Etapas(), Cadastros());

    [Fact(DisplayName = "A regra de cálculo fora do catálogo e a escala inválida saem juntas, cada uma no seu campo")]
    public void RegraDeCalculoForaDoCatalogoEEscalaInvalida_SomamAsDuasRecusas()
    {
        ClassificacaoLida lida = Ler(Local() with { RegraCalculo = ForaDoCatalogo, EscalaNotaFinal = 0m });

        lida.Classificacao.Should().BeNull();
        Causas(lida.Recusadas).Should().Equal(
            ("/classificacao/escalaNotaFinal", "ConfiguracaoClassificacao.EscalaNotaFinalInvalida"),
            ("/classificacao/regraCalculo", "ConfiguracaoClassificacao.RegraNaoEncontrada"));
    }

    [Fact(DisplayName = "Com a regra de cálculo fora do catálogo não se confere o que depende do tipo de cálculo")]
    public void RegraDeCalculoForaDoCatalogoSemArredondamento_RecusaSoARegra()
    {
        ClassificacaoLida lida = Ler(Local() with { RegraCalculo = ForaDoCatalogo, RegraArredondamento = null, CasasArredondamento = null });

        Causas(lida.Recusadas).Should().Equal(("/classificacao/regraCalculo", "ConfiguracaoClassificacao.RegraNaoEncontrada"));
    }

    [Fact(DisplayName = "A precisão informada e fora do catálogo não é recusada também como obrigatória")]
    public void ArredondamentoInformadoForaDoCatalogo_NaoRecusaComoObrigatorio()
    {
        ClassificacaoLida lida = Ler(Local() with { RegraArredondamento = ForaDoCatalogo, CasasArredondamento = 0 });

        Causas(lida.Recusadas).Should().Equal(
            ("/classificacao/casasArredondamento", "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio"),
            ("/classificacao/regraArredondamento", "ConfiguracaoClassificacao.RegraNaoEncontrada"));
    }

    [Fact(DisplayName = "As três regras fora do catálogo saem na ordem do documento: cálculo, precisão e ordem de alocação")]
    public void TresRegrasForaDoCatalogo_SomamNaOrdemDoDocumento()
    {
        ClassificacaoLida lida = Ler(Local() with
        {
            RegraCalculo = ForaDoCatalogo,
            RegraArredondamento = ForaDoCatalogo,
            RegraOrdemAlocacao = ForaDoCatalogo,
        });

        Causas(lida.Recusadas).Should().Equal(
            ("/classificacao/regraCalculo", "ConfiguracaoClassificacao.RegraNaoEncontrada"),
            ("/classificacao/regraArredondamento", "ConfiguracaoClassificacao.RegraNaoEncontrada"),
            ("/classificacao/regraOrdemAlocacao", "ConfiguracaoClassificacao.RegraNaoEncontrada"));
    }

    [Fact(DisplayName = "A ordem de alocação fora do catálogo e a escala inválida saem juntas")]
    public void OrdemDeAlocacaoForaDoCatalogoEEscalaInvalida_SomamAsDuasRecusas()
    {
        ClassificacaoLida lida = Ler(Local() with { RegraOrdemAlocacao = ForaDoCatalogo, EscalaNotaFinal = -1m });

        Causas(lida.Recusadas).Should().Equal(
            ("/classificacao/escalaNotaFinal", "ConfiguracaoClassificacao.EscalaNotaFinalInvalida"),
            ("/classificacao/regraOrdemAlocacao", "ConfiguracaoClassificacao.RegraNaoEncontrada"));
    }

    [Fact(DisplayName = "A eliminação que depende do ENEM continua conferida com a regra de cálculo fora do catálogo")]
    public void RegraDeCalculoForaDoCatalogoComEliminacaoDoEnem_ConfereADependenciaDoEnem()
    {
        RegraDeEliminacaoNoDocumento zeroEmArea = new(0, Referencia(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem), null, null, null, null);

        ClassificacaoLida lida = Ler(Local() with { RegraCalculo = ForaDoCatalogo, RegrasEliminacao = [zeroEmArea] });

        Causas(lida.Recusadas).Should().Equal(
            ("/classificacao/regrasEliminacao", "ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem"),
            ("/classificacao/regraCalculo", "ConfiguracaoClassificacao.RegraNaoEncontrada"));
    }

    [Fact(DisplayName = "O campo baseadoEmEnem ausente continua recusado como obrigatório com regra fora do catálogo, sem tratar a falta como não")]
    public void BaseadoEmEnemAusenteComRegraForaDoCatalogo_RecusaComoObrigatorioENaoConfereOEnem()
    {
        RegraDeEliminacaoNoDocumento zeroEmArea = new(0, Referencia(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem), null, null, null, null);

        ClassificacaoLida lida = Ler(Local() with { BaseadoEmEnem = null, RegraOrdemAlocacao = ForaDoCatalogo, RegrasEliminacao = [zeroEmArea] });

        Causas(lida.Recusadas).Should().BeEquivalentTo(
        [
            ("/classificacao/baseadoEmEnem", CodigosDeValidacao.Obrigatorio),
            ("/classificacao/regraOrdemAlocacao", "ConfiguracaoClassificacao.RegraNaoEncontrada"),
        ]);
    }

    [Fact(DisplayName = "A eliminação recusada conta na classificação importada: a recusa de eliminação indevida não some")]
    public void ClassificacaoImportadaComEliminacaoRecusada_ContinuaRecusandoAEliminacaoIndevida()
    {
        RegraDeEliminacaoNoDocumento recusada = new(0, ForaDoCatalogo, null, null, null, null);

        ClassificacaoLida lida = Ler(Local() with
        {
            RegraCalculo = Referencia(RegraDeCalculoDaNota.ClassificacaoImportada),
            RegraArredondamento = null,
            CasasArredondamento = null,
            RegraOrdemAlocacao = ForaDoCatalogo,
            RegrasEliminacao = [recusada],
        });

        Causas(lida.Recusadas).Should().Contain(("/classificacao/regrasEliminacao", "ConfiguracaoClassificacao.EliminacaoIndevida"));
    }

    [Fact(DisplayName = "Com a eliminação recusada antes de duas repetidas, a recusa da repetição aponta a posição dela no documento")]
    public void EliminacaoRecusadaEDuasRepetidas_ApontaAPosicaoDoDocumento()
    {
        RegraDeEliminacaoNoDocumento recusada = new(0, ForaDoCatalogo, null, null, null, null);
        RegraDeEliminacaoNoDocumento primeira = new(1, Referencia(RegraDeEliminacaoPorCalculo.AusenciaNaEtapa), EtapaAdotada, null, null, null);
        RegraDeEliminacaoNoDocumento repetida = primeira with { PosicaoNoDocumento = 2 };

        ClassificacaoLida lida = default!;
        Action ler = () => lida = Ler(Local() with { RegraOrdemAlocacao = ForaDoCatalogo, RegrasEliminacao = [recusada, primeira, repetida] });

        ler.Should().NotThrow();
        Causas(lida.Recusadas).Should().Contain(causa => causa.Ponteiro!.StartsWith("/classificacao/regrasEliminacao/2", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A eliminação de parâmetros inválidos conta na classificação importada, com as referências do catálogo válidas")]
    public void ClassificacaoImportadaComEliminacaoDeParametrosInvalidos_RecusaAEliminacaoIndevida()
    {
        RegraDeEliminacaoNoDocumento invalida = new(0, Referencia(RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa), EtapaAdotada, null, null, null);

        ClassificacaoLida lida = Ler(Local() with
        {
            RegraCalculo = Referencia(RegraDeCalculoDaNota.ClassificacaoImportada),
            RegraArredondamento = null,
            CasasArredondamento = null,
            RegrasEliminacao = [invalida],
        });

        lida.Classificacao.Should().BeNull();
        Causas(lida.Recusadas).Should().Contain(("/classificacao/regrasEliminacao", "ConfiguracaoClassificacao.EliminacaoIndevida"));
    }

    [Fact(DisplayName = "A recusa do cadastro de Pesos por Área sai depois das recusas dos campos")]
    public void ResolucaoNaoEncontrada_SaiDepoisDasRecusasDosCampos()
    {
        ClassificacaoLida lida = Ler(Local() with { BaseadoEmEnem = true, ResolucaoPesoAreaEnem = "RES-DE-TESTE", EscalaNotaFinal = 0m });

        Causas(lida.Recusadas).Should().Equal(
            ("/classificacao/escalaNotaFinal", "ConfiguracaoClassificacao.EscalaNotaFinalInvalida"),
            ("/classificacao/resolucaoPesoAreaEnem", "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemNaoEncontrada"));
    }
}

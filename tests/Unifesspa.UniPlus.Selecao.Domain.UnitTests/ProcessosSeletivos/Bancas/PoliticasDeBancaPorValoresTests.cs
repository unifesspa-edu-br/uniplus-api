namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>As regras de banca que decidem sobre valores declarados, sem o agregado: a mesma resposta para a declaração e para a entidade.</summary>
public sealed class PoliticasDeBancaPorValoresTests
{
    private static readonly ReferenciaRegra CorrecaoIndependente =
        ReferenciaRegra.De(RegraDeApuracaoDaAvaliacao.CorrecaoIndependente.Codigo(), "v1", new string('a', 64));

    private static readonly ReferenciaRegra Desconhecida =
        ReferenciaRegra.De(RegraDeApuracaoDaAvaliacao.AvaliacaoUnica.Codigo(), "v1", new string('b', 64));

    [Fact(DisplayName = "O mínimo é o maior entre o quórum e os corretores das julgadas, e sem julgadas é o quórum")]
    public void Exigido_EOMaiorEntreQuorumECorretores()
    {
        RegraDeDeliberacao regra = BancasDeTeste.Regra(integrantes: 5, quorum: 3);

        MinimoDeIntegrantes.Exigido(regra, [2, 4]).Should().Be(4);
        MinimoDeIntegrantes.Exigido(regra, [1]).Should().Be(3);
        MinimoDeIntegrantes.Exigido(regra, []).Should().Be(3);
    }

    [Fact(DisplayName = "As recusas do mínimo e da regra não resolvida citam a banca, o lugar e o que faltou")]
    public void FabricasDoMinimo_CitamBancaLugarEMedida()
    {
        MinimoDeIntegrantes.AbaixoDoMinimo("Banca de análise de recursos 1", "fase 'RECURSOS'", 2, 3).Message
            .Should().StartWith("A banca 'Banca de análise de recursos 1' da fase 'RECURSOS' declara 2 integrantes, e o mínimo exigido é 3");
        MinimoDeIntegrantes.NaoResolvida("Banca de análise de recursos 1", "fase 'RECURSOS'", 0).Code
            .Should().Be(EtapaProcessoErrorCodes.RegraDeConsolidacaoNaoResolvida);
    }

    [Fact(DisplayName = "Sem regra de consolidação a etapa tem resposta e zero corretores; com regra, só quando o número foi lido, contando o corretor adicional")]
    public void CorretoresPorValor_ResolveARegraLida()
    {
        CorretoresDasEtapas corretores = new(new Dictionary<ReferenciaRegra, CorretoresDaRegra> { [CorrecaoIndependente] = new CorretoresDaRegra(Distintos: 3, EmCampo: 4) });

        corretores.RegraResolvida(null).Should().BeTrue();
        corretores.EmCampoDaRegra(null).Should().Be(0);
        corretores.RegraResolvida(CorrecaoIndependente).Should().BeTrue();
        corretores.EmCampoDaRegra(CorrecaoIndependente).Should().Be(4);
        corretores.RegraResolvida(Desconhecida).Should().BeFalse();
        FluentActions.Invoking(() => corretores.EmCampoDaRegra(Desconhecida)).Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Os candidatos atendidos só são recusados na banca de recursos, e cita a fase declarada")]
    public void CandidatosAtendidosNaDeclaracao_SoNaBancaDeRecursos()
    {
        TipoDeBancaCopiado tipoDeRecursos = TiposDeBancaCopiadosDeTeste.DeRecursos();

        ExigenciaDeBancaDeRecurso.CandidatosAtendidosNaDeclaracao(tipoDeRecursos, 1, 1)!.Message.Should().Contain("Na fase,");
        ExigenciaDeBancaDeRecurso.CandidatosAtendidosNaDeclaracao(tipoDeRecursos, 1, 0).Should().BeNull();
        ExigenciaDeBancaDeRecurso.CandidatosAtendidosNaDeclaracao(TiposDeBancaCopiadosDeTeste.Avaliadora(), 1, 1).Should().BeNull();
    }
}

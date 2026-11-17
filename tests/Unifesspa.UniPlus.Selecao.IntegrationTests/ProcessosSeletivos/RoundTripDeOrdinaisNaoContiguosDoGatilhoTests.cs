namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// Issue #1068 — o envelope congelado a partir de ordinais de cláusula não contíguos reidrata e recanonicaliza nos mesmos bytes.
/// </summary>
public sealed class RoundTripDeOrdinaisNaoContiguosDoGatilhoTests
{
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizador = new();

    private const string ConvergenciaDosBytes =
    "reidratar um envelope com ordinais não contíguos e recanonicalizar tem de reproduzir os " +
    "bytes congelados inteiros — o regrupamento pela posição reidratada converge para a mesma " +
    "forma canônica que os ordinais originais já produziam";

    private static (ProcessoSeletivo Processo, VersaoConfiguracao Versao, SnapshotCanonico Congelado) PublicarComOrdinaisNaoContiguos()
    {
        CondicaoGatilho clausula7A = CondicaoGatilho.Criar(7, "FATO_A", Operador.Igual, PredicadoDnfOrdenacaoCanonicaTests.Escalar("valorA")).Value!;
        CondicaoGatilho clausula7B = CondicaoGatilho.Criar(7, "FATO_B", Operador.Igual, PredicadoDnfOrdenacaoCanonicaTests.Escalar("valorB")).Value!;
        CondicaoGatilho clausula4 = CondicaoGatilho.Criar(4, "FATO_C", Operador.Igual, PredicadoDnfOrdenacaoCanonicaTests.Escalar("valorC")).Value!;
        CondicaoGatilho clausula9 = CondicaoGatilho.Criar(9, "FATO_D", Operador.Igual, PredicadoDnfOrdenacaoCanonicaTests.Escalar("valorD")).Value!;

        return PublicarComGatilho([clausula7A, clausula7B, clausula4, clausula9]);
    }

    private static (ProcessoSeletivo Processo, VersaoConfiguracao Versao, SnapshotCanonico Congelado) PublicarComGatilho(CondicaoGatilho[] clausulas)
    {
        ProcessoSeletivo processo = PredicadoDnfOrdenacaoCanonicaTests.MontarProcessoComGatilho(clausulas);

        DadosEdital dados = PredicadoDnfOrdenacaoCanonicaTests.Dados();
        const string hashDocumento = "3333333333333333333333333333333333333333333333333333333333333333";
        SnapshotCanonico congelado = Canonicalizador.Canonicalizar(new EntradaCanonicalizacao(processo, dados, hashDocumento, FusoInstitucional.ZoneId, ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            new CenarioDeCongelamento { Dados = dados, Documento = new DocumentoCongelado(congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash, hashDocumento), AtorUsuarioContaId = "user-conta-predicado-1068", Contagem = CorpusEnvelopeDadosDoEdital.ContextoRico() });
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        VersaoConfiguracao v1 = publicacao.Value!;

        return (processo, v1, congelado);
    }

    private static void AfirmarClausulasReatribuidasPorPosicao(EnvelopeReidratado reidratado)
    {
        // A afirmação de convergência, provada diretamente: o decoder reatribuiu Clausula por
        // posição (0, 1, 2 — três cláusulas no array, não os ordinais 7/4/9 originais), e a
        // cláusula de dois átomos permanece uma conjunção só, não duas cláusulas separadas.
        DocumentoExigido exigenciaReidratada = reidratado.Grafo.DocumentosExigidos.Single();
        exigenciaReidratada.Condicoes.Select(static c => c.Clausula).Distinct().Should().BeEquivalentTo([0, 1, 2],
            "o decoder atribui Clausula pela posição no array — três cláusulas no envelope, nunca os " +
            "ordinais originais 7/4/9, que não sobrevivem à canonicalização");
        exigenciaReidratada.Condicoes.GroupBy(static c => c.Clausula).Should().ContainSingle(
            static g => g.Count() == 2,
            "a cláusula de dois átomos (FATO_A, FATO_B) tem de permanecer uma ÚNICA conjunção após o " +
            "round-trip, não duas cláusulas de um átomo cada");
    }

    private static Result<SnapshotCanonico> Recodificar(ProcessoSeletivo processo, VersaoConfiguracao v1, EnvelopeReidratado reidratado) =>
        CorpusEnvelope.Registro.Recodificar(
            v1.SchemaVersion,
            new EntradaCanonicalizacao(
                processo, reidratado.Dados, reidratado.HashDocumento, FusoInstitucional.ZoneId, reidratado.Retificacao,
                reidratado.Conformidade, reidratado.MetadadosFatosCongelados,
                reidratado.ValoresSelecionaveisCongelados,
                reidratado.CalendarioDiasUteis, reidratado.AgregadosDosGrupos, reidratado.ApresentacaoDosPressupostos,
                TiposDeAtoCongelados: reidratado.TiposDeAtoCongelados, FatosDerivadosCongelados: reidratado.FatosDerivadosCongelados));

    /// <summary>
    /// O decodificador atribui <c>Clausula</c> pela POSIÇÃO no array (índice do laço) — o envelope
    /// não carrega o ordinal original, só arrays posicionais. Um envelope congelado a partir de
    /// ordinais <c>7, 4, 9</c> é reidratado com ordinais <c>0, 1, 2</c>; a recodificação reagrupa e
    /// reordena por conteúdo, chegando aos MESMOS bytes — mas isso é uma afirmação a PROVAR, não a
    /// assumir (Fase 0 do plano). Ao menos uma cláusula de DOIS átomos: só cláusulas unitárias não
    /// provam preservação da conjunção (a reidratação de uma cláusula de um átomo só não
    /// discriminaria "agrupar corretamente" de "criar uma cláusula por átomo").
    /// </summary>
    [Fact(DisplayName = "condicaoGatilho: round-trip a partir de ordinais não contíguos (7, 4, 9) — reidratar reatribui por posição e recanonicalizar reproduz os bytes")]
    public void CondicaoGatilho_RoundTripComOrdinaisNaoContiguos_ReproduzOsBytes()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao v1, SnapshotCanonico congelado) = PublicarComOrdinaisNaoContiguos();

        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(v1);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);

        AfirmarClausulasReatribuidasPorPosicao(reidratado.Value!);

        processo.RestaurarConfiguracaoCongelada(v1, reidratado.Value!.Grafo).IsSuccess.Should().BeTrue();

        Result<SnapshotCanonico> recodificado = Recodificar(processo, v1, reidratado.Value!);
        recodificado.IsSuccess.Should().BeTrue(recodificado.Error?.Message);

        recodificado.Value!.Bytes.Should().Equal(congelado.Bytes, ConvergenciaDosBytes);
    }
}

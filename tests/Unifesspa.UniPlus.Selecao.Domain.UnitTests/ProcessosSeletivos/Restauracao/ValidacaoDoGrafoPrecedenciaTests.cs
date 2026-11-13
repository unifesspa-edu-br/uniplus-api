namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Restauracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ProcessoSeletivoRestaurarConfiguracaoTests;

/// <summary>
/// A validação do grafo restaurado para na primeira recusa, na ordem em que confere as regras. Cada
/// regra é provada sozinha (código, mensagem e agregado intacto) e contra a seguinte, no mesmo grafo:
/// quem corrige o grafo de cima para baixo vê sempre o primeiro problema.
/// </summary>
public sealed partial class ValidacaoDoGrafoPrecedenciaTests
{
    public static TheoryData<Violacao> TodasAsViolacoes() => [.. Enum.GetValues<Violacao>()];

    /// <summary>
    /// Pares de violações vizinhas na ordem da conferência. Onde duas vizinhas não coexistem no mesmo
    /// grafo, a primeira é pareada com a próxima que coexiste: a distribuição vazia não tem oferta
    /// duplicada, duas opções de curso só ficam acima das ofertas com uma oferta e as violações de
    /// ação e de regra divergentes entre ofertas exigem duas, o cronograma vazio não tem ordem ou fase repetida, e
    /// a fase agrupadora sem etapa exige que nenhuma etapa fique sem fase.
    /// </summary>
    private static readonly (Violacao Primeira, Violacao Segunda)[] ParesVizinhos =
    [
        (Violacao.IdDeEtapaAusente, Violacao.IdDeEtapaDuplicado),
        (Violacao.IdDeEtapaDuplicado, Violacao.OrdemDeEtapaDuplicada),
        (Violacao.OrdemDeEtapaDuplicada, Violacao.EtapaDoEnemSobClassificacaoSemEnem),
        (Violacao.EtapaDoEnemSobClassificacaoSemEnem, Violacao.NenhumaEtapaComponeANota),
        (Violacao.NenhumaEtapaComponeANota, Violacao.DistribuicaoDeVagasVazia),
        (Violacao.DistribuicaoDeVagasVazia, Violacao.OpcoesDeCursoForaDaFaixa),
        (Violacao.OfertaDeCursoDuplicada, Violacao.OpcoesDeCursoForaDaFaixa),
        (Violacao.OpcoesDeCursoForaDaFaixa, Violacao.OpcoesDeCursoAcimaDasOfertas),
        (Violacao.OpcoesDeCursoAcimaDasOfertas, Violacao.RegraIncoerenteComANatureza),
        (Violacao.OpcoesDeCursoForaDaFaixa, Violacao.AcaoQuandoIndeferidoDivergente),
        (Violacao.AcaoQuandoIndeferidoDivergente, Violacao.RegraDeAplicabilidadeDivergente),
        (Violacao.RegraDeAplicabilidadeDivergente, Violacao.RegraIncoerenteComANatureza),
        (Violacao.RegraIncoerenteComANatureza, Violacao.RemanejamentoIncoerenteComANatureza),
        (Violacao.RemanejamentoIncoerenteComANatureza, Violacao.CriteriosDeDesempateAcimaDoTeto),
        (Violacao.CriteriosDeDesempateAcimaDoTeto, Violacao.ItensDoFormularioAcimaDoTeto),
        (Violacao.ItensDoFormularioAcimaDoTeto, Violacao.OrdemDeDesempateDuplicada),
        (Violacao.OrdemDeDesempateDuplicada, Violacao.DesempateCitaEtapaInexistente),
        (Violacao.DesempateCitaEtapaInexistente, Violacao.DesempatePorAreaSemQuadro),
        (Violacao.DesempatePorAreaSemQuadro, Violacao.EliminacaoCitaEtapaInexistente),
        (Violacao.EliminacaoCitaEtapaInexistente, Violacao.AusenciaNaEtapaDoEnem),
        (Violacao.AusenciaNaEtapaDoEnem, Violacao.CronogramaVazio),
        (Violacao.CronogramaVazio, Violacao.EtapaSemFaseAlguma),
        (Violacao.OrdemDeFaseDuplicada, Violacao.FaseCanonicaDuplicada),
        (Violacao.FaseCanonicaDuplicada, Violacao.FaseAgrupadoraSemEtapa),
        (Violacao.FaseAgrupadoraSemEtapa, Violacao.ArvoreDeExigenciasInconsistente),
        (Violacao.EtapaSemFaseAlguma, Violacao.ArvoreDeExigenciasInconsistente),
    ];

    public static TheoryData<Violacao, Violacao> Pares()
    {
        TheoryData<Violacao, Violacao> pares = [];
        foreach ((Violacao primeira, Violacao segunda) in ParesVizinhos)
        {
            pares.Add(primeira, segunda);
        }

        return pares;
    }

    [Fact(DisplayName = "O grafo congelado sem violação é restaurado, de modo que cada recusa abaixo vem da violação aplicada")]
    public void Restaurar_GrafoSemViolacao_Aceita()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out VersaoConfiguracao versao);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, GrafoEmMontagem.Com().Montar());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Theory(DisplayName = "Cada violação, sozinha, é recusada com o código e a mensagem dela, sem alterar o agregado")]
    [MemberData(nameof(TodasAsViolacoes))]
    public void Restaurar_UmaViolacao_RecusaComCodigoEMensagem(Violacao violacao)
    {
        Result resultado = RestaurarSemMudarOAgregado(GrafoEmMontagem.Com(violacao));

        resultado.Error!.Code.Should().Be(Recusas[violacao].Codigo);
        resultado.Error.Message.Should().Be(Recusas[violacao].Mensagem);
    }

    [Theory(DisplayName = "O grafo que viola duas regras vizinhas é recusado pela primeira, na ordem da conferência, sem alterar o agregado")]
    [MemberData(nameof(Pares))]
    public void Restaurar_DuasViolacoes_RecusaAPrimeira(Violacao primeira, Violacao segunda)
    {
        Result resultado = RestaurarSemMudarOAgregado(GrafoEmMontagem.Com(primeira, segunda));

        resultado.Error!.Code.Should().Be(Recusas[primeira].Codigo);
    }

    [Fact(DisplayName = "A eliminação por ausência na etapa do ENEM, listada antes da que cita etapa inexistente, é a recusa: as regras de eliminação são conferidas uma a uma")]
    public void Restaurar_EliminacoesEmOrdemInversa_RecusaAPrimeiraDaLista()
    {
        Result resultado = RestaurarSemMudarOAgregado(GrafoEmMontagem.Com(
            Violacao.AusenciaNaEtapaDoEnem, Violacao.EliminacaoCitaEtapaInexistente));

        resultado.Error!.Code.Should().Be(Recusas[Violacao.AusenciaNaEtapaDoEnem].Codigo);
    }

    private static Result RestaurarSemMudarOAgregado(GrafoEmMontagem montagem)
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out VersaoConfiguracao versao);
        Estado antes = Estado.De(processo);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, montagem.Montar());

        resultado.IsFailure.Should().BeTrue();
        Estado.De(processo).Should().BeEquivalentTo(antes, "a validação do grafo acontece inteira antes de qualquer escrita no agregado");
        return resultado;
    }
}

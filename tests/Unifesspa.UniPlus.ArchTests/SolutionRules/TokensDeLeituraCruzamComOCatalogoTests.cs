namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

// `using` de namespace (não alias): os métodos de extensão `ToCodigo` de Seleção só entram
// em escopo assim. Os tipos homônimos do catálogo chegam por alias, para não colidir.
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using ComposicaoVagasDoCatalogo = Unifesspa.UniPlus.Configuracao.Domain.Enums.ComposicaoVagas;
using ComposicoesVagas = Unifesspa.UniPlus.Configuracao.Domain.Enums.ComposicoesVagas;
using NaturezaLegalDoCatalogo = Unifesspa.UniPlus.Configuracao.Domain.Enums.NaturezaLegal;
using NaturezasLegais = Unifesspa.UniPlus.Configuracao.Domain.Enums.NaturezasLegais;
using OrigemDataDoCatalogo = Unifesspa.UniPlus.Configuracao.Domain.Enums.OrigemDataFase;
using OrigensDataFase = Unifesspa.UniPlus.Configuracao.Domain.Enums.OrigensDataFase;
using RegraRemanejamentoDoCatalogo = Unifesspa.UniPlus.Configuracao.Domain.Enums.RegraRemanejamento;
using RegrasRemanejamento = Unifesspa.UniPlus.Configuracao.Domain.Enums.RegrasRemanejamento;

/// <summary>
/// Amarra os tokens que a leitura de Seleção emite aos que o catálogo de Configuração
/// publica para a MESMA entidade. Os quatro atributos abaixo são snapshot-copy (ADR-0061):
/// Seleção os congela do cadastro vivo, e depois os devolve na sua própria leitura.
/// </summary>
/// <remarks>
/// <para>
/// O mesmo consumidor recebe as duas rotas — <c>GET /api/configuracao/modalidades</c>
/// descreve a modalidade que <c>GET /api/selecao/processos-seletivos/{id}</c> referencia
/// dentro de <c>distribuicaoVagas[].modalidades[]</c>, e cruzar as duas é necessário para
/// distinguir a quantidade que o edital declara da que a Lei 12.711 calcula. Duas grafias
/// para o mesmo atributo fazem esse cruzamento comparar tokens que nunca casam (issue
/// #1294): a projeção de Seleção emitia <c>CotaReservada</c> onde o catálogo publica
/// <c>COTA_RESERVADA</c>, e a quantidade declarada de uma modalidade sumia da tela.
/// </para>
/// <para>
/// O pareamento é pelo NOME do membro, não pelo valor numérico: os dois enums são tipos
/// distintos por decisão de arquitetura (ADR-0042 — o Domain de um módulo não referencia o
/// do outro), e é o nome que os mantém reconhecíveis como o mesmo conceito. Um membro
/// renomeado de um lado só aparece aqui.
/// </para>
/// </remarks>
public sealed class TokensDeLeituraCruzamComOCatalogoTests
{
    [Fact(DisplayName = "Natureza legal: o token que Seleção lê é o que o catálogo publica")]
    public void NaturezaLegal_MesmoTokenNosDoisModulos() =>
        CadaMembroExcetoSentinela<NaturezaLegalModalidade, NaturezaLegalDoCatalogo>(
            NaturezaLegalModalidade.Nenhuma,
            static local => local.ToCodigo(),
            NaturezasLegais.ParaTokenCanonico);

    [Fact(DisplayName = "Composição de vagas: o token que Seleção lê é o que o catálogo publica")]
    public void ComposicaoVagas_MesmoTokenNosDoisModulos() =>
        CadaMembroExcetoSentinela<ComposicaoVagasModalidade, ComposicaoVagasDoCatalogo>(
            ComposicaoVagasModalidade.Nenhuma,
            static local => local.ToCodigo(),
            ComposicoesVagas.ParaTokenCanonico);

    [Fact(DisplayName = "Origem da data da fase: o token que Seleção lê é o que o catálogo publica")]
    public void OrigemDataFase_MesmoTokenNosDoisModulos() =>
        CadaMembroExcetoSentinela<OrigemDataFase, OrigemDataDoCatalogo>(
            OrigemDataFase.Nenhuma,
            static local => local.ToCodigo(),
            OrigensDataFase.ParaTokenCanonico);

    /// <summary>
    /// Remanejamento tem um caso a mais: <c>Nenhuma</c> não é sentinela de corrupção, e sim
    /// a modalidade que não remaneja. O catálogo a representa por ausência do campo, e a
    /// leitura de Seleção precisa fazer o mesmo — um token próprio ali seria vocabulário
    /// que a outra rota não publica, e o cruzamento voltaria a falhar.
    /// </summary>
    [Fact(DisplayName = "Regra de remanejamento: mesmo token, e a ausência é representada por null nos dois")]
    public void RegraRemanejamento_MesmoTokenEAusenciaEmAmbos()
    {
        CadaMembroExcetoSentinela<RegraRemanejamentoModalidade, RegraRemanejamentoDoCatalogo>(
            RegraRemanejamentoModalidade.Nenhuma,
            static local => local.ToCodigo()!,
            RegrasRemanejamento.ParaTokenCanonico);

        RegraRemanejamentoModalidade.Nenhuma.ToCodigo().Should().BeNull(
            "o catálogo publica ausência (o campo é opcional na origem) para a modalidade que não "
            + "remaneja — emitir um token aqui inventaria vocabulário que a outra rota não conhece");
    }

    private static void CadaMembroExcetoSentinela<TLocal, TCatalogo>(
        TLocal sentinela,
        Func<TLocal, string> tokenEmSelecao,
        Func<TCatalogo, string> tokenNoCatalogo)
        where TLocal : struct, Enum
        where TCatalogo : struct, Enum
    {
        foreach (TLocal local in Enum.GetValues<TLocal>().Where(v => !v.Equals(sentinela)))
        {
            string nome = local.ToString();

            Enum.TryParse(nome, ignoreCase: false, out TCatalogo noCatalogo).Should().BeTrue(
                "{0}.{1} não tem membro homônimo em {2} — os dois descrevem o mesmo atributo da "
                + "mesma entidade, e um renomeado sem o outro quebra o snapshot-copy em silêncio",
                typeof(TLocal).Name, nome, typeof(TCatalogo).Name);

            tokenEmSelecao(local).Should().Be(tokenNoCatalogo(noCatalogo),
                "a leitura de Seleção e o catálogo de Configuração descrevem {0}.{1} para o mesmo "
                + "consumidor — duas grafias fazem o cliente comparar tokens que nunca casam",
                typeof(TLocal).Name, nome);
        }
    }
}

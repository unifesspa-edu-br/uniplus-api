namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

// `using` de namespace (não alias): os métodos de extensão `ToCodigo` de Seleção só entram
// em escopo assim. Os tipos homônimos do catálogo chegam por alias, para não colidir.
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using ComposicaoVagasDoCatalogo = Unifesspa.UniPlus.Configuracao.Domain.Enums.ComposicaoVagas;
using NaturezaLegalDoCatalogo = Unifesspa.UniPlus.Configuracao.Domain.Enums.NaturezaLegal;
using OrigemDataDoCatalogo = Unifesspa.UniPlus.Configuracao.Domain.Enums.OrigemDataFase;
using RegraRemanejamentoDoCatalogo = Unifesspa.UniPlus.Configuracao.Domain.Enums.RegraRemanejamento;

/// <summary>
/// Amarra os códigos que a leitura de Seleção emite aos que o catálogo de Configuração
/// publica para a MESMA entidade. Os quatro atributos abaixo são cópia dos dados (ADR-0061):
/// Seleção os congela do cadastro vivo, e depois os devolve na sua própria leitura.
/// </summary>
/// <remarks>
/// <para>
/// O mesmo consumidor recebe as duas rotas — <c>GET /api/configuracao/modalidades</c>
/// descreve a modalidade que <c>GET /api/selecao/processos-seletivos/{id}</c> referencia
/// dentro de <c>distribuicaoVagas[].modalidades[]</c>, e cruzar as duas é necessário para
/// distinguir a quantidade que o edital declara da que a Lei 12.711 calcula. Os códigos
/// precisam coincidir para que o consumidor identifique a mesma modalidade nas duas rotas.
/// </para>
/// <para>
/// O pareamento é pelo NOME do membro, não pelo valor numérico: os dois enums são tipos
/// distintos por decisão de arquitetura (ADR-0042 — o Domain de um módulo não referencia o
/// do outro), e é o nome que os mantém reconhecíveis como o mesmo conceito. Um membro
/// renomeado de um lado só aparece aqui.
/// </para>
/// </remarks>
public sealed class CodigosDeLeituraCruzamComOCatalogoTests
{
    [Fact(DisplayName = "Natureza legal: o código que Seleção lê é o que o catálogo publica")]
    public void NaturezaLegal_MesmoCodigoNosDoisModulos() =>
        CadaMembroExceto<NaturezaLegalModalidade, NaturezaLegalDoCatalogo>(
            NaturezaLegalModalidade.Nenhuma,
            static local => local.ToCodigo(),
            CodigoDeContratoExtensions.Codigo);

    [Fact(DisplayName = "Composição de vagas: o código que Seleção lê é o que o catálogo publica")]
    public void ComposicaoVagas_MesmoCodigoNosDoisModulos() =>
        CadaMembroExceto<ComposicaoVagasModalidade, ComposicaoVagasDoCatalogo>(
            ComposicaoVagasModalidade.Nenhuma,
            static local => local.ToCodigo(),
            CodigoDeContratoExtensions.Codigo);

    [Fact(DisplayName = "Origem da data da fase: o código que Seleção lê é o que o catálogo publica")]
    public void OrigemDataFase_MesmoCodigoNosDoisModulos() =>
        CadaMembroExceto<OrigemDataFase, OrigemDataDoCatalogo>(
            OrigemDataFase.Nenhuma,
            static local => local.ToCodigo(),
            CodigoDeContratoExtensions.Codigo);

    /// <summary>
    /// Remanejamento tem um caso a mais: <c>Nenhuma</c> não indica corrupção, e sim
    /// a modalidade que não remaneja. O catálogo a representa por ausência do campo, e a
    /// leitura de Seleção precisa fazer o mesmo — um código próprio ali seria vocabulário
    /// que a outra rota não publica, e o cruzamento voltaria a falhar.
    /// </summary>
    [Fact(DisplayName = "Regra de remanejamento: mesmo código, e a ausência é representada por null nos dois")]
    public void RegraRemanejamento_MesmoCodigoEAusenciaEmAmbos()
    {
        CadaMembroExceto<RegraRemanejamentoModalidade, RegraRemanejamentoDoCatalogo>(
            RegraRemanejamentoModalidade.Nenhuma,
            static local => local.ToCodigo()!,
            CodigoDeContratoExtensions.Codigo);

        RegraRemanejamentoModalidade.Nenhuma.ToCodigo().Should().BeNull(
            "o catálogo publica ausência (o campo é opcional na origem) para a modalidade que não "
            + "remaneja — emitir um código aqui inventaria vocabulário que a outra rota não conhece");
    }

    private static void CadaMembroExceto<TLocal, TCatalogo>(
        TLocal excecao,
        Func<TLocal, string> codigoEmSelecao,
        Func<TCatalogo, string> codigoNoCatalogo)
        where TLocal : struct, Enum
        where TCatalogo : struct, Enum
    {
        foreach (TLocal local in Enum.GetValues<TLocal>().Where(v => !v.Equals(excecao)))
        {
            string nome = local.ToString();

            Enum.TryParse(nome, ignoreCase: false, out TCatalogo noCatalogo).Should().BeTrue(
                "{0}.{1} não tem membro homônimo em {2} — os dois descrevem o mesmo atributo da "
                + "mesma entidade, e um renomeado sem o outro quebra o snapshot-copy em silêncio",
                typeof(TLocal).Name, nome, typeof(TCatalogo).Name);

            codigoEmSelecao(local).Should().Be(codigoNoCatalogo(noCatalogo),
                "a leitura de Seleção e o catálogo de Configuração descrevem {0}.{1} para o mesmo "
                + "consumidor — duas grafias fazem o cliente comparar códigos que nunca casam",
                typeof(TLocal).Name, nome);
        }
    }
}

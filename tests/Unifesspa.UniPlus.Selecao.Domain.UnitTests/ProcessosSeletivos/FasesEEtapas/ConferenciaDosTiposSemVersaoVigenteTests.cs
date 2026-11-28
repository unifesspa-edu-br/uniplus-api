namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static CenarioDeProcesso;

/// <summary>O produto que declara um tipo de ato sem versão vigente no catálogo é recusado no campo do tipo, sem citar nome nem código.</summary>
public sealed class ConferenciaDosTiposSemVersaoVigenteTests
{
    private const string TipoSemCatalogo = "TIPO_FORA_DO_CATALOGO";

    private static readonly TiposDeAtoUnicosPorObjeto Catalogo = TiposDeAtoUnicosPorObjeto.ParaChecklist(
        new Dictionary<string, string>(),
        [],
        new Dictionary<string, ClassificacaoDoTipoDeAto> { ["RESULTADO_FINAL"] = new(EhResultado: true, CongelaConfiguracao: false) },
        new Dictionary<string, string> { ["RESULTADO_FINAL"] = "Resultado final" });

    private static ModeloDeFase FaseCom(params ProdutoDaFase[] produtos) => new ModeloDeFase("RESULTADO", 1).Publicando(produtos);

    private static ModeloDeEtapa EtapaCom(params ProdutoDaEtapa[] produtos) => new ModeloDeEtapa("Redação") with { Produtos = produtos };

    [Fact(DisplayName = "O tipo sem versão vigente, na fase e na etapa, é recusado uma vez por produto, no campo do tipo")]
    public void TipoSemVersao_FaseEEtapa_UmaRecusaPorProduto()
    {
        CronogramaDoProcesso cronograma = Fases(FaseCom(ProdutoDaFase.Criar("RESULTADO_FINAL", PapelDaPublicacaoPrevista.Definitivo), ProdutoDaFase.Criar(TipoSemCatalogo, null)));
        EtapasDoProcesso etapas = Etapas(EtapaCom(ProdutoDaEtapa.Criar(TipoSemCatalogo, null)));

        IReadOnlyList<FieldError> recusas = ConferenciaDosTiposDeAto.ConferirTiposSemVersaoVigente(cronograma, etapas, Catalogo);

        recusas.Select(static recusa => recusa.Field).Should().Equal("fases[0].produtos[1].atoCodigo", "etapas[0].produtos[0].atoCodigo");
        recusas.Should().OnlyContain(static recusa => recusa.Error.Code == "ProcessoSeletivo.TipoDeAtoSemVersaoVigente");
    }

    [Fact(DisplayName = "A mensagem diz que o tipo do item não tem versão vigente e não contém o código do tipo")]
    public void TipoSemVersao_Mensagem_NaoCitaOCodigo()
    {
        CronogramaDoProcesso cronograma = Fases(FaseCom(ProdutoDaFase.Criar(TipoSemCatalogo, null)));

        string mensagem = ConferenciaDosTiposDeAto.ConferirTiposSemVersaoVigente(cronograma, Etapas(), Catalogo).Single().Error.Message;

        mensagem.Should().Be("O tipo de ato declarado no item não tem versão vigente no catálogo de Publicações, e a publicação congela a classificação de cada tipo declarado.");
        mensagem.Should().NotContain(TipoSemCatalogo);
    }

    [Fact(DisplayName = "O tipo com versão vigente não é recusado")]
    public void TipoComVersao_SemRecusa() =>
        ConferenciaDosTiposDeAto.ConferirTiposSemVersaoVigente(
            Fases(FaseCom(ProdutoDaFase.Criar("RESULTADO_FINAL", PapelDaPublicacaoPrevista.Definitivo))), Etapas(), Catalogo).Should().BeEmpty();
}

namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado.ProcessoDaImportacaoDeTeste;

/// <summary>Os itens do painel da importação do resultado dizem o mesmo que a conferência da publicação.</summary>
public sealed class ItensDaImportacaoNoPainelTests
{
    private const string ItemDaImportacao = "importacao_do_resultado";
    private const string ItemDoFormulario = "formulario_pos_importacao_sem_obrigatorio";

    [Fact(DisplayName = "Com a importação declarada e o formulário obrigatório, os dois itens ficam conformes")]
    public void ImportadoConforme_ItensOk()
    {
        ProcessoSeletivo processo = ImportadoConforme();

        Item(processo, ItemDaImportacao).Ok.Should().BeTrue();
        Item(processo, ItemDoFormulario).Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "O item da importação pende quando a declaração falta, e o do formulário continua conforme")]
    public void ImportadoSemDeclaracao_ItemDaImportacaoPende()
    {
        ProcessoSeletivo processo = ImportadoConforme();
        processo.DefinirImportacaoDoResultado([], null).IsSuccess.Should().BeTrue();

        Item(processo, ItemDaImportacao).Ok.Should().BeFalse();
        Item(processo, ItemDaImportacao).Dimensao.Should().Be(DimensaoConformidade.ImportacaoDoResultado);
        Item(processo, ItemDoFormulario).Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "O item do formulário pende sem o formulário obrigatório, e só ele, sem tocar nos itens da inscrição nem da isenção")]
    public void SemFormulario_SoOItemDoFormularioPende()
    {
        ProcessoSeletivo processo = ImportadoConforme();
        processo.RemoverFormulario(PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();

        Item(processo, ItemDoFormulario).Ok.Should().BeFalse();
        Item(processo, ItemDaImportacao).Ok.Should().BeTrue();
        Item(processo, "formulario_inscricao_ausente").Ok.Should().BeTrue();
        Item(processo, "formulario_isencao_sem_taxa").Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "Os itens concordam com a conferência da publicação em cada estado do processo")]
    public void ItensConcordamComAConferencia()
    {
        ProcessoSeletivo processo = ImportadoConforme();
        Item(processo, ItemDaImportacao).Ok.Should().Be(CodigosDaImportacao(processo).Count == 0);

        processo.DefinirImportacaoDoResultado(UmRelatorio, null).IsSuccess.Should().BeTrue();

        Item(processo, ItemDaImportacao).Ok.Should().Be(CodigosDaImportacao(processo).Count == 0);
    }

    [Fact(DisplayName = "O processo com inscrição pelo sistema sem declaração tem os dois itens conformes")]
    public void InscricaoPeloSistema_ItensOk()
    {
        ProcessoSeletivo processo = InscricaoPeloSistema();

        Item(processo, ItemDaImportacao).Ok.Should().BeTrue();
        Item(processo, ItemDoFormulario).Ok.Should().BeTrue();
    }
}

namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.VagasDoDocumentoDeTeste;

public sealed class LeituraDoFadaTests
{
    private static FadaLido Ler(FadaNoDocumento? fada) => LeituraDoFada.Ler(fada, Cadastros());

    private static FadaNoDocumento SemConfiguracao(bool aplica) => new(aplica, null, null, null, null, null);

    [Fact(DisplayName = "O bloco ausente não declara se o processo aplica o Fator de Atenuação das Desigualdades de Acesso")]
    public void BlocoAusente_NaoDeclaraNada()
    {
        FadaLido lido = Ler(null);

        lido.Aplica.Should().BeNull();
        lido.Fada.Should().BeNull();
        lido.Recusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O processo que aplica e traz a configuração conforme a tem lida, com a base legal congelada do cadastro")]
    public void Aplica_ComConfiguracaoConforme_ViraAConfiguracao()
    {
        FadaLido lido = Ler(FadaConforme());

        lido.Recusadas.Should().BeEmpty();
        lido.Aplica.Should().BeTrue();
        lido.Fada!.Fator.Should().Be(1.2m);
        lido.Fada.BaseLegalCodigo.Should().Be(BaseLegalDoFada);
    }

    [Fact(DisplayName = "O processo que não aplica e não traz configuração fica declarado como não aplicado")]
    public void NaoAplica_SemConfiguracao_FicaDeclarado()
    {
        FadaLido lido = Ler(SemConfiguracao(aplica: false));

        lido.Aplica.Should().BeFalse();
        lido.Fada.Should().BeNull();
        lido.Recusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O processo que não aplica e traz a configuração é recusado no bloco, com a recusa da declaração")]
    public void NaoAplica_ComConfiguracao_Recusa()
    {
        FadaLido lido = Ler(FadaConforme(aplica: false));

        lido.Aplica.Should().BeNull();
        lido.Fada.Should().BeNull();
        Causas(lido.Recusadas).Should().Equal(("/fada", ProcessoSeletivoErrorCodes.FadaNaoAplicaComConfiguracao));
    }

    [Fact(DisplayName = "O processo que aplica e não traz configuração alguma fica declarado, e a conformidade da configuração o recusa")]
    public void Aplica_SemConfiguracao_FicaDeclarado()
    {
        FadaLido lido = Ler(SemConfiguracao(aplica: true));

        lido.Aplica.Should().BeTrue();
        lido.Fada.Should().BeNull();
        lido.Recusadas.Should().BeEmpty();
    }

    [Theory(DisplayName = "A regra que o catálogo não tem, ou que é de outro tipo, recusa no campo do código da regra")]
    [InlineData(false, "ConfiguracaoDoFada.RegraNaoEncontrada")]
    [InlineData(true, "ConfiguracaoDoFada.RegraTipoInvalido")]
    public void Regra_ForaDoCatalogoOuDeOutroTipo_Recusa(bool deOutroTipo, string codigo)
    {
        FadaNoDocumento fada = FadaConforme() with { Regra = deOutroTipo ? Institucional : new(TextoDigitado, "v1") };

        FadaLido lido = Ler(fada);

        Causas(lido.Recusadas).Should().Equal(("/fada/regra/codigo", codigo));
        lido.Recusadas.Single().Causa.Error.Message.Should().NotContain(TextoDigitado);
        lido.Aplica.Should().BeNull();
    }

    [Fact(DisplayName = "A base legal que o cadastro não tem recusa no campo do código dela, sem repetir o texto")]
    public void BaseLegalInexistente_RecusaNoCampoDoCodigo()
    {
        FadaLido lido = Ler(FadaConforme() with { BaseLegalCodigo = TextoDigitado });

        Causas(lido.Recusadas).Should().Equal(("/fada/baseLegalCodigo", "ConfiguracaoDoFada.BaseLegalNaoEncontrada"));
        lido.Recusadas.Single().Causa.Error.Message.Should().NotContain(TextoDigitado);
    }

    [Fact(DisplayName = "Os campos que faltam à configuração de quem aplica são recusados cada um no seu campo")]
    public void CamposQueFaltam_RecusadosCadaUmNoSeuCampo()
    {
        FadaNoDocumento fada = new(true, new(null, null), null, 1.5m, null, null);

        Causas(Ler(fada).Recusadas).Should().Equal(
            ("/fada/regra/codigo", "ConfiguracaoDoFada.CamposObrigatorios"),
            ("/fada/regra/versao", "ConfiguracaoDoFada.CamposObrigatorios"),
            ("/fada/fator", "ConfiguracaoDoFada.CamposObrigatorios"),
            ("/fada/baseLegalCodigo", "ConfiguracaoDoFada.CamposObrigatorios"));
    }

    [Fact(DisplayName = "O fator e o teto que a configuração recusa voltam juntos, cada um no seu campo, e o dado de elegibilidade no campo do documento")]
    public void RecusasDaConfiguracao_Acumulam()
    {
        FadaNoDocumento fada = FadaConforme() with { Fator = 0m, Teto = -1m, CodigoDoDadoDeElegibilidade = "inválido" };

        Causas(Ler(fada).Recusadas).Should().Equal(
            ("/fada/fator", "ConfiguracaoDoFada.FatorInvalido"),
            ("/fada/teto", "ConfiguracaoDoFada.TetoInvalido"),
            ("/fada/codigoDoDadoDeElegibilidade", "ConfiguracaoDoFada.FatoElegibilidadeInvalido"));
    }
}

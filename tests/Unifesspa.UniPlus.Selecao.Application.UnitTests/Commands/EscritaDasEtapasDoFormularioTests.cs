namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A leitura das etapas do formulário do processo: tipo e bloco são lidos antes de a etapa ser montada.</summary>
public sealed class EscritaDasEtapasDoFormularioTests
{
    [Fact(DisplayName = "A seção sem bloco e o bloco de sistema com o código do bloco são lidos como etapas")]
    public void Ler_SecaoEBlocoDeSistema_MontaAsEtapas()
    {
        EtapasDoFormularioLidas lidas = EscritaDasEtapasDoFormulario.Ler(
        [
            EntradaDaEtapaFormularioDeTeste.Secao("DADOS", 0).Construir(),
            EntradaDaEtapaFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "REVISAO_E_ACEITE").Construir(),
        ]);

        lidas.Erros.Should().BeEmpty();
        lidas.Etapas.Select(static e => (e.Tipo, e.Bloco)).Should().Equal(
            (TipoEtapaFormulario.Secao, (BlocoSistema?)null), (TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite));
    }

    [Fact(DisplayName = "O tipo e o bloco que não são códigos do contrato são recusados nos próprios campos e a etapa não é montada")]
    public void Ler_TipoEBlocoDesconhecidos_RecusaCadaCampoSemMontarAEtapa()
    {
        EtapaFormularioInput etapa = EntradaDaEtapaFormularioDeTeste.Secao("DADOS", 0).Construir() with { Tipo = "secao", Bloco = "RODAPE" };

        EtapasDoFormularioLidas lidas = EscritaDasEtapasDoFormulario.Ler([etapa]);

        lidas.Etapas.Should().BeEmpty();
        lidas.Erros.Select(static e => (e.Field!, e.Error.Code)).Should().Equal(
            ("etapas[0].tipo", EstruturaFormularioErrorCodes.TipoDeEtapaObrigatorio),
            ("etapas[0].bloco", EstruturaFormularioErrorCodes.BlocoNaoAdmitido));
    }

    [Fact(DisplayName = "A etapa com tipo e bloco lidos mas com forma inválida é recusada pela forma, no caminho da etapa")]
    public void Ler_FormaInvalidaDaEtapa_RecusaPelaForma()
    {
        EtapaFormularioInput etapa = EntradaDaEtapaFormularioDeTeste.Secao(string.Empty, 0).ComTitulo("Dados").Construir();

        EtapasDoFormularioLidas lidas = EscritaDasEtapasDoFormulario.Ler([etapa]);

        lidas.Etapas.Should().BeEmpty();
        lidas.Erros.Select(static e => (e.Field!, e.Error.Code)).Should().Equal(("etapas[0].codigo", EstruturaFormularioErrorCodes.EtapaCodigoInvalido));
    }
}

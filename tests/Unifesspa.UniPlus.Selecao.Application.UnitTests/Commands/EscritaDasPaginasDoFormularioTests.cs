namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A leitura das paginas do formulário do processo: tipo e bloco são lidos antes de a pagina ser montada.</summary>
public sealed class EscritaDasPaginasDoFormularioTests
{
    [Fact(DisplayName = "A seção sem bloco e o bloco de sistema com o código do bloco são lidos como paginas")]
    public void Ler_SecaoEBlocoDeSistema_MontaAsPaginas()
    {
        PaginasDoFormularioLidas lidas = EscritaDasPaginasDoFormulario.Ler(
        [
            EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS", 0).Construir(),
            EntradaDaPaginaDoFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "REVISAO_E_ACEITE").Construir(),
        ]);

        lidas.Erros.Should().BeEmpty();
        lidas.Paginas.Select(static e => (e.Tipo, e.Bloco)).Should().Equal(
            (TipoPaginaFormulario.Secao, (BlocoSistema?)null), (TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite));
    }

    [Fact(DisplayName = "O tipo e o bloco que não são códigos do contrato são recusados nos próprios campos e a pagina não é montada")]
    public void Ler_TipoEBlocoDesconhecidos_RecusaCadaCampoSemMontarAPagina()
    {
        PaginaDoFormularioInput pagina = EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS", 0).Construir() with { Tipo = "secao", Bloco = "RODAPE" };

        PaginasDoFormularioLidas lidas = EscritaDasPaginasDoFormulario.Ler([pagina]);

        lidas.Paginas.Should().BeEmpty();
        lidas.Erros.Select(static e => (e.Field!, e.Error.Code)).Should().Equal(
            ("paginas[0].tipo", EstruturaFormularioErrorCodes.TipoDePaginaObrigatorio),
            ("paginas[0].bloco", EstruturaFormularioErrorCodes.BlocoNaoAdmitido));
    }

    [Fact(DisplayName = "A pagina com tipo e bloco lidos mas com forma inválida é recusada pela forma, no caminho da pagina")]
    public void Ler_FormaInvalidaDaPagina_RecusaPelaForma()
    {
        PaginaDoFormularioInput pagina = EntradaDaPaginaDoFormularioDeTeste.Secao(string.Empty, 0).ComTitulo("Dados").Construir();

        PaginasDoFormularioLidas lidas = EscritaDasPaginasDoFormulario.Ler([pagina]);

        lidas.Paginas.Should().BeEmpty();
        lidas.Erros.Select(static e => (e.Field!, e.Error.Code)).Should().Equal(("paginas[0].codigo", EstruturaFormularioErrorCodes.PaginaCodigoInvalido));
    }
}

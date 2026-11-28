namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A ordem ausente de página, item, grupo e termo é recusada como obrigatória; o zero continua sendo ordem válida.</summary>
public sealed class OrdemAusenteNaFormaDoFormularioTests
{
    private static (string? Campo, string Codigo)[] Recusas(IEnumerable<FieldError> erros) =>
        [.. erros.Select(static e => (e.Field, e.Error.Code))];

    [Fact(DisplayName = "O item sem ordem é recusado como obrigatório, junto do rótulo em branco")]
    public void Item_OrdemAusente_RecusaComoObrigatoria() =>
        Recusas(FormaDoItem.ValidarFormaBasica("NOME", null, " "))
            .Should().Equal(("ordem", CodigosDeValidacao.Obrigatorio), ("apresentacao.rotulo", ItemFormularioErrorCodes.RotuloObrigatorio));

    [Fact(DisplayName = "A página sem ordem é recusada como obrigatória")]
    public void Pagina_OrdemAusente_RecusaComoObrigatoria() =>
        Recusas(FormaDaPagina.Conferir("DADOS", null, TipoPaginaFormulario.Secao, "Título", null, null, false))
            .Should().Equal(("ordem", CodigosDeValidacao.Obrigatorio));

    [Fact(DisplayName = "O termo sem ordem é recusado como obrigatório")]
    public void Termo_OrdemAusente_RecusaComoObrigatoria() =>
        Recusas(FormaDoTermo.ValidarFormaBasica("TERMO_LGPD", null)).Should().Equal(("ordem", CodigosDeValidacao.Obrigatorio));

    [Fact(DisplayName = "Dois termos sem ordem não são recusados como ordem repetida")]
    public void Termos_DuasOrdensAusentes_NaoRecusaOrdemDuplicada() =>
        FormaDoTermo.ConferirUnicidade([("TERMO_A", null), ("TERMO_B", null)]).Should().BeEmpty();

    [Fact(DisplayName = "A ordem zero é válida na página, no item e no termo")]
    public void OrdemZero_NaoERecusada()
    {
        FormaDoItem.ValidarFormaBasica("NOME", 0, "Nome").Should().BeEmpty();
        FormaDaPagina.Conferir("DADOS", 0, TipoPaginaFormulario.Secao, "Título", null, null, false).Should().BeEmpty();
        FormaDoTermo.ValidarFormaBasica("TERMO_LGPD", 0).Should().BeEmpty();
    }

    [Fact(DisplayName = "O conjunto básico mescla itens e grupos sem ordem sem falhar, e deixa a ausência para a recusa")]
    public void ConjuntoBasico_OrdemAusente_NaoFalhaEMantemAAusencia()
    {
        FatoColetadoInput semOrdem = EntradaDoFatoColetadoDeTeste.Coletando("MAIOR_IDADE").ComRotulo("Maior de idade").DoTipo("SIM_OU_NAO").Construir() with { Ordem = null };

        (IReadOnlyList<FatoColetadoInput> itens, _, _) =
            ConjuntoBasicoDaInscricao.MesclarItens([semOrdem], [], ConjuntoBasicoDaInscricao.Referencia([]));

        itens.Single(static i => i.FatoCodigo == "MAIOR_IDADE").Ordem.Should().BeNull();
    }

    [Fact(DisplayName = "O item básico igual à referência, mas sem ordem, não é coberto pela ordem da referência")]
    public void ConjuntoBasico_BasicoSemOrdem_MantemAAusenciaParaARecusa()
    {
        FatoColetadoInput nome = ConjuntoBasicoDaInscricao.Itens[0];

        (IReadOnlyList<FatoColetadoInput> itens, _, List<FieldError> erros) =
            ConjuntoBasicoDaInscricao.MesclarItens([nome with { Ordem = null }], [], ConjuntoBasicoDaInscricao.Itens);

        erros.Should().BeEmpty();
        itens.Where(i => i.FatoCodigo == nome.FatoCodigo).Should().ContainSingle().Which.Ordem.Should().BeNull();
        FormaDoItem.ValidarFormaBasica(nome.FatoCodigo, itens.Single(i => i.FatoCodigo == nome.FatoCodigo).Ordem, nome.Apresentacao!.Rotulo)
            .Should().ContainSingle().Which.Error.Code.Should().Be(CodigosDeValidacao.Obrigatorio);
    }
}

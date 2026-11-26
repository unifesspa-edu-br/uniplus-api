namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O que os formulários coletaram nas versões publicadas (UNI-REQ-0144): o fato garantido de um
/// formulário é o que ele coletou em todas as versões em que existia.
/// </summary>
public sealed class FatosDasVersoesPublicadasTests
{
    private static readonly FormularioProcesso Inscricao = LigacaoAoFormulario.FormularioNoPapel(PapelDoFormularioDeTeste.Inscricao);
    private static readonly FormularioProcesso Habilitacao = LigacaoAoFormulario.FormularioNoPapel(PapelDoFormularioDeTeste.Habilitacao);
    private static readonly CodigoDoFormulario CodigoDaInscricao = FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Inscricao);
    private static readonly CodigoDoFormulario CodigoDaHabilitacao = FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Habilitacao);

    private static FatoColetado Item(string codigo) => Item(codigo, Inscricao);

    private static FatoColetado Item(string codigo, FormularioProcesso formulario) =>
        FatoColetadoDeTeste.Coletando(codigo, 0).ComObrigatoriedade(Obrigatoriedade.Sempre).Construir().Em(formulario);

    [Fact(DisplayName = "Fato coletado em todas as versões é garantido; o que só a versão base coleta, não")]
    public void Garante_SoOQueTodasAsVersoesColetaram()
    {
        FatosDasVersoesPublicadas fatos = VersoesPublicadasDeTeste.SemEixosCongelados(
        [
            VersoesPublicadasDeTeste.Versao([Inscricao], [Item("TEM_RENDA")]),
            VersoesPublicadasDeTeste.Versao([Inscricao], [Item("TEM_RENDA"), Item("NOVO")]),
        ]);

        fatos.Garante(CodigoDaInscricao, "TEM_RENDA").Should().BeTrue();
        fatos.Garante(CodigoDaInscricao, "NOVO").Should().BeFalse("quem se inscreveu na primeira versão não o informou");
    }

    [Fact(DisplayName = "A versão em que o formulário não existia não entra no que ele garante")]
    public void Garante_IgnoraVersaoSemOFormulario()
    {
        FatosDasVersoesPublicadas fatos = VersoesPublicadasDeTeste.SemEixosCongelados(
        [
            VersoesPublicadasDeTeste.Versao([Inscricao], [Item("TEM_RENDA")]),
            VersoesPublicadasDeTeste.Versao([Inscricao, Habilitacao], [Item("TEM_RENDA"), Item("COMPROVANTE", Habilitacao)]),
        ]);

        fatos.Garante(CodigoDaHabilitacao, "COMPROVANTE").Should().BeTrue("ninguém preencheu a habilitação na versão que não a tinha");
    }

    [Fact(DisplayName = "Formulário que nenhuma versão tinha garante o que produz: o formulário nasce na retificação")]
    public void Garante_FormularioSemVersaoPublicada()
    {
        FatosDasVersoesPublicadas fatos = VersoesPublicadasDeTeste.SemEixosCongelados([VersoesPublicadasDeTeste.Versao([Inscricao], [Item("TEM_RENDA")])]);

        fatos.Garante(CodigoDaHabilitacao, "COMPROVANTE").Should().BeTrue();
    }

    [Fact(DisplayName = "O código do grupo e os campos dele contam como coletados pelo formulário")]
    public void Garante_GrupoECamposDoGrupo()
    {
        GrupoColetado grupo = GrupoColetadoDeTeste.Agrupando("FAMILIA", 1)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComCampos(FatoColetadoDeTeste.Coletando("MEMBRO_RENDA", 0).ComRotulo("Renda").ComObrigatoriedade(Obrigatoriedade.Sempre).Construir())
            .Construir()
            .Em(Inscricao);

        FatosDasVersoesPublicadas fatos = VersoesPublicadasDeTeste.SemEixosCongelados([VersoesPublicadasDeTeste.Versao([Inscricao], [Item("TEM_RENDA")], [grupo])]);

        fatos.Garante(CodigoDaInscricao, "FAMILIA").Should().BeTrue();
        fatos.Garante(CodigoDaInscricao, "MEMBRO_RENDA").Should().BeTrue();
    }
}

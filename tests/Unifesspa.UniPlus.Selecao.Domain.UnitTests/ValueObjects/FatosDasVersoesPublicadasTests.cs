namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O que os formulários coletaram nas versões publicadas (UNI-REQ-0144): o fato garantido de uma
/// finalidade é o que o formulário dela coletou em todas as versões em que existia.
/// </summary>
public sealed class FatosDasVersoesPublicadasTests
{
    private static readonly FormularioProcesso Inscricao = Formulario(FinalidadeFormulario.Inscricao);
    private static readonly FormularioProcesso Habilitacao = Formulario(FinalidadeFormulario.Habilitacao);

    private static FormularioProcesso Formulario(FinalidadeFormulario finalidade) =>
        FormularioProcesso.Criar(finalidade, null, null, FormularioDeTeste.Etapas(finalidade)).Value!;

    private static FatoColetado Item(string codigo, FinalidadeFormulario finalidade = FinalidadeFormulario.Inscricao) =>
        FatoColetado.Criar(codigo, 0, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, finalidade: finalidade, classificacaoProtecao: "PESSOAL").Value!;

    [Fact(DisplayName = "Fato coletado em todas as versões é garantido; o que só a versão base coleta, não")]
    public void Garante_SoOQueTodasAsVersoesColetaram()
    {
        FatosDasVersoesPublicadas fatos = FatosDasVersoesPublicadas.De(
        [
            VersoesPublicadasDeTeste.Versao([Inscricao], [Item("TEM_RENDA")]),
            VersoesPublicadasDeTeste.Versao([Inscricao], [Item("TEM_RENDA"), Item("NOVO")]),
        ]);

        fatos.Garante(FinalidadeFormulario.Inscricao, "TEM_RENDA").Should().BeTrue();
        fatos.Garante(FinalidadeFormulario.Inscricao, "NOVO").Should().BeFalse("quem se inscreveu na primeira versão não o informou");
    }

    [Fact(DisplayName = "A versão em que a finalidade não tinha formulário não entra no que ela garante")]
    public void Garante_IgnoraVersaoSemOFormularioDaFinalidade()
    {
        FatosDasVersoesPublicadas fatos = FatosDasVersoesPublicadas.De(
        [
            VersoesPublicadasDeTeste.Versao([Inscricao], [Item("TEM_RENDA")]),
            VersoesPublicadasDeTeste.Versao([Inscricao, Habilitacao], [Item("TEM_RENDA"), Item("COMPROVANTE", FinalidadeFormulario.Habilitacao)]),
        ]);

        fatos.Garante(FinalidadeFormulario.Habilitacao, "COMPROVANTE").Should().BeTrue("ninguém preencheu a habilitação na versão que não a tinha");
    }

    [Fact(DisplayName = "Finalidade sem formulário em nenhuma versão garante o que produz: o formulário nasce na retificação")]
    public void Garante_FinalidadeSemFormularioPublicado()
    {
        FatosDasVersoesPublicadas fatos = FatosDasVersoesPublicadas.De([VersoesPublicadasDeTeste.Versao([Inscricao], [Item("TEM_RENDA")])]);

        fatos.Garante(FinalidadeFormulario.Habilitacao, "COMPROVANTE").Should().BeTrue();
    }

    [Fact(DisplayName = "O código do grupo e os campos dele contam como coletados pela finalidade")]
    public void Garante_GrupoECamposDoGrupo()
    {
        GrupoColetado grupo = GrupoColetado.Criar(
            "FAMILIA", 1, FormularioDeTeste.Secao, "Composição familiar", 0, 5, null, Obrigatoriedade.Sempre,
            [FatoColetado.Criar("MEMBRO_RENDA", 0, "Renda", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL").Value!],
            FinalidadeFormulario.Inscricao).Value!;

        FatosDasVersoesPublicadas fatos = FatosDasVersoesPublicadas.De([VersoesPublicadasDeTeste.Versao([Inscricao], [Item("TEM_RENDA")], [grupo])]);

        fatos.Garante(FinalidadeFormulario.Inscricao, "FAMILIA").Should().BeTrue();
        fatos.Garante(FinalidadeFormulario.Inscricao, "MEMBRO_RENDA").Should().BeTrue();
    }
}

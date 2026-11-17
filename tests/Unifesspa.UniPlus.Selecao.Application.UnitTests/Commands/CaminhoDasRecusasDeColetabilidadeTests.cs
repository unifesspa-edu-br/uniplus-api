namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A recusa do fato que o catálogo deixou de aceitar aponta o campo do documento que o cita, <c>fatoCodigo</c> do item ou do campo
/// do grupo, no mesmo caminho que as demais conferências do formulário dão, e sai na ordem do documento: por formulário, os itens
/// e depois os grupos.
/// </summary>
public sealed class CaminhoDasRecusasDeColetabilidadeTests
{
    private const string NaoMaisDeclarado = "ProcessoSeletivo.FatoColetadoNaoMaisDeclarado";

    [Fact(DisplayName = "O item e o campo do grupo recusados apontam o fatoCodigo no formulário da finalidade")]
    public void ItemECampoDoGrupo_ApontamOFatoCodigoNoFormulario()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Result gravado = processo.DefinirItens(
            [Item("ITEM_A", 0), Item("ITEM_B", 1)], grupos: [Grupo("COMPOSICAO", 2, Item("CAMPO_A", 0), Item("CAMPO_B", 1))], finalidade: FinalidadeFormulario.Habilitacao);
        gravado.IsSuccess.Should().BeTrue(string.Join(";", gravado.Errors.Select(static e => e.Error.Message)));

        Result resultado = ConferenciaDeColetabilidadeDeFatos.Conferir(processo, CatalogoSemNada());

        resultado.Errors.Select(static e => e.Field).Should().Equal(
            "formularios[HABILITACAO].itens[0].fatoCodigo",
            "formularios[HABILITACAO].itens[1].fatoCodigo",
            "formularios[HABILITACAO].grupos[0].subitens[0].fatoCodigo",
            "formularios[HABILITACAO].grupos[0].subitens[1].fatoCodigo");
        resultado.Errors.Select(static e => e.Error.Code).Should().OnlyContain(codigo => codigo == NaoMaisDeclarado);
    }

    [Fact(DisplayName = "Com mais de uma finalidade, a ordem do documento é por formulário, com os itens antes dos grupos de cada um")]
    public void MaisDeUmaFinalidade_OrdemDoDocumentoEPorFormulario()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens([Item("HAB_ITEM", 0)], grupos: [Grupo("COMPOSICAO", 1, Item("HAB_CAMPO", 0))], finalidade: FinalidadeFormulario.Habilitacao)
            .IsSuccess.Should().BeTrue();
        processo.DefinirItens([Item("ISEN_ITEM", 0)], finalidade: FinalidadeFormulario.IsencaoTaxa).IsSuccess.Should().BeTrue();

        IReadOnlyList<ItemNoDocumento> naoColetaveis = ConferenciaDeColetabilidadeDeFatos.NaoColetaveis(processo, CatalogoSemNada());

        naoColetaveis.Select(static n => n.Item.FatoCodigo).Should().Equal("ISEN_ITEM", "HAB_ITEM", "HAB_CAMPO");
        naoColetaveis.Select(static n => n.Caminho).Should().Equal(
            "formularios[ISENCAO_TAXA].itens[0]", "formularios[HABILITACAO].itens[0]", "formularios[HABILITACAO].grupos[0].subitens[0]");
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS Caminho da coletabilidade", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static Dictionary<string, FatoCandidatoView> CatalogoSemNada() => new(StringComparer.Ordinal);

    private static FatoColetado Item(string codigo, int ordem) =>
        FatoColetado.Criar(
            codigo, ordem, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL",
            hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade,
            descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    private static GrupoColetado Grupo(string codigo, int ordem, params FatoColetado[] campos) =>
        GrupoColetado.Criar(codigo, ordem, FormularioDeTeste.Secao, "Composição familiar", 0, 5, null, Obrigatoriedade.Nunca, campos).Value!;
}

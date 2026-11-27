namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.DeclaracoesDeModeloDeTeste;

/// <summary>Os pressupostos do modelo (UNI-REQ-0144): fato coletado pela inscrição, fora do formulário que o cita.</summary>
public sealed class ModeloFormularioPressupostosTests
{
    private static readonly Dictionary<string, IReadOnlyCollection<string>> SemDerivacoes = new(StringComparer.Ordinal);

    private static readonly PaginaDoModelo Dados = new("DADOS", 0, TipoPaginaFormulario.Secao, null, "Dados", null, null, null);
    private static readonly PaginaDoModelo Revisao = new("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null);

    private static ItemDoModelo Item(string fato, int ordem, string? pagina = "DADOS") =>
        new(fato, ordem, pagina, new ApresentacaoDoCampo(fato, TipoDeCampo.SimOuNao), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, []));

    private static ConteudoDoModelo Conteudo(IReadOnlyList<ItemDoModelo> itens, IReadOnlyList<GrupoDoModelo> grupos, params string[] pressupostos) =>
        new("Habilitação", [Dados, Revisao], itens, [], pressupostos, grupos);

    private static Result<ModeloFormulario> Criar(DeclaracaoDoFormulario declaracao, ConteudoDoModelo conteudo) =>
        ModeloFormulario.Criar("HABILITACAO_MEDICINA", new DescritivoDoModelo("Habilitação Medicina 2027", null, "REGULAR_UNIFICADO"), declaracao, conteudo, SemDerivacoes);

    [Fact(DisplayName = "Modelo que serve à fase de inscrição não tem pressupostos")]
    public void Inscricao_ComPressuposto_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(DaInscricao, Conteudo([Item("COR_RACA", 0)], [], "SEXO"));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.PressupostoNaInscricao);
    }

    [Fact(DisplayName = "Fato não é pressuposto e item do mesmo modelo")]
    public void PressupostoTambemItem_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(DeOutraFaseObrigatoria, Conteudo([Item("CONCLUSAO_REGULAR", 0)], [], "CONCLUSAO_REGULAR"));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.PressupostoTambemItem);
    }

    [Fact(DisplayName = "Fato não é pressuposto e campo de grupo do mesmo modelo")]
    public void PressupostoTambemCampoDeGrupo_Recusa()
    {
        GrupoDoModelo composicao = new(
            "COMPOSICAO_FAMILIAR", 1, "DADOS", "Composição familiar", new OcorrenciasDoGrupo(1, 10, IncluiCandidato: false),
            new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre), [Item("RENDA", 0, pagina: null)]);

        Result<ModeloFormulario> modelo = Criar(DeOutraFaseObrigatoria, Conteudo([Item("CONCLUSAO_REGULAR", 0)], [composicao], "RENDA"));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.PressupostoTambemItem);
    }

    [Fact(DisplayName = "Pressuposto em branco ou repetido é recusado com o índice de cada um")]
    public void PressupostoEmBrancoERepetido_RecusaNoIndice()
    {
        Result<ModeloFormulario> modelo = Criar(DeOutraFaseObrigatoria, Conteudo([Item("CONCLUSAO_REGULAR", 0)], [], "SEXO", "", "SEXO"));

        modelo.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
        [
            ("conteudo.pressupostos[1]", ModeloFormularioErrorCodes.PressupostoEmBranco),
            ("conteudo.pressupostos[2]", ModeloFormularioErrorCodes.PressupostoRepetido),
        ]);
    }

    [Fact(DisplayName = "Pressuposto repetido e também item é recusado nessa ordem, por ocorrência")]
    public void PressupostoRepetidoETambemItem_RecusaNaOrdem()
    {
        Result<ModeloFormulario> modelo = Criar(DeOutraFaseObrigatoria, Conteudo([Item("CONCLUSAO_REGULAR", 0)], [], "CONCLUSAO_REGULAR", "CONCLUSAO_REGULAR"));

        modelo.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
        [
            ("conteudo.pressupostos[0]", ModeloFormularioErrorCodes.PressupostoTambemItem),
            ("conteudo.pressupostos[1]", ModeloFormularioErrorCodes.PressupostoRepetido),
            ("conteudo.pressupostos[1]", ModeloFormularioErrorCodes.PressupostoTambemItem),
        ]);
    }
}

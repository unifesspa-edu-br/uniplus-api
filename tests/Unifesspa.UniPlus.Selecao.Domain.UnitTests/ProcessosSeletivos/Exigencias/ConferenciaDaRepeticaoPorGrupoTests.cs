namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Exigencias;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// A exigência que repete por entidade nomeia um grupo repetível coletado no formulário que a entrega ou num
/// que ele alcança, e os campos do grupo são conhecidos no ponto (UNI-REQ-0069, UNI-REQ-0146).
/// </summary>
public sealed class ConferenciaDaRepeticaoPorGrupoTests
{
    private readonly FaseCronograma _inscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true);
    private readonly FaseCronograma _habilitacao = FaseDeTeste.Criar(5, "HABILITACAO");

    private ConhecimentoDosFatos Conhecimento(params Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta.ColetaDoFormulario[] coletas) =>
        new(coletas, new CatalogoDosFatosCitados(new Dictionary<string, string>(), [], new Dictionary<string, IReadOnlyCollection<string>>()), [_inscricao, _habilitacao]);

    private static RepeticaoDaExigencia Repeticao(FormularioDeclarado formulario, FaseCronograma fase, string grupo) =>
        new(formulario.Codigo, fase.Id, grupo);

    [Fact(DisplayName = "O grupo coletado no formulário que entrega a exigência é aceito")]
    public void GrupoDoProprioFormulario_Aceita()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);

        ConferenciaDaRepeticaoPorGrupo.Conferir(
            Conhecimento(ColetaDeTeste.ColetaComGrupo(inscricao, "dependentes", "nome_do_dependente")),
            [Repeticao(inscricao, _inscricao, "dependentes")]).Should().BeEmpty();
    }

    [Fact(DisplayName = "O grupo coletado em formulário obrigatório já alcançado é aceito")]
    public void GrupoDeFormularioAlcancado_Aceita()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        FormularioDeclarado habilitacao = ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao);

        ConferenciaDaRepeticaoPorGrupo.Conferir(
            Conhecimento(ColetaDeTeste.ColetaComGrupo(inscricao, "dependentes", "nome_do_dependente"), ColetaDeTeste.Coleta(habilitacao, "carteira")),
            [Repeticao(habilitacao, _habilitacao, "dependentes")]).Should().BeEmpty();
    }

    [Fact(DisplayName = "O grupo que nenhum formulário coleta é recusado")]
    public void GrupoInexistente_Recusa()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);

        ConferenciaDaRepeticaoPorGrupo.Conferir(
            Conhecimento(ColetaDeTeste.Coleta(inscricao, "origem_escolar")),
            [Repeticao(inscricao, _inscricao, "dependentes")])
            .Should().ContainSingle().Which.Error.Code.Should().Be("RegraDeComprovacao.RepeticaoPorGrupoInexistente");
    }

    [Fact(DisplayName = "O grupo de formulário que o ponto não alcança é recusado")]
    public void GrupoDeFormularioNaoAlcancado_Recusa()
    {
        FormularioDeclarado habilitacao = ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao);
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);

        ConferenciaDaRepeticaoPorGrupo.Conferir(
            Conhecimento(ColetaDeTeste.ColetaComGrupo(habilitacao, "dependentes", "nome_do_dependente"), ColetaDeTeste.Coleta(inscricao, "origem_escolar")),
            [Repeticao(inscricao, _inscricao, "dependentes")])
            .Should().ContainSingle().Which.Error.Code.Should().Be("RegraDeComprovacao.RepeticaoPorGrupoNaoAlcancado");
    }

    [Fact(DisplayName = "A recusa aponta a exigência e o grupo pelo caminho")]
    public void Recusa_ApontaOCaminho()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);

        ConferenciaDaRepeticaoPorGrupo.Conferir(
            Conhecimento(ColetaDeTeste.Coleta(inscricao, "origem_escolar")),
            [Repeticao(inscricao, _inscricao, "dependentes")])
            .Should().ContainSingle().Which.Field.Should().Be("exigencias[0].repeticaoPorGrupo");
    }
}

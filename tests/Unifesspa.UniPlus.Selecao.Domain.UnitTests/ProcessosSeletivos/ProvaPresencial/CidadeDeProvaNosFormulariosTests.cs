namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ProvaPresencial;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// A cidade de prova nos formulários do processo (UNI-REQ-0407): com prova presencial, algum formulário
/// da inscrição a pede a todo candidato; sem prova presencial, nenhum formulário a pede.
/// </summary>
public sealed class CidadeDeProvaNosFormulariosTests
{
    private static readonly FaseCronograma Inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
    private static readonly FaseCronograma Habilitacao = FaseDeTeste.Criar(2, "HABILITACAO");

    private static DeclaracaoDaProvaPresencial Declaracao(bool temProvaPresencial) => DeclaracaoDaProvaPresencial.Declarar(
        temProvaPresencial, temProvaPresencial ? [new MunicipioDeAplicacaoInformado("1504208", "PA")] : [], 100).Value!;

    private static FormularioDeclarado Formulario(string codigo, FaseCronograma fase, bool obrigatorio) =>
        new(CodigoDoFormulario.Criar(codigo).Value!, fase.Id, obrigatorio);

    private static CampoDoFormulario Campo(FormularioDeclarado formulario, bool pedidoATodoCandidato = true) => new(
        formulario.Codigo, ObrigatorioSempre: pedidoATodoCandidato, SemPrecondicao: true, ForaDeGrupo: true, EmSecaoSempreVisivel: true);

    private static IReadOnlyList<Unifesspa.UniPlus.Kernel.Results.FieldError> Conferir(
        DeclaracaoDaProvaPresencial? declaracao, IReadOnlyList<FormularioDeclarado> formularios, params CampoDoFormulario[] campos) =>
        CidadeDeProvaNosFormularios.Conferir(declaracao, formularios, [Inscricao, Habilitacao], campos);

    [Fact(DisplayName = "Com prova presencial, o campo pedido a todo candidato no formulário da inscrição é aceito")]
    public void ComProva_CampoNoFormularioDaInscricao_Aceita()
    {
        FormularioDeclarado dados = Formulario("INSCRICAO", Inscricao, obrigatorio: true);

        Conferir(Declaracao(true), [dados], Campo(dados)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Com prova presencial e dois formulários da inscrição, o campo em um deles basta")]
    public void ComProva_CampoEmUmDosFormulariosDaInscricao_Aceita()
    {
        FormularioDeclarado dados = Formulario("DADOS", Inscricao, obrigatorio: true);
        FormularioDeclarado complementares = Formulario("DADOS_COMPLEMENTARES", Inscricao, obrigatorio: true);

        Conferir(Declaracao(true), [dados, complementares], Campo(complementares)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Com prova presencial e sem o campo, a falta é recusada nos formulários")]
    public void ComProva_SemCampo_Recusa()
    {
        FormularioDeclarado dados = Formulario("INSCRICAO", Inscricao, obrigatorio: true);

        Conferir(Declaracao(true), [dados]).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "formularios",
            Error = new { Code = ProvaPresencialErrorCodes.InscricaoSemCidadeDeProva },
        });
    }

    [Theory(DisplayName = "O campo fora do formulário da inscrição não é a cidade de prova de todo candidato")]
    [InlineData(false)]
    [InlineData(true)]
    public void ComProva_CampoForaDoFormularioDaInscricao_Recusa(bool formularioObrigatorio)
    {
        FormularioDeclarado dados = Formulario("INSCRICAO", Inscricao, obrigatorio: true);
        FormularioDeclarado outro = Formulario("OUTRO", formularioObrigatorio ? Habilitacao : Inscricao, formularioObrigatorio);

        Conferir(Declaracao(true), [dados, outro], Campo(outro)).Should().ContainSingle().Which.Error.Code
            .Should().Be(ProvaPresencialErrorCodes.InscricaoSemCidadeDeProva);
    }

    [Fact(DisplayName = "O campo condicional, em grupo ou em seção que se esconde não é pedido a todo candidato")]
    public void ComProva_CampoNaoPedidoATodoCandidato_Recusa()
    {
        FormularioDeclarado dados = Formulario("INSCRICAO", Inscricao, obrigatorio: true);
        CampoDoFormulario[] camposParciais =
        [
            Campo(dados) with { ObrigatorioSempre = false },
            Campo(dados) with { SemPrecondicao = false },
            Campo(dados) with { ForaDeGrupo = false },
            Campo(dados) with { EmSecaoSempreVisivel = false },
        ];

        foreach (CampoDoFormulario campo in camposParciais)
        {
            Conferir(Declaracao(true), [dados], campo).Should().ContainSingle().Which.Error.Code
                .Should().Be(ProvaPresencialErrorCodes.InscricaoSemCidadeDeProva);
        }
    }

    [Theory(DisplayName = "Sem prova presencial, ou por declarar, nenhum formulário pode pedir a cidade de prova")]
    [InlineData(false)]
    [InlineData(true)]
    public void SemProva_ComCampo_Recusa(bool porDeclarar)
    {
        FormularioDeclarado dados = Formulario("INSCRICAO", Inscricao, obrigatorio: true);

        Conferir(porDeclarar ? null : Declaracao(false), [dados], Campo(dados)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "formularios",
            Error = new { Code = ProvaPresencialErrorCodes.CidadeDeProvaSemProvaPresencial },
        });
    }

    [Fact(DisplayName = "Sem prova presencial e sem o campo, nada a recusar")]
    public void SemProva_SemCampo_Aceita()
    {
        FormularioDeclarado dados = Formulario("INSCRICAO", Inscricao, obrigatorio: true);

        Conferir(Declaracao(false), [dados]).Should().BeEmpty();
    }
}

namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// Quais respostas um ponto de uso alcança, nas situações da regra: o próprio formulário, o produtor
/// facultativo, o obrigatório de fase anterior, a mesma fase para a exigência e para o formulário
/// facultativo ou obrigatório, a fase posterior, a janela da isenção ordenada antes da inscrição e o
/// produtor sem fase.
/// </summary>
public sealed class AlcanceDasRespostasTests
{
    private static FormularioDeclarado Obrigatorio(string codigo, FaseCronograma? fase) =>
        new(CodigoDoFormulario.Criar(codigo).Value!, fase?.Id, Obrigatorio: true);

    private static FormularioDeclarado Facultativo(string codigo, FaseCronograma? fase) =>
        new(CodigoDoFormulario.Criar(codigo).Value!, fase?.Id, Obrigatorio: false);

    [Fact(DisplayName = "O formulário alcança as próprias respostas, mesmo facultativo")]
    public void ProprioFormulario_Alcanca()
    {
        FaseCronograma fase = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);
        FormularioDeclarado isencao = Facultativo("ISENCAO", fase);

        AlcanceDasRespostas.Alcanca(new PontoDeUso(fase, isencao), isencao, [fase]).Should().BeTrue();
    }

    [Fact(DisplayName = "A resposta de formulário facultativo não é fonte de ninguém")]
    public void ProdutorFacultativo_NaoAlcanca()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");
        FormularioDeclarado produtor = Facultativo("ISENCAO", isencao);
        FormularioDeclarado usuario = Obrigatorio("HABILITACAO", habilitacao);

        AlcanceDasRespostas.Alcanca(new PontoDeUso(habilitacao, usuario), produtor, [isencao, habilitacao]).Should().BeFalse();
    }

    [Fact(DisplayName = "O obrigatório de fase anterior é fonte do formulário de fase posterior")]
    public void ObrigatorioDeFaseAnterior_Alcanca()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FaseCronograma habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");
        FormularioDeclarado produtor = Obrigatorio("INSCRICAO", inscricao);
        FormularioDeclarado usuario = Obrigatorio("HABILITACAO", habilitacao);

        AlcanceDasRespostas.Alcanca(new PontoDeUso(habilitacao, usuario), produtor, [inscricao, habilitacao]).Should().BeTrue();
    }

    [Fact(DisplayName = "Na mesma fase, a exigência, que não é formulário, alcança o obrigatório")]
    public void MesmaFase_ExigenciaAlcanca()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FormularioDeclarado produtor = Obrigatorio("INSCRICAO", inscricao);

        AlcanceDasRespostas.Alcanca(new PontoDeUso(inscricao, Formulario: null), produtor, [inscricao]).Should().BeTrue();
    }

    [Fact(DisplayName = "Na mesma fase, o formulário facultativo alcança o obrigatório")]
    public void MesmaFase_FacultativoAlcancaObrigatorio()
    {
        FaseCronograma fase = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true, coletaSolicitacaoIsencao: true);
        FormularioDeclarado produtor = Obrigatorio("INSCRICAO", fase);
        FormularioDeclarado usuario = Facultativo("ISENCAO", fase);

        AlcanceDasRespostas.Alcanca(new PontoDeUso(fase, usuario), produtor, [fase]).Should().BeTrue();
    }

    [Fact(DisplayName = "Na mesma fase, dois obrigatórios não se alcançam")]
    public void MesmaFase_ObrigatorioComObrigatorio_NaoAlcanca()
    {
        FaseCronograma fase = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FormularioDeclarado produtor = Obrigatorio("INSCRICAO", fase);
        FormularioDeclarado usuario = Obrigatorio("DADOS_COMPLEMENTARES", fase);

        AlcanceDasRespostas.Alcanca(new PontoDeUso(fase, usuario), produtor, [fase]).Should().BeFalse();
    }

    [Fact(DisplayName = "O obrigatório de fase posterior não é fonte")]
    public void ProdutorEmFasePosterior_NaoAlcanca()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FaseCronograma habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");
        FormularioDeclarado produtor = Obrigatorio("HABILITACAO", habilitacao);
        FormularioDeclarado usuario = Facultativo("ISENCAO", inscricao);

        AlcanceDasRespostas.Alcanca(new PontoDeUso(inscricao, usuario), produtor, [inscricao, habilitacao]).Should().BeFalse();
    }

    [Fact(DisplayName = "A solicitação de isenção ordenada antes da inscrição é contemporânea dela e a alcança")]
    public void JanelaDeIsencaoOrdenadaAntes_AlcancaAInscricao()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma inscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true);
        FormularioDeclarado produtor = Obrigatorio("INSCRICAO", inscricao);
        FormularioDeclarado usuario = Facultativo("ISENCAO", isencao);

        AlcanceDasRespostas.Alcanca(new PontoDeUso(isencao, usuario), produtor, [isencao, inscricao]).Should().BeTrue();
    }

    [Theory(DisplayName = "A isenção antes da inscrição alcança a resposta enviada no formulário obrigatório da inscrição, com e sem a marca")]
    [InlineData(false)]
    [InlineData(true)]
    public void IsencaoAntesDaInscricao_AlcancaComESemAMarca(bool inscricaoMarcada)
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma inscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true, noMesmoMomentoDaAnterior: inscricaoMarcada);
        FormularioDeclarado produtor = Obrigatorio("INSCRICAO", inscricao);
        FormularioDeclarado usuario = Facultativo("ISENCAO", isencao);

        AlcanceDasRespostas.Alcanca(new PontoDeUso(isencao, usuario), produtor, [isencao, inscricao]).Should().BeTrue();
    }

    [Theory(DisplayName = "O reaproveitamento segue o envio e não a marca: o produtor de fase posterior não é alcançado, com ou sem a marca")]
    [InlineData(false)]
    [InlineData(true)]
    public void ProdutorDeFasePosterior_NaoAlcancaComESemAMarca(bool marcada)
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FaseCronograma habilitacao = FaseDeTeste.Criar(2, "HABILITACAO", noMesmoMomentoDaAnterior: marcada);
        FormularioDeclarado produtor = Obrigatorio("HABILITACAO", habilitacao);
        FormularioDeclarado usuario = Obrigatorio("INSCRICAO", inscricao);

        AlcanceDasRespostas.Alcanca(new PontoDeUso(inscricao, usuario), produtor, [inscricao, habilitacao]).Should().BeFalse();
    }

    [Fact(DisplayName = "A solicitação de isenção ordenada depois da inscrição vale a própria ordem, e a inscrição a antecede")]
    public void JanelaDeIsencaoOrdenadaDepois_ValeAPropriaOrdem()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FaseCronograma isencao = FaseDeTeste.Criar(2, "ISENCAO", coletaSolicitacaoIsencao: true);
        FormularioDeclarado daInscricao = Obrigatorio("INSCRICAO", inscricao);
        FormularioDeclarado daIsencao = Facultativo("ISENCAO", isencao);

        AlcanceDasRespostas.Alcanca(new PontoDeUso(isencao, daIsencao), daInscricao, [inscricao, isencao]).Should().BeTrue();
        AlcanceDasRespostas.Alcanca(new PontoDeUso(inscricao, daInscricao), Obrigatorio("ISENCAO", isencao), [inscricao, isencao]).Should().BeFalse();
    }

    [Fact(DisplayName = "Com mais de uma fase de inscrição, a isenção vale a de menor ordem, em qualquer ordem do cronograma")]
    public void MaisDeUmaFaseDeInscricao_ValeAMenorOrdem()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma primeiraInscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true);
        FaseCronograma segundaInscricao = FaseDeTeste.Criar(4, "INSCRICAO_COMPLEMENTAR", coletaInscricao: true);
        FormularioDeclarado produtor = Obrigatorio("INSCRICAO", primeiraInscricao);
        FormularioDeclarado usuario = Facultativo("ISENCAO", isencao);
        PontoDeUso ponto = new(isencao, usuario);

        AlcanceDasRespostas.Alcanca(ponto, produtor, [isencao, primeiraInscricao, segundaInscricao]).Should().BeTrue();
        AlcanceDasRespostas.Alcanca(ponto, produtor, [segundaInscricao, primeiraInscricao, isencao]).Should().BeTrue();
    }

    [Fact(DisplayName = "O produtor sem fase do cronograma não é fonte, a não ser dele mesmo")]
    public void ProdutorSemFase_NaoAlcanca()
    {
        FaseCronograma habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");
        FormularioDeclarado produtor = Obrigatorio("INSCRICAO", fase: null);
        FormularioDeclarado usuario = Obrigatorio("HABILITACAO", habilitacao);

        AlcanceDasRespostas.Alcanca(new PontoDeUso(habilitacao, usuario), produtor, [habilitacao]).Should().BeFalse();
        AlcanceDasRespostas.Alcanca(new PontoDeUso(habilitacao, produtor), produtor, [habilitacao]).Should().BeTrue();
    }

    [Fact(DisplayName = "Com dois formulários na mesma fase, o facultativo alcança o obrigatório e não o contrário")]
    public void DoisFormulariosNaMesmaFase_AlcanceEAssimetrico()
    {
        FaseCronograma fase = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FormularioDeclarado obrigatorio = Obrigatorio("INSCRICAO", fase);
        FormularioDeclarado facultativo = Facultativo("DADOS_COMPLEMENTARES", fase);

        AlcanceDasRespostas.Alcanca(new PontoDeUso(fase, facultativo), obrigatorio, [fase]).Should().BeTrue();
        AlcanceDasRespostas.Alcanca(new PontoDeUso(fase, obrigatorio), facultativo, [fase]).Should().BeFalse();
    }

    [Fact(DisplayName = "As fontes do ponto saem em ordem ordinal do código, só as que ele alcança")]
    public void Fontes_SoAsAlcancadasEmOrdemOrdinal()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FaseCronograma habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");
        FormularioDeclarado daInscricao = Obrigatorio("INSCRICAO", inscricao);
        FormularioDeclarado daIsencao = Facultativo("ISENCAO", inscricao);
        FormularioDeclarado daHabilitacao = Obrigatorio("HABILITACAO", habilitacao);

        IReadOnlyList<FormularioDeclarado> fontes = AlcanceDasRespostas.Fontes(
            new PontoDeUso(habilitacao, daHabilitacao), [daHabilitacao, daIsencao, daInscricao], [inscricao, habilitacao]);

        fontes.Should().Equal(daHabilitacao, daInscricao);
    }
}

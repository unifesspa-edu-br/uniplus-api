namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// Os formulários que o candidato responde para se inscrever: os obrigatórios ligados à fase que coleta
/// a inscrição. O processo pode ter nenhum, um ou mais de um; a regra não limita a quantidade.
/// </summary>
public sealed class FormulariosDaInscricaoTests
{
    private static FormularioDeclarado Formulario(string codigo, FaseCronograma? fase, bool obrigatorio) =>
        new(CodigoDoFormulario.Criar(codigo).Value!, fase?.Id, obrigatorio);

    [Fact(DisplayName = "Sem formulário obrigatório na fase de inscrição, a inscrição não tem formulário")]
    public void ZeroFormularios_ListaVazia()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);

        FormulariosDaInscricao.De([], [inscricao]).Should().BeEmpty();
    }

    [Fact(DisplayName = "O formulário obrigatório da fase de inscrição é o da inscrição")]
    public void UmFormulario_E_ODaInscricao()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FormularioDeclarado dados = Formulario("INSCRICAO", inscricao, obrigatorio: true);

        FormulariosDaInscricao.De([dados], [inscricao]).Should().Equal(dados);
    }

    [Fact(DisplayName = "Dois obrigatórios na fase de inscrição são os dois da inscrição, em ordem ordinal do código")]
    public void DoisFormularios_SaoOsDois_EmOrdemOrdinal()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FormularioDeclarado complementares = Formulario("DADOS_COMPLEMENTARES", inscricao, obrigatorio: true);
        FormularioDeclarado dados = Formulario("DADOS", inscricao, obrigatorio: true);

        FormulariosDaInscricao.De([dados, complementares], [inscricao]).Should().Equal(dados, complementares);
    }

    [Fact(DisplayName = "O formulário facultativo da fase de inscrição não é da inscrição")]
    public void Facultativo_NaoEDaInscricao()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FormularioDeclarado facultativo = Formulario("DADOS_COMPLEMENTARES", inscricao, obrigatorio: false);

        FormulariosDaInscricao.De([facultativo], [inscricao]).Should().BeEmpty();
    }

    [Fact(DisplayName = "O obrigatório de outra fase não é da inscrição")]
    public void ObrigatorioDeOutraFase_NaoEDaInscricao()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FaseCronograma habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");
        FormularioDeclarado daHabilitacao = Formulario("HABILITACAO", habilitacao, obrigatorio: true);

        FormulariosDaInscricao.De([daHabilitacao], [inscricao, habilitacao]).Should().BeEmpty();
    }

    [Fact(DisplayName = "O obrigatório sem fase não é da inscrição")]
    public void ObrigatorioSemFase_NaoEDaInscricao()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);

        FormulariosDaInscricao.De([Formulario("INSCRICAO", fase: null, obrigatorio: true)], [inscricao]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem fase que coleta a inscrição no cronograma, nenhum formulário é da inscrição")]
    public void SemFaseDeInscricao_ListaVazia()
    {
        FaseCronograma habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");

        FormulariosDaInscricao.De([Formulario("HABILITACAO", habilitacao, obrigatorio: true)], [habilitacao]).Should().BeEmpty();
    }
}

namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

public sealed class ConclusaoDaFaseTests
{
    private static CodigoDoFormulario Codigo(string valor) => CodigoDoFormulario.Criar(valor).Value!;

    [Fact(DisplayName = "A fase se cumpre ao enviar todos os formulários obrigatórios que se aplicam ao candidato")]
    public void Cumprida_TodosEnviados()
    {
        ConclusaoDaFase.Cumprida([Codigo("INSCRICAO"), Codigo("DADOS")], new HashSet<CodigoDoFormulario> { Codigo("INSCRICAO"), Codigo("DADOS"), Codigo("OUTRO") }).Should().BeTrue();
    }

    [Fact(DisplayName = "Faltar um obrigatório, ou nenhum envio, não cumpre a fase")]
    public void Cumprida_FaltaUm()
    {
        ConclusaoDaFase.Cumprida([Codigo("INSCRICAO"), Codigo("DADOS")], new HashSet<CodigoDoFormulario> { Codigo("INSCRICAO") }).Should().BeFalse();
        ConclusaoDaFase.Cumprida([Codigo("INSCRICAO")], new HashSet<CodigoDoFormulario>()).Should().BeFalse();
    }

    [Fact(DisplayName = "Sem obrigatório que se aplique ao candidato, a fase está cumprida por ele")]
    public void Cumprida_SemObrigatorioQueSeAplique()
    {
        ConclusaoDaFase.Cumprida([], new HashSet<CodigoDoFormulario>()).Should().BeTrue();
    }
}

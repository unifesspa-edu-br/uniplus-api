namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

/// <summary>
/// O que o arquivo traz é o que os relatórios considerados trazem em toda linha (UNI-REQ-0151): os declarados e os
/// que já importaram candidato. O dado básico que algum deles não traz inteiro é o que o formulário complementa.
/// </summary>
public sealed class RelatoriosConsideradosTests
{
    private static readonly HashSet<string> Rg = ["RG_NUMERO", "RG_ORGAO", "RG_UF", "RG_DATA"];
    private static readonly HashSet<string> ConjuntoBasico = [.. Rg, "CPF", "ENDERECO", "DESEJA_NOME_SOCIAL"];
    private static readonly PresencaNaImportacao[] Presencas = [new("RG_VEIO_NA_IMPORTACAO", Rg)];

    private static LeiauteDoRelatorio Completo(string codigo) =>
        LeiauteDeTeste.Criar(codigo, [.. Rg.Append("CPF").Append("ENDERECO").Select(LeiauteDeTeste.EmTodaLinha)]);

    private static RelatoriosConsiderados Considerando(LeiauteDoRelatorio[] declarados, params LeiauteDoRelatorio[] comCandidato) => new(declarados, comCandidato);

    [Fact(DisplayName = "O leiaute declarado e o já importado, mesmo retirado do documento, são considerados uma só vez")]
    public void Considerados_DeclaradosEImportados_SemRepeticao()
    {
        LeiauteDoRelatorio regular = Completo("CHAMADA_REGULAR");
        LeiauteDoRelatorio espera = Completo("LISTA_DE_ESPERA");

        Considerando([regular], regular, espera).Considerados.Should().Equal(regular, espera);
    }

    [Fact(DisplayName = "A cópia histórica de conteúdo diferente do mesmo código e versão não é descartada")]
    public void Considerados_CopiaHistoricaDeOutroConteudo_Permanece()
    {
        LeiauteDoRelatorio atual = LeiauteDeTeste.Criar("CHAMADA_REGULAR", LeiauteDeTeste.EmTodaLinha("ENDERECO"));
        LeiauteDoRelatorio historico = LeiauteDeTeste.Criar("CHAMADA_REGULAR", LeiauteDeTeste.EmTodaLinha("CPF")) with { Hash = "anterior" };

        Considerando([atual], historico).Considerados.Should().Equal(atual, historico);
    }

    [Fact(DisplayName = "O fato é trazido quando algum relatório considerado o traz em toda linha")]
    public void Traz_AlgumRelatorio()
    {
        RelatoriosConsiderados relatorios = Considerando(
            [LeiauteDeTeste.Criar("A", LeiauteDeTeste.EmTodaLinha("CPF")), LeiauteDeTeste.Criar("B", LeiauteDeTeste.Opcional("ENDERECO"))]);

        relatorios.Traz("CPF").Should().BeTrue();
        relatorios.Traz("ENDERECO").Should().BeFalse("a coluna opcional não conta como dado trazido");
        RelatoriosConsiderados.Nenhum.Traz("CPF").Should().BeFalse();
    }

    [Fact(DisplayName = "O grupo é trazido quando um mesmo relatório traz todos os fatos dele")]
    public void TrazTodosEmAlgum_ExigeUmSoRelatorio()
    {
        LeiauteDoRelatorio metade1 = LeiauteDeTeste.Criar("A", LeiauteDeTeste.EmTodaLinha("RG_NUMERO"), LeiauteDeTeste.EmTodaLinha("RG_ORGAO"));
        LeiauteDoRelatorio metade2 = LeiauteDeTeste.Criar("B", LeiauteDeTeste.EmTodaLinha("RG_UF"), LeiauteDeTeste.EmTodaLinha("RG_DATA"));

        Considerando([metade1, metade2]).TrazTodosEmAlgum(Rg).Should().BeFalse("o dado só em pedaços, entre relatórios, não vale");
        Considerando([metade1, metade2, Completo("C")]).TrazTodosEmAlgum(Rg).Should().BeTrue();
    }

    [Fact(DisplayName = "Com todos os relatórios trazendo o dado inteiro, só o dado que nenhum traz fica a complementar")]
    public void RelatoriosCompletos_SoOQueNenhumTraz()
    {
        IReadOnlySet<string> aComplementar = Considerando([Completo("A"), Completo("B")]).DadosBasicosAComplementar(ConjuntoBasico, Presencas);

        aComplementar.Should().BeEquivalentTo(["DESEJA_NOME_SOCIAL"]);
    }

    [Fact(DisplayName = "O RG que um relatório traz só com o número fica todo a complementar, pela granularidade do dado")]
    public void DadoIncompleto_FicaTodoAComplementar()
    {
        LeiauteDoRelatorio soNumero = LeiauteDeTeste.Criar("B", LeiauteDeTeste.EmTodaLinha("RG_NUMERO"), LeiauteDeTeste.EmTodaLinha("CPF"), LeiauteDeTeste.EmTodaLinha("ENDERECO"));

        Considerando([Completo("A"), soNumero]).DadosBasicosAComplementar(ConjuntoBasico, Presencas).Should()
            .BeEquivalentTo([.. Rg, "DESEJA_NOME_SOCIAL"]);
    }

    [Fact(DisplayName = "O dado que um relatório traz em coluna opcional fica a complementar")]
    public void ColunaOpcional_FicaAComplementar()
    {
        LeiauteDoRelatorio comOpcional = LeiauteDeTeste.Criar("A", [.. Rg.Append("CPF").Select(LeiauteDeTeste.EmTodaLinha), LeiauteDeTeste.Opcional("ENDERECO")]);

        Considerando([comOpcional]).DadosBasicosAComplementar(ConjuntoBasico, Presencas).Should().BeEquivalentTo(["ENDERECO", "DESEJA_NOME_SOCIAL"]);
    }

    [Fact(DisplayName = "O relatório já importado, mesmo retirado do documento, ainda conta para o dado a complementar")]
    public void RelatorioImportadoRetirado_AindaConta()
    {
        LeiauteDoRelatorio semEndereco = LeiauteDeTeste.Criar("PERFIL", [.. Rg.Append("CPF").Select(LeiauteDeTeste.EmTodaLinha)]);

        Considerando([Completo("A")], semEndereco).DadosBasicosAComplementar(ConjuntoBasico, Presencas).Should()
            .BeEquivalentTo(["ENDERECO", "DESEJA_NOME_SOCIAL"]);
    }

    [Fact(DisplayName = "Sem relatório considerado, o arquivo não traz dado algum e o conjunto básico todo fica a complementar")]
    public void SemRelatorio_TudoAComplementar() =>
        RelatoriosConsiderados.Nenhum.DadosBasicosAComplementar(ConjuntoBasico, Presencas).Should().BeEquivalentTo(ConjuntoBasico);
}

namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>O nome de cada tipo de ato citado: o da leitura, o da operação em curso e a recusa de tipo classificado sem nome.</summary>
public sealed class NomesDosTiposDeAtoTests
{
    private static readonly ClassificacaoDoTipoDeAto Resultado = new(EhResultado: true, CongelaConfiguracao: false);

    private static Dictionary<string, string> Nomes(params (string Codigo, string Nome)[] tipos) =>
        tipos.ToDictionary(static tipo => tipo.Codigo, static tipo => tipo.Nome, StringComparer.Ordinal);

    [Fact(DisplayName = "O nome do tipo com nome informado é devolvido")]
    public void NomeDe_TipoComNome_DevolveONome() =>
        new NomesDosTiposDeAto(Nomes(("RESULTADO_FINAL", "Resultado final"))).NomeDe("RESULTADO_FINAL").Should().Be("Resultado final");

    [Fact(DisplayName = "O tipo sem versão vigente nem cópia não tem nome: nunca devolve o código no lugar")]
    public void NomeDe_TipoSemNome_DevolveNulo() =>
        new NomesDosTiposDeAto(Nomes()).NomeDe("RESULTADO_FINAL").Should().BeNull();

    [Fact(DisplayName = "O tipo único sem leitura de nomes usa o nome do catálogo dos tipos únicos")]
    public void Nomes_SemLeituraDeNomes_UsaONomeDosTiposUnicos()
    {
        TiposDeAtoUnicosPorObjeto tipos = TiposDeAtoUnicosPorObjeto.ParaChecklist(Nomes(("EDITAL", "Edital")), []);

        tipos.Nomes.NomeDe("EDITAL").Should().Be("Edital");
    }

    [Fact(DisplayName = "O nome da cópia guardada na versão publicada prevalece sobre o nome do ato da operação de mesmo tipo")]
    public void Nomes_CopiaGuardadaEAtoDaOperacaoDoMesmoTipo_CopiaVence()
    {
        TiposDeAtoUnicosPorObjeto tipos = TiposDeAtoUnicosPorObjeto.ParaOperacao(
            Nomes(), [], "RESULTADO_FINAL", nomeDoAtoDaOperacao: "Resultado da classificação", nomes: Nomes(("RESULTADO_FINAL", "Resultado final")));

        tipos.Nomes.NomeDe("RESULTADO_FINAL").Should().Be("Resultado final");
    }

    [Fact(DisplayName = "O tipo do ato da operação sem nome na leitura usa o nome do ato")]
    public void Nomes_AtoDaOperacaoSemNomeNaLeitura_UsaONomeDoAto()
    {
        TiposDeAtoUnicosPorObjeto tipos = TiposDeAtoUnicosPorObjeto.ParaOperacao(
            Nomes(), [], "EDITAL_RETIFICACAO", nomeDoAtoDaOperacao: "Edital de retificação");

        tipos.Nomes.NomeDe("EDITAL_RETIFICACAO").Should().Be("Edital de retificação");
    }

    [Fact(DisplayName = "O tipo com classificação e sem nome é recusado na montagem")]
    public void ParaChecklist_ClassificacaoSemNome_Recusa()
    {
        Dictionary<string, ClassificacaoDoTipoDeAto> classificacoes = new(StringComparer.Ordinal) { ["RESULTADO_FINAL"] = Resultado };

        Action montar = () => TiposDeAtoUnicosPorObjeto.ParaChecklist(Nomes(), [], classificacoes);

        montar.Should().Throw<ArgumentException>();
    }
}

namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>A cópia do tipo de banca que a banca guarda: o que ela copia do cadastro e o que decide a partir da cópia.</summary>
public sealed class TipoDeBancaCopiadoTests
{
    private static TipoDeBancaCopiado Copia(
        Guid? origem = null,
        string nome = "Nome do cadastro",
        PapelDaBanca papel = PapelDaBanca.AvaliaOMerito,
        params TipoMotivoDeDecisao[] julgados) =>
        new(origem ?? Guid.CreateVersion7(), TipoDeBanca.AnaliseDocumental, nome, papel, exigeDuasOuMaisBancas: true, encerraRecursoNaPrimeiraInstancia: true, julgados);

    [Fact(DisplayName = "A cópia guarda o tipo, o nome, o papel, as duas marcas e os tipos de decisão julgados")]
    public void Criar_GuardaOQueCopiaDoCadastro()
    {
        Guid origem = Guid.CreateVersion7();

        TipoDeBancaCopiado copia = Copia(origem, "Nome do cadastro", PapelDaBanca.AvaliaOMerito, TipoMotivoDeDecisao.AnaliseDocumental);

        copia.OrigemId.Should().Be(origem);
        copia.Codigo.Should().Be(TipoDeBanca.AnaliseDocumental);
        copia.Nome.Should().Be("Nome do cadastro");
        copia.Papel.Should().Be(PapelDaBanca.AvaliaOMerito);
        copia.ExigeDuasOuMaisBancas.Should().BeTrue();
        copia.EncerraRecursoNaPrimeiraInstancia.Should().BeTrue();
        copia.TiposDeDecisaoJulgados.Should().Equal(TipoMotivoDeDecisao.AnaliseDocumental);
    }

    [Fact(DisplayName = "Os tipos de decisão julgados saem sem repetição e em ordem alfabética ordinal do código")]
    public void Criar_TiposDeDecisaoSemRepeticaoEmOrdemDoCodigo()
    {
        TipoDeBancaCopiado copia = Copia(
            julgados: [TipoMotivoDeDecisao.NotaDaEtapa, TipoMotivoDeDecisao.AnaliseDocumental, TipoMotivoDeDecisao.NotaDaEtapa, TipoMotivoDeDecisao.Classificacao]);

        copia.TiposDeDecisaoJulgados.Should().Equal(TipoMotivoDeDecisao.AnaliseDocumental, TipoMotivoDeDecisao.Classificacao, TipoMotivoDeDecisao.NotaDaEtapa);
    }

    [Fact(DisplayName = "O nome do cadastro é guardado sem os espaços das pontas")]
    public void Criar_NomeComEspacos_GuardaAparado() =>
        Copia(nome: "  Banca X  ").Nome.Should().Be("Banca X");

    [Fact(DisplayName = "A cópia sem o id do tipo no cadastro é erro de programação")]
    public void Criar_SemOrigem_Lanca() =>
        FluentActions.Invoking(() => Copia(Guid.Empty)).Should().Throw<ArgumentException>();

    [Theory(DisplayName = "A cópia sem nome é erro de programação")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemNome_Lanca(string nome) =>
        FluentActions.Invoking(() => Copia(nome: nome)).Should().Throw<ArgumentException>();

    [Theory(DisplayName = "Só o papel que julga o recurso em 1ª instância faz a cópia julgar recurso, e só o que avalia o mérito a faz avaliar")]
    [InlineData(PapelDaBanca.AvaliaOMerito, false, true)]
    [InlineData(PapelDaBanca.JulgaAPrimeiraInstancia, true, false)]
    [InlineData(PapelDaBanca.ImportaAsNotasDoInep, false, false)]
    [InlineData(PapelDaBanca.ImportaCandidatos, false, false)]
    public void Papel_DecideSeJulgaRecursoESeAvalia(PapelDaBanca papel, bool julgaRecurso, bool avalia)
    {
        TipoDeBancaCopiado copia = Copia(papel: papel);

        copia.JulgaRecurso.Should().Be(julgaRecurso);
        copia.AvaliaOMerito.Should().Be(avalia);
    }

    [Fact(DisplayName = "Duas cópias com o mesmo conteúdo são iguais, ainda que as listas sejam instâncias diferentes")]
    public void Igualdade_MesmoConteudo_Iguais()
    {
        Guid origem = Guid.CreateVersion7();

        Copia(origem, julgados: TipoMotivoDeDecisao.AnaliseDocumental).Should().Be(Copia(origem, julgados: TipoMotivoDeDecisao.AnaliseDocumental));
        Copia(origem, julgados: TipoMotivoDeDecisao.AnaliseDocumental).GetHashCode().Should().Be(Copia(origem, julgados: TipoMotivoDeDecisao.AnaliseDocumental).GetHashCode());
    }

    [Fact(DisplayName = "Cópias do mesmo tipo que diferem no nome, no papel ou nos tipos de decisão julgados são diferentes")]
    public void Igualdade_CopiasDoMesmoTipoComConteudoDiferente_Diferentes()
    {
        Guid origem = Guid.CreateVersion7();
        TipoDeBancaCopiado referencia = Copia(origem, "Nome", PapelDaBanca.AvaliaOMerito, TipoMotivoDeDecisao.AnaliseDocumental);

        Copia(origem, "Outro nome", PapelDaBanca.AvaliaOMerito, TipoMotivoDeDecisao.AnaliseDocumental).Should().NotBe(referencia);
        Copia(origem, "Nome", PapelDaBanca.JulgaAPrimeiraInstancia, TipoMotivoDeDecisao.AnaliseDocumental).Should().NotBe(referencia);
        Copia(origem, "Nome", PapelDaBanca.AvaliaOMerito, TipoMotivoDeDecisao.Classificacao).Should().NotBe(referencia);
        Copia(origem, "Nome", PapelDaBanca.AvaliaOMerito).Should().NotBe(referencia);
    }
}

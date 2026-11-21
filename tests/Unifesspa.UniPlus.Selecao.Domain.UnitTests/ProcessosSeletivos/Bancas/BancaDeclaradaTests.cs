namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>A banca declarada guarda a posição na lista e a posição entre as do mesmo tipo, e decide pela cópia do tipo dela.</summary>
public sealed class BancaDeclaradaTests
{
    [Fact(DisplayName = "A posição entre as do mesmo tipo conta dentro de cada tipo, na ordem em que as bancas chegam")]
    public void De_NumeraDentroDeCadaTipo()
    {
        IReadOnlyList<BancaDeclarada> declaradas = BancaDeclarada.De(
        [
            (TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Entrevista), null),
            (TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental), null),
            (TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Entrevista), null),
        ]);

        declaradas.Select(static b => (b.Posicao, b.PosicaoNoTipo)).Should().Equal((0, 1), (1, 1), (2, 2));
        declaradas.Select(static b => b.Citacao).Should().Equal("Banca de entrevista 1", "Banca de análise documental 1", "Banca de entrevista 2");
    }

    [Fact(DisplayName = "A posição entre as do mesmo tipo conta pelo código do tipo, ainda que as cópias sejam diferentes")]
    public void De_CopiasDiferentesDoMesmoTipo_ContamNoMesmoTipo()
    {
        IReadOnlyList<BancaDeclarada> declaradas = BancaDeclarada.De(
        [
            (TiposDeBancaCopiadosDeTeste.Com(TipoDeBanca.AnaliseDeRecursos, PapelDaBanca.JulgaAPrimeiraInstancia), null),
            (TiposDeBancaCopiadosDeTeste.Com(TipoDeBanca.AnaliseDeRecursos, PapelDaBanca.AvaliaOMerito), null),
        ]);

        declaradas.Select(static b => b.PosicaoNoTipo).Should().Equal(1, 2);
        declaradas.Select(static b => b.Tipo.JulgaRecurso).Should().Equal(true, false);
        declaradas.Select(static b => b.Tipo.AvaliaOMerito).Should().Equal(false, true);
    }

    [Fact(DisplayName = "A banca julga recurso sem votação só quando o papel da cópia julga recurso e a regra decide por ato individual sem votação")]
    public void JulgaRecursoSemVotacao_PapelEModo()
    {
        RegraDeDeliberacao semVotacao = RegraDeDeliberacao.Criar(
            new DeclaracaoDaDeliberacao(ModoDeDeliberacao.AtoIndividual, 1, 0, null, null, null, null, null)).Value!;

        new BancaDeclarada(0, 1, TiposDeBancaCopiadosDeTeste.DeRecursos(), semVotacao).JulgaRecursoSemVotacao.Should().BeTrue();
        new BancaDeclarada(0, 1, TiposDeBancaCopiadosDeTeste.Avaliadora(), semVotacao).JulgaRecursoSemVotacao.Should().BeFalse();
        new BancaDeclarada(0, 1, TiposDeBancaCopiadosDeTeste.DeRecursos(), BancasDeTeste.Regra()).JulgaRecursoSemVotacao.Should().BeFalse();
        new BancaDeclarada(0, 1, TiposDeBancaCopiadosDeTeste.DeRecursos(), null).JulgaRecursoSemVotacao.Should().BeFalse();
    }
}

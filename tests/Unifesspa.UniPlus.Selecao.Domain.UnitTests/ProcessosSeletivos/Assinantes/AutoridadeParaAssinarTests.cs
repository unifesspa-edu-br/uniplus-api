namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Assinantes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Assinantes;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Assinantes.UnidadesDosAssinantesParaTestes;

/// <summary>Quem pode assinar o documento de uma unidade: a própria e as que estão acima dela.</summary>
public sealed class AutoridadeParaAssinarTests
{
    public static TheoryData<Guid, Guid> AssinaturasPermitidas() => new()
    {
        { Cga, Cga },
        { Cga, CoordenadoriaDeEnsino },
        { Cga, CoordenadoriaDeRegistro },
        { Cga, SecaoDeEditais },
        { Cga, SubsecaoDeProvas },
        { CoordenadoriaDeEnsino, CoordenadoriaDeEnsino },
        { CoordenadoriaDeEnsino, SecaoDeEditais },
        { CoordenadoriaDeEnsino, SubsecaoDeProvas },
        { SecaoDeEditais, SubsecaoDeProvas },
    };

    public static TheoryData<Guid, Guid> AssinaturasNegadas() => new()
    {
        { CoordenadoriaDeEnsino, Cga },
        { SecaoDeEditais, CoordenadoriaDeEnsino },
        { SubsecaoDeProvas, SecaoDeEditais },
        { CoordenadoriaDeRegistro, CoordenadoriaDeEnsino },
        { CoordenadoriaDeRegistro, SubsecaoDeProvas },
        { Ceps, Cga },
        { Cga, Ceps },
    };

    [Theory(DisplayName = "A pessoa assina pela própria unidade e pelas que estão abaixo dela")]
    [MemberData(nameof(AssinaturasPermitidas))]
    public void PessoaAssina_PelaPropriaUnidadeEPelasAbaixo(Guid unidadeDaPessoa, Guid unidadeDoDocumento) =>
        AutoridadeParaAssinar.Avaliar(unidadeDaPessoa, unidadeDoDocumento, Estrutura()).Should().Be(ResultadoDaAutoridade.Autorizada);

    [Theory(DisplayName = "A pessoa não assina pela unidade acima, pela de mesmo nível nem pela de outra árvore")]
    [MemberData(nameof(AssinaturasNegadas))]
    public void PessoaNaoAssina_PelaUnidadeAcimaDoMesmoNivelOuDeOutraArvore(Guid unidadeDaPessoa, Guid unidadeDoDocumento) =>
        AutoridadeParaAssinar.Avaliar(unidadeDaPessoa, unidadeDoDocumento, Estrutura()).Should().Be(ResultadoDaAutoridade.SemAutoridade);

    [Fact(DisplayName = "Unidade inativa no caminho entre a do documento e a da pessoa nega a autoridade")]
    public void UnidadeInativaNaSubida_NegaAAutoridade() =>
        AutoridadeParaAssinar.Avaliar(Cga, SubsecaoDeProvas, EstruturaSem(SecaoDeEditais))
            .Should().Be(ResultadoDaAutoridade.SemAutoridade);

    [Fact(DisplayName = "Unidade do documento inativa nega a autoridade")]
    public void UnidadeDoDocumentoInativa_NegaAAutoridade() =>
        AutoridadeParaAssinar.Avaliar(Cga, SecaoDeEditais, EstruturaSem(SecaoDeEditais))
            .Should().Be(ResultadoDaAutoridade.SemAutoridade);

    [Fact(DisplayName = "Unidade da pessoa inativa nega a autoridade")]
    public void UnidadeDaPessoaInativa_NegaAAutoridade() =>
        AutoridadeParaAssinar.Avaliar(CoordenadoriaDeEnsino, SecaoDeEditais, EstruturaSem(CoordenadoriaDeEnsino))
            .Should().Be(ResultadoDaAutoridade.SemAutoridade);

    [Fact(DisplayName = "Unidade desconhecida da estrutura nega a autoridade")]
    public void UnidadeDesconhecida_NegaAAutoridade() =>
        AutoridadeParaAssinar.Avaliar(Cga, Guid.NewGuid(), Estrutura())
            .Should().Be(ResultadoDaAutoridade.SemAutoridade);

    [Fact(DisplayName = "Hierarquia que forma ciclo falha de forma explícita, sem negar em silêncio")]
    public void HierarquiaComCiclo_FalhaDeFormaExplicita()
    {
        Guid primeira = Guid.NewGuid();
        Guid segunda = Guid.NewGuid();
        Guid outra = Guid.NewGuid();
        EstruturaDasUnidadesAtivas estrutura = new([
            new(primeira, segunda),
            new(segunda, primeira),
            new(outra, null),
        ]);

        AutoridadeParaAssinar.Avaliar(outra, primeira, estrutura).Should().Be(ResultadoDaAutoridade.HierarquiaComCiclo);
    }

    [Fact(DisplayName = "Ciclo na hierarquia nunca autoriza, nem para a pessoa que está dentro dele")]
    public void PessoaDentroDoCiclo_NaoEAutorizada()
    {
        Guid primeira = Guid.NewGuid();
        Guid segunda = Guid.NewGuid();
        EstruturaDasUnidadesAtivas estrutura = new([new(primeira, segunda), new(segunda, primeira)]);

        AutoridadeParaAssinar.Avaliar(segunda, primeira, estrutura).Should().Be(ResultadoDaAutoridade.HierarquiaComCiclo);
    }
}

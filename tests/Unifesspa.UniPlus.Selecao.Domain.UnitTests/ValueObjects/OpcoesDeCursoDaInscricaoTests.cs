namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class OpcoesDeCursoDaInscricaoTests
{
    private static readonly Guid Primeira = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Segunda = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static RespostasAdmitidas Admitidas => new(new Dictionary<string, DefinicaoDaResposta>(),
        new HashSet<string>(), new Dictionary<string, IReadOnlySet<string>>(), new HashSet<Guid> { Primeira, Segunda },
        new Dictionary<string, TermoAdmitido>(), NumeroDeOpcoesDeCurso: 2);

    [Fact]
    public void Escolher_MesmasOpcoes_PossuiIgualdadePorValor()
    {
        OpcoesDeCursoDaInscricao opcoes = OpcoesDeCursoDaInscricao.Escolher(Primeira, Segunda, Admitidas).Value!;
        OpcoesDeCursoDaInscricao iguais = OpcoesDeCursoDaInscricao.Escolher(Primeira, Segunda, Admitidas).Value!;
        opcoes.Should().Be(iguais);
        opcoes.GetHashCode().Should().Be(iguais.GetHashCode());
        typeof(OpcoesDeCursoDaInscricao).GetConstructors().Should().BeEmpty();
        typeof(OpcoesDeCursoDaInscricao).GetProperties().Should().OnlyContain(propriedade => !propriedade.CanWrite);
    }

    [Fact]
    public void Reidratar_ValoresPersistidos_PreservaAmbasAsOpcoes()
    {
        OpcoesDeCursoDaInscricao opcoes = OpcoesDeCursoDaInscricao.Reidratar(Primeira, Primeira);
        opcoes.Primeira.Should().Be(Primeira);
        opcoes.Segunda.Should().Be(Primeira);
    }

    [Fact]
    public void Escolher_OpcoesIguais_Recusa()
    {
        Result<OpcoesDeCursoDaInscricao> resultado = OpcoesDeCursoDaInscricao.Escolher(Primeira, Primeira, Admitidas);
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.SegundaOpcaoIgualAPrimeira);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Recusas_SegundaOpcaoNaoAdmitida_AcumulaNaOrdem(bool aoEnviar)
    {
        Guid foraDoProcesso = Guid.CreateVersion7();
        OpcoesDeCursoDaInscricao opcoes = OpcoesDeCursoDaInscricao.Reidratar(foraDoProcesso, foraDoProcesso);
        RespostasAdmitidas admitidas = Admitidas with { NumeroDeOpcoesDeCurso = 1 };
        IEnumerable<FieldError> recusas = aoEnviar ? opcoes.RecusasAoEnviar(admitidas) : opcoes.RecusasAoSalvar(admitidas);
        recusas.Select(static recusa => (recusa.Field, recusa.Error.Code, recusa.Error.Message)).Should().Equal(
            ("ofertaCursoId", InscricaoErrorCodes.OfertaForaDoProcesso, "A oferta de curso escolhida não é deste processo seletivo."),
            ("ofertaCursoSegundaOpcaoId", InscricaoErrorCodes.SegundaOpcaoNaoAdmitida, "Este processo seletivo admite uma só opção de curso. Retire a 2ª opção."));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Recusas_OpcoesInvalidas_AcumulaNaOrdem(bool aoEnviar)
    {
        Guid foraDoProcesso = Guid.CreateVersion7();
        OpcoesDeCursoDaInscricao opcoes = OpcoesDeCursoDaInscricao.Reidratar(foraDoProcesso, foraDoProcesso);
        IEnumerable<FieldError> recusas = aoEnviar ? opcoes.RecusasAoEnviar(Admitidas) : opcoes.RecusasAoSalvar(Admitidas);
        recusas.Select(static recusa => (recusa.Field, recusa.Error.Code, recusa.Error.Message)).Should().Equal(
            ("ofertaCursoId", InscricaoErrorCodes.OfertaForaDoProcesso, "A oferta de curso escolhida não é deste processo seletivo."),
            ("ofertaCursoSegundaOpcaoId", InscricaoErrorCodes.SegundaOpcaoIgualAPrimeira, "A 2ª opção de curso tem de ser diferente da 1ª."),
            ("ofertaCursoSegundaOpcaoId", InscricaoErrorCodes.OfertaForaDoProcesso, "A oferta de curso escolhida não é deste processo seletivo."));
    }

    [Fact]
    public void RecusasAoEnviar_SemPrimeiraComSegunda_RecusaAmbasNaOrdem()
    {
        OpcoesDeCursoDaInscricao opcoes = OpcoesDeCursoDaInscricao.Escolher(null, Segunda, Admitidas).Value!;
        opcoes.RecusasAoSalvar(Admitidas).Should().BeEmpty();
        opcoes.RecusasAoEnviar(Admitidas).Select(static r => r.Error.Code)
            .Should().Equal(InscricaoErrorCodes.OfertaObrigatoria, InscricaoErrorCodes.SegundaOpcaoSemPrimeira);
    }

    [Fact]
    public void RecusasAoEnviar_SemOfertasESemOpcao_NaoRecusa()
    {
        RespostasAdmitidas admitidas = Admitidas with { OfertasDeCurso = new HashSet<Guid>() };
        OpcoesDeCursoDaInscricao.Escolher(null, null, admitidas).Value!.RecusasAoEnviar(admitidas).Should().BeEmpty();
    }

    [Fact]
    public void SegundaAdmissivel_SemPrimeiraOuComSegundaInvalida_RetornaFalso()
    {
        OpcoesDeCursoDaInscricao.SegundaAdmissivel(null, Segunda, Admitidas).Should().BeFalse();
        OpcoesDeCursoDaInscricao.SegundaAdmissivel(Primeira, Primeira, Admitidas).Should().BeFalse();
        OpcoesDeCursoDaInscricao.SegundaAdmissivel(Primeira, Segunda, Admitidas).Should().BeTrue();
    }
}

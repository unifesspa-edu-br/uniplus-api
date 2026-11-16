namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Publicacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

public sealed class ProntidaoParaCongelarTests
{
    private static readonly string[] OrdemDosPortoes =
    [
        "IdentificadorLegivel", "Localidade", "AlgoritmoDeContagem", "CalendarioVigente", "Conformidade", "Cronograma", "Bancas",
        "TipoDeAtoUnico", "ResultadoSemPapel", "TipoQueCongelaDeclaradoComoResultado", "Cascata", "PreCanonicalizacao",
    ];

    [Fact(DisplayName = "Sem pendência, os doze portões são avaliados uma vez, na ordem")]
    public void SemPendencia_AvaliaOsDozePortoesNaOrdem()
    {
        List<string> avaliados = [];

        DomainError? pendencia = ProntidaoParaCongelar.PrimeiraPendencia(Portoes(avaliados, recusaEm: null));

        pendencia.Should().BeNull();
        avaliados.Should().Equal(OrdemDosPortoes);
    }

    [Theory(DisplayName = "A primeira pendência encerra a avaliação e os portões seguintes não rodam")]
    [InlineData("IdentificadorLegivel")]
    [InlineData("Conformidade")]
    [InlineData("PreCanonicalizacao")]
    public void PrimeiraPendencia_ParaNoPortaoQueRecusa(string recusante)
    {
        List<string> avaliados = [];

        DomainError? pendencia = ProntidaoParaCongelar.PrimeiraPendencia(Portoes(avaliados, recusaEm: recusante));

        pendencia!.Code.Should().Be(recusante);
        avaliados.Should().Equal(OrdemDosPortoes.Take(Array.IndexOf(OrdemDosPortoes, recusante) + 1));
    }

    private static ProntidaoParaCongelar.Portoes Portoes(List<string> avaliados, string? recusaEm)
    {
        Func<DomainError?> Portao(string nome) => () =>
        {
            avaliados.Add(nome);
            return nome == recusaEm ? new DomainError(nome, nome) : null;
        };

        return new(
            new(Portao("IdentificadorLegivel"), Portao("Localidade"), Portao("AlgoritmoDeContagem"), Portao("CalendarioVigente")),
            new(Portao("Conformidade"), Portao("Cronograma"), Portao("Bancas")),
            new(Portao("TipoDeAtoUnico"), Portao("ResultadoSemPapel"), Portao("TipoQueCongelaDeclaradoComoResultado")),
            new(Portao("Cascata"), Portao("PreCanonicalizacao")));
    }
}

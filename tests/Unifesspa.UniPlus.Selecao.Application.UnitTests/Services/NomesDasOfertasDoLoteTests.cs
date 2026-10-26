namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Xunit;

/// <summary>Os cursos do inscrito no lote, na ordem em que ele os escolheu.</summary>
public sealed class NomesDasOfertasDoLoteTests
{
    private static readonly Guid Primeira = Guid.CreateVersion7();
    private static readonly Guid Segunda = Guid.CreateVersion7();

    private static InscritoDoLote Inscrito(Guid? primeira, Guid? segunda) =>
        new("UNI-0001", "Pessoa", "***.111.222-**", Guid.CreateVersion7(), primeira, segunda);

    [Fact(DisplayName = "sem 2ª opção traz só a 1ª")]
    public void Opcoes_SemSegunda_TrazAPrimeira()
    {
        List<OpcaoDeCursoDoLoteDto> opcoes = NomesDasOfertasDoLote.Opcoes(
            Inscrito(Primeira, null), new Dictionary<Guid, string> { [Primeira] = "Curso A" });

        opcoes.Should().Equal(new OpcaoDeCursoDoLoteDto(1, "Curso A"));
    }

    [Fact(DisplayName = "1ª opção fora da versão não descarta a 2ª, que mantém a ordem 2")]
    public void Opcoes_PrimeiraNaoAchada_MantemASegundaComASuaOrdem()
    {
        List<OpcaoDeCursoDoLoteDto> opcoes = NomesDasOfertasDoLote.Opcoes(
            Inscrito(Primeira, Segunda), new Dictionary<Guid, string> { [Segunda] = "Curso B" });

        opcoes.Should().Equal(new OpcaoDeCursoDoLoteDto(2, "Curso B"));
    }
}

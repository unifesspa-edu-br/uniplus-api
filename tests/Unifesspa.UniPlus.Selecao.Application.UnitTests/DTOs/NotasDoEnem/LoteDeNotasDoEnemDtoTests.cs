namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.DTOs.NotasDoEnem;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.DTOs.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;

using Xunit;

/// <summary>Como o lote lista quem faltou: o total, as áreas de cada ausente e a ordem da lista.</summary>
public sealed class LoteDeNotasDoEnemDtoTests
{
    private static LoteDeNotasDoEnem LoteVazio() =>
        LoteDeNotasDoEnem.Registrar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "2025", "INEP_ENEM_RETORNO", "v1", new string('a', 64),
            new string('b', 64), "sub-admin", DateTimeOffset.UtcNow, 0, Guid.CreateVersion7(),
            new ResultadoDoCruzamento([], [], [], []), [], "Operador de Teste");

    [Fact(DisplayName = "ausente parcial traz só a área em que faltou e ausente total traz as cinco, em ordem de inscrição")]
    public void De_Ausentes_TrazAsAreasDeCadaUm()
    {
        Guid parcial = Guid.CreateVersion7();
        Guid total = Guid.CreateVersion7();
        DetalheDoLoteDeNotas detalhe = new(
            new Dictionary<Guid, InscritoDoLote>
            {
                [parcial] = new("UNI-0002", "Pessoa Parcial", "***.111.222-**", Guid.CreateVersion7(), null, null),
                [total] = new("UNI-0001", "Pessoa Total", "***.333.444-**", Guid.CreateVersion7(), null, null),
            },
            new Dictionary<Guid, IReadOnlyList<string>>
            {
                [parcial] = [AreaDoEnem.Matematica],
                [total] = AreaDoEnem.Todas,
            },
            new Dictionary<Guid, IReadOnlyList<OpcaoDeCursoDoLoteDto>>());

        LoteDeNotasDoEnemDto dto = LoteDeNotasDoEnemDto.De(LoteVazio(), vigente: true, detalhe, totalDeInscritosAusentes: 2);

        dto.TotalDeInscritosAusentes.Should().Be(2);
        dto.ImportadoPorNome.Should().Be("Operador de Teste");
        dto.Ausentes!.Select(a => a.NumeroDeInscricao).Should().Equal("UNI-0001", "UNI-0002");
        dto.Ausentes![0].Areas.Should().Equal(AreaDoEnem.Todas);
        dto.Ausentes![1].Areas.Should().Equal(AreaDoEnem.Matematica);
        dto.Ausentes![1].NomeDoCandidato.Should().Be("Pessoa Parcial");
        dto.Ausentes![1].CpfMascarado.Should().Be("***.111.222-**");
    }

    [Fact(DisplayName = "ausente sem número de inscrição lido vai para o fim da lista")]
    public void De_AusenteSemNumero_VaiParaOFim()
    {
        Guid semNumero = Guid.CreateVersion7();
        Guid comNumero = Guid.CreateVersion7();
        DetalheDoLoteDeNotas detalhe = new(
            new Dictionary<Guid, InscritoDoLote> { [comNumero] = new("UNI-0001", "Pessoa", "***.333.444-**", Guid.CreateVersion7(), null, null) },
            new Dictionary<Guid, IReadOnlyList<string>> { [semNumero] = [AreaDoEnem.Redacao], [comNumero] = [AreaDoEnem.Redacao] },
            new Dictionary<Guid, IReadOnlyList<OpcaoDeCursoDoLoteDto>>());

        LoteDeNotasDoEnemDto dto = LoteDeNotasDoEnemDto.De(LoteVazio(), vigente: true, detalhe, totalDeInscritosAusentes: 2);

        dto.Ausentes!.Select(a => a.InscricaoId).Should().Equal(comNumero, semNumero);
    }
}

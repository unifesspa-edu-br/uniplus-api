namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.MontagemDoLocalDeOferta;

public sealed class LocalOfertaProcedenciaDaCidadeTests
{
    private static readonly DateTimeOffset DepoisDaCriacao = Agora.AddDays(30);

    public static TheoryData<string, Func<DeclaracaoDoLocalDeOferta, DeclaracaoDoLocalDeOferta>> MudancasDaReferenciaDaCidade => new()
    {
        { "código IBGE", d => d with { CidadeCodigoIbge = "1501402", CidadeNome = "Marabá", CidadeUf = "PA" } },
        { "nome", d => d with { CidadeNome = "Marabá do Pará" } },
        { "UF junto com o código IBGE", d => d with { CidadeCodigoIbge = "1721000", CidadeUf = "TO" } },
    };

    public static TheoryData<string, Func<DeclaracaoDoLocalDeOferta, DeclaracaoDoLocalDeOferta>> MudancasForaDaReferenciaDaCidade => new()
    {
        { "tipo", d => d with { Tipo = "CAMPUS_SEDE" } },
        { "campus responsável", d => d with { CampusResponsavelId = Guid.CreateVersion7() } },
        { "código e-MEC", d => d with { CodigoEmec = "98765" } },
        { "endereço", d => d with { Endereco = Endereco() } },
        { "caixa da UF e espaço nas pontas", d => d with { CidadeCodigoIbge = " 1504208 ", CidadeNome = " Marabá ", CidadeUf = "pa" } },
    };

    [Theory(DisplayName = "Atualizar renova a procedência da cidade quando a referência muda")]
    [MemberData(nameof(MudancasDaReferenciaDaCidade))]
    public void Atualizar_ReferenciaDaCidadeMudou_RenovaAProcedencia(
        string campoQueMudou, Func<DeclaracaoDoLocalDeOferta, DeclaracaoDoLocalDeOferta> ajuste)
    {
        LocalOferta local = Existente();

        local.Atualizar(Declaracao(ajuste), DepoisDaCriacao).IsSuccess.Should().BeTrue($"a mudança de {campoQueMudou} é válida");

        local.CidadeOrigem.Should().Be(ReferenciaCidadeGeo.OrigemGeoApi);
        local.CidadeDisplayAtualizadoEm.Should().Be(DepoisDaCriacao);
    }

    [Theory(DisplayName = "Atualizar preserva a procedência da cidade quando só outro campo muda")]
    [MemberData(nameof(MudancasForaDaReferenciaDaCidade))]
    public void Atualizar_ReferenciaDaCidadeIgual_PreservaAProcedencia(
        string campoQueMudou, Func<DeclaracaoDoLocalDeOferta, DeclaracaoDoLocalDeOferta> ajuste)
    {
        LocalOferta local = Existente();

        local.Atualizar(Declaracao(ajuste), DepoisDaCriacao).IsSuccess.Should().BeTrue($"a mudança de {campoQueMudou} é válida");

        local.CidadeOrigem.Should().Be(ReferenciaCidadeGeo.OrigemGeoApi);
        local.CidadeDisplayAtualizadoEm.Should().Be(Agora);
    }

    [Fact(DisplayName = "Atualizar recusada não renova a procedência da cidade")]
    public void Atualizar_DeclaracaoInvalida_NaoRenovaAProcedencia()
    {
        LocalOferta local = Existente();

        local.Atualizar(Declaracao(d => d with { CidadeNome = "Marabá do Pará", Tipo = "INEXISTENTE" }), DepoisDaCriacao)
            .IsFailure.Should().BeTrue();

        local.CidadeDisplayAtualizadoEm.Should().Be(Agora);
    }
}

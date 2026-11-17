namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// A expressão de vigência é composta por invocação de uma janela única, e só o tradutor do EF diz
/// se ela vira SQL: o filtro por <c>Where</c> traduz a janela inteira, com o dia como parâmetro, em
/// vez de avaliá-la em memória.
/// </summary>
public sealed class TipoAtoPublicadoVigenciaConsultaTests
{
    [Fact(DisplayName = "A vigência de um dia traduz para SQL com início, fim e o dia como parâmetro")]
    public void VigenteEm_ConsultaTraduzida_TrazAJanelaEODiaComoParametro()
    {
        using MonolitoDbContext ctx = AjustesDoHostDeTeste.ContextoSemBanco();

        string sql = ctx.TiposAtoPublicado
            .Where(TipoAtoPublicado.VigenteEm(new DateOnly(2026, 6, 15)))
            .ToQueryString();

        sql.Should().Contain("-- @dia=", "o dia da consulta chega como parâmetro");
        sql.Should().Contain("t.vigencia_inicio <= @dia AND (t.vigencia_fim IS NULL OR t.vigencia_fim > @dia)");
    }

    [Fact(DisplayName = "Dias diferentes reaproveitam o mesmo texto de SQL e mudam só o parâmetro")]
    public void VigenteEm_DoisDias_MudamSoOParametro()
    {
        using MonolitoDbContext ctx = AjustesDoHostDeTeste.ContextoSemBanco();

        string primeira = ctx.TiposAtoPublicado.Where(TipoAtoPublicado.VigenteEm(new DateOnly(2026, 6, 15))).ToQueryString();
        string segunda = ctx.TiposAtoPublicado.Where(TipoAtoPublicado.VigenteEm(new DateOnly(2026, 6, 16))).ToQueryString();

        primeira.Should().NotBe(segunda, "o parâmetro carrega o dia de cada consulta");
        SemOParametro(primeira).Should().Be(SemOParametro(segunda));
    }

    private static string SemOParametro(string sql) => sql[(sql.IndexOf('\n', StringComparison.Ordinal) + 1)..];
}

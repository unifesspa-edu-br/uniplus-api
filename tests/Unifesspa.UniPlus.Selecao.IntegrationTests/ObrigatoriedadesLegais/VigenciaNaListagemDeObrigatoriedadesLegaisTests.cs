namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// A listagem de vigentes compara as datas de vigência com o dia que quem consulta informa: o início é inclusivo e o fim é
/// exclusivo. Fica em classe própria porque as regras gravadas aqui entrariam na listagem sem filtro de tipo da outra.
/// </summary>
public sealed class VigenciaNaListagemDeObrigatoriedadesLegaisTests(ObrigatoriedadeLegalDbFixture fixture)
    : IClassFixture<ObrigatoriedadeLegalDbFixture>
{
    [Theory(DisplayName = "A listagem vigente usa o dia informado: o fim é exclusivo e o início é inclusivo")]
    [InlineData(-30, 1, "FIM_AMANHA", true)]
    [InlineData(-30, 0, "FIM_HOJE", false)]
    [InlineData(1, null, "INICIO_AMANHA", false)]
    [InlineData(0, null, "INICIO_HOJE", true)]
    public async Task ListarPaginado_ComDiaDeVigencia_AplicaFimExclusivoEInicioInclusivo(
        int inicioEmDias, int? fimEmDias, string codigo, bool apareceHoje)
    {
        DateOnly hoje = new(2026, 6, 15);
        await using UniPlusDbContext contexto = fixture.CreateDbContext("admin");
        contexto.ObrigatoriedadesLegais.Add(Regra(codigo, codigo, hoje.AddDays(inicioEmDias), fimEmDias is { } fim ? hoje.AddDays(fim) : null));
        await contexto.SaveChangesAsync();
        ObrigatoriedadeLegalRepository repositorio = new(contexto);

        (IReadOnlyList<ObrigatoriedadeLegal> itens, _, _) = await repositorio.ListarPaginadoAsync(
            null, null, 20, PaginationDirection.Next, new FiltroDasObrigatoriedadesLegais(codigo, null, hoje));

        itens.Select(regra => regra.RegraCodigo).Should().Equal(apareceHoje ? [codigo] : []);

    }

    private static ObrigatoriedadeLegal Regra(string tipo, string codigo, DateOnly inicio, DateOnly? fim) => ObrigatoriedadeLegal.Criar(
        tipo, CategoriaObrigatoriedade.Outros, codigo, new ConcorrenciaDuplaObrigatoria(),
        new TextoNormativo("Descrição", "Lei", null, null), inicio, fim).Value!;
}

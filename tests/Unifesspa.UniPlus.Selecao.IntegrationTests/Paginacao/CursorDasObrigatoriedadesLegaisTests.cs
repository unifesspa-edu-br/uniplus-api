namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Paginacao;

using System.Net;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

/// <summary>
/// O cursor da lista de obrigatoriedades legais continua só o tipo de processo, a categoria e a vigência que o emitiram;
/// o tipo de processo vale por comparação exata.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class CursorDasObrigatoriedadesLegaisTests : IAsyncLifetime
{
    private const string Rota = "/api/selecao/obrigatoriedades-legais";

    private readonly CascadingFixture _fixture;
    private readonly List<string> _tipos = [];
    private readonly ApoioDosCursoresDeSelecao _apoio;

    public CursorDasObrigatoriedadesLegaisTests(CascadingFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDosCursoresDeSelecao(fixture);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        string[] tipos = [.. _tipos];
        await LimpezaDasObrigatoriedadesDoTeste.ApagarDosTiposAsync(db, tipos);
        await db.TiposProcesso.IgnoreQueryFilters().Where(t => tipos.Contains(t.Codigo)).ExecuteDeleteAsync();
    }

    [Theory(DisplayName = "Acrescentar um filtro ao cursor da lista de obrigatoriedades recusa a continuação")]
    [InlineData("categoria", "ETAPA")]
    [InlineData("vigentes", "false")]
    public async Task Continuar_FiltroAcrescentado_Recusa(string parametro, string valor)
    {
        (ProvaDoCursor prova, string tipo) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"tipoProcesso={tipo}", parametro, valor);
        }
    }

    [Fact(DisplayName = "Trocar o tipo de processo ou retirá-lo recusa a continuação, e o tipo com espaços é outro tipo")]
    public async Task Continuar_TipoAlterado_Recusa()
    {
        (ProvaDoCursor prova, string tipo) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"tipoProcesso={tipo}", "tipoProcesso", "OUTRO_TIPO");
            await prova.RecusaTrocaAsync($"tipoProcesso={tipo}", "tipoProcesso", $" {tipo} ");
            await prova.RecusaTrocaAsync($"tipoProcesso={tipo}", "tipoProcesso");
        }
    }

    [Fact(DisplayName = "Trocar a categoria ou a vigência no cursor da lista recusa a continuação")]
    public async Task Continuar_CategoriaOuVigenciaTrocada_Recusa()
    {
        (ProvaDoCursor prova, string tipo) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"tipoProcesso={tipo}&categoria=OUTROS", "categoria", "ETAPA");
            await prova.RecusaTrocaAsync($"tipoProcesso={tipo}&vigentes=false", "vigentes", "true");
        }
    }

    [Fact(DisplayName = "Dizer o padrão de que só as vigentes vêm é a mesma consulta e continua")]
    public async Task Continuar_PadraoExplicito_Continua()
    {
        (ProvaDoCursor prova, string tipo) = await ProvaAsync();
        using (prova)
        {
            await prova.ContinuaComAsync($"tipoProcesso={tipo}", "vigentes", "true");
            await prova.ConfereOPercursoEquivalenteAsync(
                $"tipoProcesso={tipo}", 1, static link => LinkDePaginacao.ComParametro(link, "vigentes", "true"));
        }
    }

    [Fact(DisplayName = "Categoria inválida com o cursor de outra consulta é recusada pelo filtro, não pelo cursor")]
    public async Task Continuar_CategoriaInvalida_RecusaPeloFiltro()
    {
        (ProvaDoCursor prova, string tipo) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaPeloFiltroAsync(
                $"tipoProcesso={tipo}", "categoria", "etapa", HttpStatusCode.BadRequest, "uniplus.selecao.obrigatoriedade_legal.filtro_de_categoria_invalido");
        }
    }

    [Fact(DisplayName = "Cursor sem a chave, de outra coleção, malformado, de outra consulta ou vencido é recusado")]
    public async Task Cursor_Adulterado_Recusa()
    {
        (ProvaDoCursor prova, string tipo) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaCursorAdulteradoAsync($"tipoProcesso={tipo}");
            await prova.RecusaCursorVencidoAsync($"tipoProcesso={tipo}");
            await prova.ConferePayloadAsync($"tipoProcesso={tipo}", vinculadoAConta: false, tipo);
        }
    }

    [Fact(DisplayName = "A conferência vale também no link prev da lista de obrigatoriedades")]
    public async Task Cursor_LinkAnterior_ConfereAConsulta()
    {
        (ProvaDoCursor prova, string tipo) = await ProvaAsync();
        using (prova)
        {
            await prova.ConfereOLinkAnteriorAsync($"tipoProcesso={tipo}", static link => LinkDePaginacao.ComParametro(link, "vigentes", "false"));
        }
    }

    // A lista traduz o tipo de processo de cada regra pelo cadastro, então o tipo semeado precisa existir nele; e o código
    // da regra é único entre as ativas, então leva o do tipo.
    private async Task<(ProvaDoCursor Prova, string Tipo)> ProvaAsync()
    {
        string tipo = $"TIPO_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        _tipos.Add(tipo);
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.TiposProcesso.Add(TipoProcesso.Criar(tipo, "Tipo do teste de cursor", null).Value!);
        db.ObrigatoriedadesLegais.AddRange(Enumerable.Range(0, 3).Select(i => Regra(tipo, $"{tipo}_REGRA_{i}")));
        await db.SaveChangesAsync();

        return (_apoio.Prova(_apoio.Anonimo(), Rota), tipo);
    }

    private static ObrigatoriedadeLegal Regra(string tipo, string codigo) => ObrigatoriedadeLegal.Criar(
        tipo, CategoriaObrigatoriedade.Outros, codigo, new ConcorrenciaDuplaObrigatoria(),
        new TextoNormativo("Descrição", "Lei", null, null), new DateOnly(2026, 1, 1), null).Value!;
}

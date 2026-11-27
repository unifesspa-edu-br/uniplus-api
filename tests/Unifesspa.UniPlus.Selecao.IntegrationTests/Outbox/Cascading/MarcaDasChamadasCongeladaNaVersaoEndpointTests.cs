namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Domain.Entities;
using Domain.ProcessosSeletivos.Publicacao;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PublicacaoPeloEndpointDeTeste;

/// <summary>
/// A marca "organizada em chamadas" fica congelada na versão publicada: alterar a marca da fase no cadastro vivo depois de
/// congelar não muda o que a versão guarda.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class MarcaDasChamadasCongeladaNaVersaoEndpointTests
{
    private const string CodigoDaFase = "HABILITACAO";

    private readonly CascadingFixture _fixture;

    public MarcaDasChamadasCongeladaNaVersaoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A marca de chamadas congelada na versão publicada não muda quando o cadastro vivo da fase é alterado depois")]
    public async Task CadastroAlteradoDepoisDeCongelar_VersaoConservaAMarca()
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        (Guid faseVivaId, bool marcaOriginal) = await FaseVivaAsync();
        Guid processoId = await SemearEPublicarComSucessoAsync(faseVivaId);
        JsonNode congelada = await FaseCongeladaAsync(processoId);
        congelada["tipoFaseOrigemId"]!.GetValue<Guid>().Should().Be(faseVivaId);
        congelada["organizadaEmChamadas"]!.GetValue<bool>().Should().BeTrue();

        await ComAMarcaDoCadastroVivoInvertidaAsync(faseVivaId, marcaOriginal, async () =>
        {
            (await FaseVivaAsync()).Marca.Should().Be(!marcaOriginal);
            (await FaseCongeladaAsync(processoId))["organizadaEmChamadas"]!.GetValue<bool>().Should().BeTrue();
        });
    }

    private async Task ComAMarcaDoCadastroVivoInvertidaAsync(Guid faseVivaId, bool marcaOriginal, Func<Task> conferencia)
    {
        try
        {
            await DefinirMarcaNoCadastroVivoAsync(faseVivaId, !marcaOriginal);
            await conferencia();
        }
        finally
        {
            await DefinirMarcaNoCadastroVivoAsync(faseVivaId, marcaOriginal);
        }
    }

    private async Task<Guid> SemearEPublicarComSucessoAsync(Guid faseVivaId)
    {
        (Guid processoId, Guid documentoId) = await SemearAsync(faseVivaId);
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage resposta = await PublicarAsync(client, processoId, documentoId);
        resposta.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.Accepted, HttpStatusCode.NoContent);

        return processoId;
    }

    private static OpcoesDoProcessoSeletivoPublicavel OpcoesComAFaseViva(Guid faseVivaId) => new()
    {
        Nome = $"{nameof(CadastroAlteradoDepoisDeCongelar_VersaoConservaAMarca)} {Guid.CreateVersion7()}",
        FasesAdicionais =
        [
            FaseDoCronogramaPublicavelDeTeste.Criar(
                FaseDoCronogramaPublicavelDeTeste.OrigemNoEdital(CodigoDaFase).ComChamadas(true) with { OrigemId = faseVivaId },
                2, new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), new(2026, 2, 10, 0, 0, 0, TimeSpan.Zero)),
        ],
        Complementar = static p => p.DefinirFormularioSemConjuntoBasico(
            FormulariosDoProcessoDeTeste.Habilitacao, p.CronogramaFases.Single(static f => f.OrganizadaEmChamadas).Id, obrigatorio: true),
    };

    private async Task<(Guid ProcessoId, Guid DocumentoId)> SemearAsync(Guid faseVivaId)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        OpcoesDoProcessoSeletivoPublicavel opcoes = OpcoesComAFaseViva(faseVivaId);
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, opcoes);
        return (processo.Id, documento.Id);
    }

    private async Task<JsonNode> FaseCongeladaAsync(Guid processoId)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        VersaoConfiguracao versao = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId).OrderByDescending(v => v.NumeroVersao).FirstAsync();
        JsonArray fases = JsonNode.Parse(versao.ConfiguracaoCongelada)!["cronogramaFases"]!["fases"]!.AsArray();

        return fases.Single(static fase => fase!["codigo"]!.GetValue<string>() == CodigoDaFase)!;
    }

    private async Task DefinirMarcaNoCadastroVivoAsync(Guid faseVivaId, bool marca)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.Database.ExecuteSqlAsync($"UPDATE configuracao.tipos_de_fase SET organizada_em_chamadas = {marca} WHERE id = {faseVivaId}");
    }

    private async Task<(Guid Id, bool Marca)> FaseVivaAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        List<FaseCanonica> vivas = await db.FasesCanonicas.AsNoTracking().ToListAsync();
        FaseCanonica fase = vivas.Single(static f => string.Equals(f.Codigo.Valor, CodigoDaFase, StringComparison.Ordinal));
        return (fase.Id, fase.Sinalizadores.OrganizadaEmChamadas);
    }
}

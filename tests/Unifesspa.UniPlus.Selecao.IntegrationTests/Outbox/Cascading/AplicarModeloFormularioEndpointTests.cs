namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A aplicação do modelo de formulário ao processo pelo HTTP, contra o Postgres (UNI-REQ-0144): a
/// cópia grava o formulário com a origem e é por valor — editar o modelo depois não muda o processo
/// — e o fato que passa de outra finalidade para a inscrição respeita os índices únicos da coleta.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class AplicarModeloFormularioEndpointTests
{
    private readonly CascadingFixture _fixture;

    public AplicarModeloFormularioEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Aplicar sem o papel plataforma-admin é 403")]
    public async Task AplicarModelo_SemPapel_Retorna403()
    {
        Guid processoId = await SemearProcessoAsync(nameof(AplicarModelo_SemPapel_Retorna403));
        Guid modeloId = await SemearModeloAsync(FinalidadeFormulario.Inscricao, "QUILOMBOLA");

        (await AplicarAsync(processoId, modeloId, papel: "consulta-publica")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "A cópia grava o formulário com a origem, e editar o modelo depois não muda o processo")]
    public async Task AplicarModelo_ModeloEditadoDepois_ProcessoNaoMuda()
    {
        Guid processoId = await SemearProcessoAsync(nameof(AplicarModelo_ModeloEditadoDepois_ProcessoNaoMuda));
        Guid modeloId = await SemearModeloAsync(FinalidadeFormulario.Inscricao, "QUILOMBOLA");

        HttpResponseMessage resposta = await AplicarAsync(processoId, modeloId);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        using (JsonDocument relatorio = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()))
        {
            relatorio.RootElement.GetProperty("finalidade").GetString().Should().Be("INSCRICAO");
        }

        await using (AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope())
        {
            ConfiguracaoDbContext configuracao = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
            ModeloFormulario modelo = await configuracao.ModelosFormulario.SingleAsync(m => m.Id == modeloId);
            modelo.Atualizar("Editado", null, null, Conteudo("QUILOMBOLA", "PCD"), new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal))
                .IsSuccess.Should().BeTrue();
            await configuracao.SaveChangesAsync();
        }

        ProcessoSeletivo processo = await LerProcessoAsync(processoId);
        processo.FatosColetados.ForaDoConjuntoBasico().Select(static f => f.FatoCodigo).Should().Equal("QUILOMBOLA");
        processo.FormularioDe(FinalidadeFormulario.Inscricao)!.ModeloOrigemId.Should().Be(modeloId);
    }

    [Fact(DisplayName = "O fato da habilitação passa para a inscrição na mesma gravação, sem colidir nos índices únicos")]
    public async Task AplicarModelo_FatoDeOutraFinalidade_MudaDeFormularioNaMesmaGravacao()
    {
        Guid processoId = await SemearProcessoAsync(nameof(AplicarModelo_FatoDeOutraFinalidade_MudaDeFormularioNaMesmaGravacao));
        (await AplicarAsync(processoId, await SemearModeloAsync(FinalidadeFormulario.Habilitacao, "QUILOMBOLA"))).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage resposta = await AplicarAsync(processoId, await SemearModeloAsync(FinalidadeFormulario.Inscricao, "QUILOMBOLA"));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        ProcessoSeletivo processo = await LerProcessoAsync(processoId);
        processo.FatosColetados.ForaDoConjuntoBasico().Should().ContainSingle().Which.Finalidade.Should().Be(FinalidadeFormulario.Inscricao);
        processo.FormularioDe(FinalidadeFormulario.Habilitacao).Should().NotBeNull("o formulário de habilitação continua, só sem o fato");
    }

    [Fact(DisplayName = "O grupo repetível do modelo é gravado no processo, e reaplicar o modelo o substitui na mesma gravação")]
    public async Task AplicarModelo_ComGrupo_GravaESubstitui()
    {
        Guid processoId = await SemearProcessoAsync(nameof(AplicarModelo_ComGrupo_GravaESubstitui));
        Guid modeloId = await SemearModeloAsync(
            FinalidadeFormulario.Inscricao,
            Conteudo("QUILOMBOLA") with
            {
                Grupos =
                [
                    new GrupoDoModelo(
                        "COMPOSICAO_FAMILIAR", 1, "DADOS", "Composição familiar", 1, 10, null, Obrigatoriedade.Sempre,
                        [new ItemDoModelo("MAIOR_IDADE", 0, null, "Maior de idade", TipoRenderizacao.Booleano, null, null, Obrigatoriedade.Sempre, null, [], false)]),
                ],
            });

        (await AplicarAsync(processoId, modeloId)).StatusCode.Should().Be(HttpStatusCode.OK);
        HttpResponseMessage reaplicado = await AplicarAsync(processoId, modeloId);

        reaplicado.StatusCode.Should().Be(HttpStatusCode.OK, await reaplicado.Content.ReadAsStringAsync());
        ProcessoSeletivo processo = await LerProcessoAsync(processoId);
        processo.GruposColetados.Should().ContainSingle().Which.Subitens.Should().ContainSingle().Which.FatoCodigo.Should().Be("MAIOR_IDADE");
        processo.FatosColetados.ForaDoConjuntoBasico().Select(static f => f.FatoCodigo).Should().Equal("QUILOMBOLA");
    }

    private async Task<Guid> SemearProcessoAsync(string nome)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
        return processo.Id;
    }

    private Task<Guid> SemearModeloAsync(FinalidadeFormulario finalidade, params string[] fatos) => SemearModeloAsync(finalidade, Conteudo(fatos));

    private async Task<Guid> SemearModeloAsync(FinalidadeFormulario finalidade, ConteudoDoModelo conteudo)
    {
        ModeloFormulario modelo = ModeloFormulario.Criar(
            $"MODELO_{Guid.NewGuid():N}"[..30], "Modelo", null, finalidade, null, conteudo,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)).Value!;
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext db = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        db.ModelosFormulario.Add(modelo);
        await db.SaveChangesAsync();
        return modelo.Id;
    }

    private static ConteudoDoModelo Conteudo(params string[] fatos) => new(
        "Formulário",
        [
            new("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null, null),
            new("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null, null),
        ],
        [.. fatos.Select(static (fato, ordem) => new ItemDoModelo(
            fato, ordem, "DADOS", fato, TipoRenderizacao.Booleano, null, null, Obrigatoriedade.Sempre, null, [], false))],
        [],
        [], []);

    private async Task<ProcessoSeletivo> LerProcessoAsync(Guid processoId)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        return await db.ProcessosSeletivos.AsNoTracking()
            .Include(static p => p.Formularios)
            .Include(static p => p.Campos)
            .Include(static p => p.GruposColetados).ThenInclude(static g => g.Subitens)
            .SingleAsync(p => p.Id == processoId);
    }

    private async Task<HttpResponseMessage> AplicarAsync(Guid processoId, Guid modeloId, string papel = "plataforma-admin")
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri($"/api/selecao/admin/processos-seletivos/{processoId}/formularios/aplicacoes-de-modelo", UriKind.Relative))
        {
            Content = JsonContent.Create(new { modeloId }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, papel);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }
}

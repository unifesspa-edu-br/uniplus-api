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
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A aplicação do modelo de formulário a um formulário do processo, citado pelo código, pelo HTTP, contra o
/// Postgres (UNI-REQ-0144): a cópia grava o formulário com a origem e é por valor — editar o modelo depois não
/// muda o processo —, o fato que passa de um formulário para outro respeita os índices únicos da coleta, e o
/// formulário que o processo não tem é recusado.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class AplicarModeloFormularioEndpointTests
{
    private const string FormularioComplementar = "DADOS_COMPLEMENTARES";

    private static readonly DeclaracaoDoFormulario DeOutraFase = new(ServeAFaseDeInscricao: false, Obrigatorio: true);

    private readonly CascadingFixture _fixture;

    public AplicarModeloFormularioEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "A cópia grava o formulário com a origem, e editar o modelo depois não muda o processo")]
    public async Task AplicarModelo_ModeloEditadoDepois_ProcessoNaoMuda()
    {
        Guid processoId = await SemearProcessoAsync(nameof(AplicarModelo_ModeloEditadoDepois_ProcessoNaoMuda));
        Guid modeloId = await SemearModeloAsync(DeclaracaoDoFormulario.DaInscricao, "QUILOMBOLA");

        HttpResponseMessage resposta = await AplicarAsync(processoId, modeloId, FormulariosDoProcessoDeTeste.Inscricao);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        await EditarModeloAsync(modeloId, Conteudo("QUILOMBOLA", "PCD"));

        ProcessoSeletivo processo = await LerProcessoAsync(processoId);
        processo.FatosColetados.ForaDoConjuntoBasico().Select(static f => f.FatoCodigo).Should().Equal("QUILOMBOLA");
        processo.FormularioDoCodigo(FormulariosDoProcessoDeTeste.Inscricao)!.ModeloDeOrigem!.Id.Should().Be(modeloId);
        ExigirItensLigadosAoFormularioDaInscricao(processo);
    }

    [Fact(DisplayName = "O fato de um formulário passa para o formulário da inscrição na mesma gravação, sem colidir nos índices únicos")]
    public async Task AplicarModelo_FatoDeOutroFormulario_MudaDeFormularioNaMesmaGravacao()
    {
        Guid processoId = await SemearProcessoAsync(nameof(AplicarModelo_FatoDeOutroFormulario_MudaDeFormularioNaMesmaGravacao), comFormularioComplementar: true);
        (await AplicarAsync(processoId, await SemearModeloAsync(DeOutraFase, "QUILOMBOLA"), FormularioComplementar)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage resposta = await AplicarAsync(processoId, await SemearModeloAsync(DeclaracaoDoFormulario.DaInscricao, "QUILOMBOLA"), FormulariosDoProcessoDeTeste.Inscricao);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        ProcessoSeletivo processo = await LerProcessoAsync(processoId);
        Guid formularioDaInscricao = processo.FormularioDoCodigo(FormulariosDoProcessoDeTeste.Inscricao)!.Id;
        processo.FatosColetados.ForaDoConjuntoBasico().Should().ContainSingle().Which.FormularioId.Should().Be(formularioDaInscricao);
        processo.FormularioDoCodigo(FormularioComplementar).Should().NotBeNull("o outro formulário continua, só sem o fato");
        processo.CamposDoFormulario(FormularioComplementar).Should().BeEmpty();
    }

    [Fact(DisplayName = "Aplicar o modelo a um formulário que o processo não tem é recusado com 422 e nada é criado")]
    public async Task AplicarModelo_FormularioInexistente_422()
    {
        Guid processoId = await SemearProcessoAsync(nameof(AplicarModelo_FormularioInexistente_422));
        Guid modeloId = await SemearModeloAsync(DeclaracaoDoFormulario.DaInscricao, "QUILOMBOLA");

        HttpResponseMessage resposta = await AplicarAsync(processoId, modeloId, "FORMULARIO_QUE_NAO_EXISTE");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.formulario_processo.formulario_inexistente");
        problema.RootElement.GetProperty("errors").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("field").GetString().Should().Be("formularioCodigo");
        (await LerProcessoAsync(processoId)).FormularioDoCodigo("FORMULARIO_QUE_NAO_EXISTE").Should().BeNull();
    }

    [Fact(DisplayName = "O grupo repetível do modelo é gravado no processo, e reaplicar o modelo o substitui na mesma gravação")]
    public async Task AplicarModelo_ComGrupo_GravaESubstitui()
    {
        Guid processoId = await SemearProcessoAsync(nameof(AplicarModelo_ComGrupo_GravaESubstitui));
        Guid modeloId = await SemearModeloAsync(DeclaracaoDoFormulario.DaInscricao, ConteudoComGrupoDaComposicaoFamiliar());

        (await AplicarAsync(processoId, modeloId, FormulariosDoProcessoDeTeste.Inscricao)).StatusCode.Should().Be(HttpStatusCode.OK);
        HttpResponseMessage reaplicado = await AplicarAsync(processoId, modeloId, FormulariosDoProcessoDeTeste.Inscricao);

        reaplicado.StatusCode.Should().Be(HttpStatusCode.OK, await reaplicado.Content.ReadAsStringAsync());
        ProcessoSeletivo processo = await LerProcessoAsync(processoId);
        processo.GruposColetados.Should().ContainSingle().Which.Subitens.Should().ContainSingle().Which.FatoCodigo.Should().Be("MAIOR_IDADE");
        ExigirItensLigadosAoFormularioDaInscricao(processo);
        processo.FatosColetados.ForaDoConjuntoBasico().Select(static f => f.FatoCodigo).Should().Equal("QUILOMBOLA");
    }

    private static void ExigirItensLigadosAoFormularioDaInscricao(ProcessoSeletivo processo)
    {
        Guid formularioId = processo.FormularioDoCodigo(FormulariosDoProcessoDeTeste.Inscricao)!.Id;
        processo.CamposDoFormulario(FormulariosDoProcessoDeTeste.Inscricao).Should().NotBeEmpty();
        processo.Campos.Should().OnlyContain(f => f.FormularioId == formularioId);
        processo.GruposColetados.Should().OnlyContain(g => g.FormularioId == formularioId);
    }

    private async Task EditarModeloAsync(Guid modeloId, ConteudoDoModelo conteudo)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext configuracao = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ModeloFormulario modelo = await configuracao.ModelosFormulario.SingleAsync(m => m.Id == modeloId);
        modelo.Atualizar("Editado", null, null, conteudo, new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal))
            .IsSuccess.Should().BeTrue();
        await configuracao.SaveChangesAsync();
    }

    private async Task<Guid> SemearProcessoAsync(string nome, bool comFormularioComplementar = false)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel
        {
            Nome = $"{nome} {Guid.CreateVersion7()}",
            Complementar = comFormularioComplementar
                ? static p => p.DefinirFormularioSemConjuntoBasico(FormularioComplementar, p.CronogramaFases.Single().Id, obrigatorio: false)
                : null,
        });
        return processo.Id;
    }

    private Task<Guid> SemearModeloAsync(DeclaracaoDoFormulario declaracao, params string[] fatos) => SemearModeloAsync(declaracao, Conteudo(fatos));

    private async Task<Guid> SemearModeloAsync(DeclaracaoDoFormulario declaracao, ConteudoDoModelo conteudo)
    {
        ModeloFormulario modelo = ModeloFormulario.Criar(
            $"MODELO_{Guid.NewGuid():N}"[..30].ToUpperInvariant(), "Modelo", null, declaracao, null, conteudo,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)).Value!;
        modelo.Ativar().IsSuccess.Should().BeTrue();
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.ModelosFormulario.Add(modelo);
        await db.SaveChangesAsync();
        await db.AprovarCatalogoAsync();
        return modelo.Id;
    }

    private static ConteudoDoModelo Conteudo(params string[] fatos) => new(
        "Formulário",
        [
            new("DADOS", 0, TipoPaginaFormulario.Secao, null, "Dados", null, null, null),
            new("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null, null),
        ],
        [.. fatos.Select(static (fato, ordem) => new ItemDoModelo(
            fato, ordem, "DADOS", new ApresentacaoDoCampo(fato, TipoDeCampo.SimOuNao), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, [])))],
        [],
        [], []);

    private static ConteudoDoModelo ConteudoComGrupoDaComposicaoFamiliar() =>
        Conteudo("QUILOMBOLA") with
        {
            Grupos =
            [
                new GrupoDoModelo(
                    "COMPOSICAO_FAMILIAR", 1, "DADOS", "Composição familiar", new OcorrenciasDoGrupo(1, 10, IncluiCandidato: false),
                    new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre),
                    [new ItemDoModelo(
                        "MAIOR_IDADE", 0, null, new ApresentacaoDoCampo("Maior de idade", TipoDeCampo.SimOuNao), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, []))]),
            ],
        };

    private async Task<ProcessoSeletivo> LerProcessoAsync(Guid processoId)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.ProcessosSeletivos.AsNoTracking()
            .Include(static p => p.Formularios)
            .Include(static p => p.Campos)
            .Include(static p => p.GruposColetados).ThenInclude(static g => g.Subitens)
            .SingleAsync(p => p.Id == processoId);
    }

    private async Task<HttpResponseMessage> AplicarAsync(Guid processoId, Guid modeloId, string formularioCodigo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri($"/api/selecao/admin/processos-seletivos/{processoId}/formularios/aplicacoes-de-modelo", UriKind.Relative))
        {
            Content = JsonContent.Create(new { modeloId, formularioCodigo }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }
}

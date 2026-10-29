namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// A proteção do tipo de documento na exigência, pelo HTTP e com versões de verdade: a exigência
/// copia a proteção do tipo ao ser gravada, a publicação e o fechamento da retificação recusam a
/// que comprova um fato mais protegido que o tipo, e reclassificar o tipo depois não muda o que a
/// exigência já copiou.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class ProtecaoDaExigenciaEndpointTests
{
    private const string FatoSensivel = "QUILOMBOLA";
    private const string RecusaDeProtecao = "uniplus.selecao.processo_seletivo.exigencia_menos_protegida_que_fato_comprovado";

    private static readonly int PrimeiraOrdem = ConjuntoBasicoDaInscricao.Itens.Count;

    private readonly CascadingFixture _fixture;

    public ProtecaoDaExigenciaEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Publicação recusa a exigência pessoal que comprova fato sensível e aceita a sensível")]
    public async Task Publicacao_ExigenciaMenosProtegidaQueOFato_Recusa()
    {
        Contexto ctx = await ProcessoComFatoSensivelAsync(nameof(Publicacao_ExigenciaMenosProtegidaQueOFato_Recusa));
        Guid pessoal = await SemearTipoAsync(ClassificacaoDeProtecao.Pessoal, "ATESTADO_SIMPLES");
        Guid sensivel = await SemearTipoAsync(ClassificacaoDeProtecao.Sensivel, "LAUDO_MEDICO");

        await Aceito(ctx.PutExigenciaAsync(pessoal, ifMatch: null));
        await DeveRecusarAsync(ctx.PublicarAsync(), RecusaDeProtecao);

        await Aceito(ctx.PutExigenciaAsync(sensivel, ifMatch: null));
        await Aceito(ctx.PublicarAsync());
    }

    [Fact(DisplayName = "Tipo reclassificado para sensível entre a gravação e a publicação: a exigência publica sensível")]
    public async Task Publicacao_CongelaAProtecaoVigenteDoTipo()
    {
        Contexto ctx = await ProcessoComFatoSensivelAsync(nameof(Publicacao_CongelaAProtecaoVigenteDoTipo));
        Guid tipo = await SemearTipoAsync(ClassificacaoDeProtecao.Pessoal, "ATESTADO_SIMPLES");
        await Aceito(ctx.PutExigenciaAsync(tipo, ifMatch: null));

        await ReclassificarAsync(tipo, ClassificacaoDeProtecao.Sensivel);
        await Aceito(ctx.PublicarAsync());

        using JsonDocument processo = JsonDocument.Parse(await ctx.ObterProcessoAsync());
        processo.RootElement.GetProperty("documentosExigidos").EnumerateArray().Single()
            .GetProperty("classificacaoProtecao").GetString().Should().Be("SENSIVEL");
    }

    [Fact(DisplayName = "Tipo reclassificado para pessoal entre a gravação e a publicação: a publicação recusa o fato sensível comprovado")]
    public async Task Publicacao_TipoReclassificadoParaMenosProtegido_Recusa()
    {
        Contexto ctx = await ProcessoComFatoSensivelAsync(nameof(Publicacao_TipoReclassificadoParaMenosProtegido_Recusa));
        Guid tipo = await SemearTipoAsync(ClassificacaoDeProtecao.Sensivel, "LAUDO_MEDICO");
        await Aceito(ctx.PutExigenciaAsync(tipo, ifMatch: null));

        await ReclassificarAsync(tipo, ClassificacaoDeProtecao.Pessoal);

        await DeveRecusarAsync(ctx.PublicarAsync(), RecusaDeProtecao);
    }

    [Fact(DisplayName = "Exigência pessoal que comprova a renda per capita (derivada) e o tipo de deficiência é recusada pelo tipo de deficiência")]
    public async Task Publicacao_ComprovaFatoDerivadoEFatoSensivel_RecusaPeloSensivel()
    {
        Contexto ctx = await ProcessoComFatoSensivelAsync(nameof(Publicacao_ComprovaFatoDerivadoEFatoSensivel_RecusaPeloSensivel));
        Guid pessoal = await SemearTipoAsync(ClassificacaoDeProtecao.Pessoal, "DECLARACAO_RENDA_E_SAUDE");

        await Aceito(ctx.PutExigenciaAsync(pessoal, ifMatch: null, comprova: ["RENDA_PER_CAPITA", FatoSensivel]));

        await DeveRecusarAsync(ctx.PublicarAsync(), RecusaDeProtecao);
    }

    [Fact(DisplayName = "Retificar com o tipo reclassificado para pessoal mantém sensível a exigência que a versão vigente já tinha")]
    public async Task Retificacao_MantemAProtecaoCongeladaDaExigenciaExistente()
    {
        Contexto ctx = await ProcessoComFatoSensivelAsync(nameof(Retificacao_MantemAProtecaoCongeladaDaExigenciaExistente));
        Guid tipo = await SemearTipoAsync(ClassificacaoDeProtecao.Sensivel, "LAUDO_MEDICO");
        await Aceito(ctx.PutExigenciaAsync(tipo, ifMatch: null));
        await Aceito(ctx.PublicarAsync());
        await ReclassificarAsync(tipo, ClassificacaoDeProtecao.Pessoal);

        string etag = LerETag(await Aceito(ctx.AbrirAsync()));
        await Aceito(ctx.FecharRetificacaoAsync(etag));

        using JsonDocument processo = JsonDocument.Parse(await ctx.ObterProcessoAsync());
        processo.RootElement.GetProperty("documentosExigidos").EnumerateArray().Single()
            .GetProperty("classificacaoProtecao").GetString().Should().Be("SENSIVEL");
    }

    [Fact(DisplayName = "Exigência nova de um tipo reclassificado congela pelo cadastro vigente na retificação")]
    public async Task Retificacao_ExigenciaNovaDoTipoReclassificado_CongelaPeloCadastro()
    {
        Contexto ctx = await ProcessoComFatoSensivelAsync(nameof(Retificacao_ExigenciaNovaDoTipoReclassificado_CongelaPeloCadastro));
        Guid tipo = await SemearTipoAsync(ClassificacaoDeProtecao.Sensivel, "LAUDO_MEDICO");
        await Aceito(ctx.PutExigenciaAsync(tipo, ifMatch: null, comprova: []));
        await Aceito(ctx.PublicarAsync());
        await ReclassificarAsync(tipo, ClassificacaoDeProtecao.Pessoal);

        string etag = LerETag(await Aceito(ctx.AbrirAsync()));
        etag = LerETag(await Aceito(ctx.PutExigenciaAsync(tipo, etag, comprova: [])));
        await Aceito(ctx.FecharRetificacaoAsync(etag));

        using JsonDocument processo = JsonDocument.Parse(await ctx.ObterProcessoAsync());
        processo.RootElement.GetProperty("documentosExigidos").EnumerateArray().Single()
            .GetProperty("classificacaoProtecao").GetString().Should().Be("PESSOAL");
    }

    [Fact(DisplayName = "O painel de conformidade marca pendente o mesmo caso que a publicação recusa")]
    public async Task Painel_MarcaPendenteOQueAPublicacaoRecusa()
    {
        Contexto ctx = await ProcessoComFatoSensivelAsync(nameof(Painel_MarcaPendenteOQueAPublicacaoRecusa));
        Guid pessoal = await SemearTipoAsync(ClassificacaoDeProtecao.Pessoal, "DECLARACAO_RENDA_E_SAUDE");
        await Aceito(ctx.PutExigenciaAsync(pessoal, ifMatch: null, comprova: ["RENDA_PER_CAPITA", FatoSensivel]));

        await DeveRecusarAsync(ctx.PublicarAsync(), RecusaDeProtecao);
        using JsonDocument painel = JsonDocument.Parse(await ctx.ObterConformidadeAsync());
        painel.RootElement.GetProperty("itens").EnumerateArray()
            .Single(i => i.GetProperty("codigo").GetString() == "exigencia_menos_protegida_que_fato_comprovado")
            .GetProperty("ok").GetBoolean().Should().BeFalse();
    }

    [Fact(DisplayName = "Fechar a retificação recusa a exigência que passou a comprovar fato mais protegido que o tipo")]
    public async Task Retificacao_ExigenciaMenosProtegidaQueOFato_Recusa()
    {
        Contexto ctx = await ProcessoComFatoSensivelAsync(nameof(Retificacao_ExigenciaMenosProtegidaQueOFato_Recusa));
        Guid pessoal = await SemearTipoAsync(ClassificacaoDeProtecao.Pessoal, "ATESTADO_SIMPLES");
        Guid sensivel = await SemearTipoAsync(ClassificacaoDeProtecao.Sensivel, "LAUDO_MEDICO");
        await Aceito(ctx.PutExigenciaAsync(sensivel, ifMatch: null));
        await Aceito(ctx.PublicarAsync());

        string etag = LerETag(await Aceito(ctx.AbrirAsync()));
        etag = LerETag(await Aceito(ctx.PutExigenciaAsync(pessoal, etag)));

        await DeveRecusarAsync(ctx.FecharRetificacaoAsync(etag), RecusaDeProtecao);
    }

    [Fact(DisplayName = "Reclassificar o tipo de documento depois da gravação não muda a exigência publicada")]
    public async Task ReclassificarOTipo_NaoMudaAExigenciaPublicada()
    {
        Contexto ctx = await ProcessoComFatoSensivelAsync(nameof(ReclassificarOTipo_NaoMudaAExigenciaPublicada));
        Guid sensivel = await SemearTipoAsync(ClassificacaoDeProtecao.Sensivel, "LAUDO_MEDICO");
        await Aceito(ctx.PutExigenciaAsync(sensivel, ifMatch: null));
        await Aceito(ctx.PublicarAsync());

        await ReclassificarAsync(sensivel, ClassificacaoDeProtecao.Pessoal);

        using JsonDocument processo = JsonDocument.Parse(await ctx.ObterProcessoAsync());
        JsonElement exigencia = processo.RootElement.GetProperty("documentosExigidos").EnumerateArray().Single();
        exigencia.GetProperty("classificacaoProtecao").GetString().Should().Be("SENSIVEL");
        exigencia.GetProperty("fatosComprovados").EnumerateArray().Select(static f => f.GetString()).Should().Equal(FatoSensivel);
    }

    private async Task<Guid> SemearTipoAsync(ClassificacaoDeProtecao classificacao, string prefixo)
    {
        TipoDocumento tipo = TipoDocumento.Criar(
            $"{prefixo}_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}", prefixo, null, "SAUDE", null, null, null,
            classificacao, "Comprovação documental do candidato.", HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext db = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        db.Add(tipo);
        await db.SaveChangesAsync();
        return tipo.Id;
    }

    private async Task ReclassificarAsync(Guid tipoId, ClassificacaoDeProtecao classificacao)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext db = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        TipoDocumento tipo = await db.TiposDocumento.SingleAsync(t => t.Id == tipoId);
        tipo.Atualizar(
            tipo.Codigo.Valor, tipo.Nome, tipo.Descricao, tipo.Categoria, tipo.FormatosAceitos, tipo.TamanhoMaximoMb, tipo.TipoEquivalente,
            classificacao, tipo.FinalidadeTratamento, tipo.HipoteseLegal).IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
    }

    /// <summary>Um processo publicável cuja inscrição coleta um fato sensível do catálogo.</summary>
    private async Task<Contexto> ProcessoComFatoSensivelAsync(string nome)
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);

        Guid processoId;
        Guid documentoId;
        Guid faseId;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
            processoId = processo.Id;
            documentoId = documento.Id;
            faseId = processo.CronogramaFases.Single().Id;
        }

        Contexto ctx = new(api, api.CreateClient(), processoId, documentoId, faseId);
        (await ctx.PutItensAsync()).StatusCode.Should().Be(HttpStatusCode.NoContent);
        return ctx;
    }

    private static async Task<HttpResponseMessage> Aceito(Task<HttpResponseMessage> chamada)
    {
        HttpResponseMessage resposta = await chamada;
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().BeOneOf([HttpStatusCode.NoContent, HttpStatusCode.Created], corpo);
        return resposta;
    }

    private static async Task DeveRecusarAsync(Task<HttpResponseMessage> chamada, string codigo)
    {
        HttpResponseMessage resposta = await chamada;
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        using JsonDocument problema = JsonDocument.Parse(corpo);
        problema.RootElement.GetProperty("code").GetString().Should().Be(codigo, corpo);
        problema.RootElement.GetProperty("detail").GetString().Should().Contain(FatoSensivel);
    }

    private static string LerETag(HttpResponseMessage resposta)
    {
        resposta.Headers.ETag.Should().NotBeNull();
        return resposta.Headers.ETag!.ToString();
    }

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId, Guid DocumentoEditalId, Guid FaseId)
    {
        public Task<HttpResponseMessage> PutItensAsync() =>
            EnviarAsync(
                HttpMethod.Put,
                new Uri($"/api/selecao/admin/processos-seletivos/{ProcessoId}/formularios/INSCRICAO/itens", UriKind.Relative),
                new
                {
                    itens = new object[]
                    {
                        new
                        {
                            fatoCodigo = FatoSensivel,
                            ordem = PrimeiraOrdem,
                            rotulo = FatoSensivel,
                            tipoRenderizacao = "BOOLEANO",
                            obrigatoriedade = "NUNCA",
                            precondicao = (object?)null,
                            etapaCodigo = FormularioDeInscricaoHttp.Secao,
                        },
                    },
                },
                ifMatch: null);

        public Task<HttpResponseMessage> PutExigenciaAsync(Guid tipoDocumentoId, string? ifMatch, string[]? comprova = null) =>
            EnviarAsync(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/documentos-exigidos", UriKind.Relative),
                new[]
                {
                    new
                    {
                        tipo = "FOLHA",
                        documento = new
                        {
                            exigidoNaFaseId = FaseId,
                            finalidade = "INSCRICAO",
                            tipoDocumentoId,
                            aplicabilidade = "GERAL",
                            obrigatorio = false,
                            condicoes = Array.Empty<object>(),
                            basesLegais = Array.Empty<object>(),
                            formatosPermitidos = "QUALQUER",
                            fatosComprovados = comprova ?? [FatoSensivel],
                        },
                    },
                },
                ifMatch);

        public Task<HttpResponseMessage> PublicarAsync() =>
            EnviarAsync(HttpMethod.Post, Rota("publicacao"), CorpoDoAto(DocumentoEditalId, "EDITAL_ABERTURA", "001/2026"), ifMatch: null);

        public Task<HttpResponseMessage> AbrirAsync() =>
            EnviarAsync(HttpMethod.Post, Rota("retificacao-em-curso"), new { motivo = "Troca do documento exigido" }, ifMatch: null);

        public async Task<HttpResponseMessage> FecharRetificacaoAsync(string etag)
        {
            Guid documento = await SemearDocumentoConfirmadoAsync().ConfigureAwait(false);
            return await EnviarAsync(HttpMethod.Post, Rota("retificacao-em-curso/fechamento"), CorpoDoAto(documento, "EDITAL_RETIFICACAO", "001/2026-R"), etag)
                .ConfigureAwait(false);
        }

        public Task<string> ObterConformidadeAsync() => LerAsync(Rota("conformidade"));

        public Task<string> ObterProcessoAsync() => LerAsync(Rota(string.Empty));

        private async Task<string> LerAsync(Uri rota)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, rota);
            request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
            HttpResponseMessage resposta = await Client.SendAsync(request).ConfigureAwait(false);
            string corpo = await resposta.Content.ReadAsStringAsync().ConfigureAwait(false);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
            return corpo;
        }

        private async Task<Guid> SemearDocumentoConfirmadoAsync()
        {
            await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(ProcessoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
            documento.Confirmar(2048, string.Concat(Enumerable.Repeat("cd45670189", 7))[..64], TimeProvider.System).IsSuccess.Should().BeTrue();
            await db.DocumentosAPublicar.AddAsync(documento);
            await db.SaveChangesAsync();
            return documento.Id;
        }

        private Uri Rota(string recurso) => new($"/api/selecao/processos-seletivos/{ProcessoId}/{recurso}".TrimEnd('/'), UriKind.Relative);

        private static object CorpoDoAto(Guid documentoId, string tipoAto, string numero) => new
        {
            numero,
            documentoEditalId = documentoId,
            ato = new
            {
                orgao = "CEPS",
                serie = "EDITAL",
                ano = 2026,
                dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                assinante = "Diretor do CEPS",
                tipoAtoCodigo = tipoAto,
            },
        };

        private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, Uri rota, object corpo, string? ifMatch)
        {
            using HttpRequestMessage request = new(metodo, rota) { Content = JsonContent.Create(corpo) };
            request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
            if (ifMatch is not null)
            {
                request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            }

            return await Client.SendAsync(request).ConfigureAwait(false);
        }
    }
}

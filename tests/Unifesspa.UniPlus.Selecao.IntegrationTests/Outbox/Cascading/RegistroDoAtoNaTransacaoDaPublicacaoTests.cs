namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Events;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PublicacaoPeloEndpointDeTeste;

/// <summary>
/// Contra o host real (Wolverine + Postgres via Testcontainers): publicar e retificar gravam a versão, o ato do processo,
/// a divulgação e o ato normativo na mesma transação, e o ato normativo existe assim que a resposta volta.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class RegistroDoAtoNaTransacaoDaPublicacaoTests
{
    private readonly CascadingFixture _fixture;

    public RegistroDoAtoNaTransacaoDaPublicacaoTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "publicar registra o ato em Publicações na mesma transação, sem esperar fila nenhuma")]
    public async Task Publicar_RegistraOAtoNaMesmaTransacao()
    {
        (CascadingApiFactory api, HttpClient client) = await PrepararAsync();
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(api, nameof(Publicar_RegistraOAtoNaMesmaTransacao));
        (await PublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid atoId = await ObterAtoIdAsync(api, processoId);
        AtoNormativo ato = await AtoNoRegistroCentral.LerAsync(api, atoId);

        ato.TipoCodigo.Should().Be("EDITAL_ABERTURA");
        ato.TipoNome.Should().Be("Edital de abertura", "o nome do tipo é o que a Seleção conferiu ao publicar");
        await ConferirGravadoNaTransacaoDaPublicacaoAsync(api, processoId, ato);
    }

    [Fact(DisplayName = "publicar e retificar em sequência registram os dois atos, com a cadeia e uma só vaga")]
    public async Task PublicarERetificar_RegistramOsDoisAtos_EmCadeia()
    {
        (CascadingApiFactory api, HttpClient client) = await PrepararAsync();
        (Guid processoId, Guid documentoAbertura) = await SemearProcessoAsync(api, nameof(PublicarERetificar_RegistramOsDoisAtos_EmCadeia));
        (await PublicarAsync(client, processoId, documentoAbertura)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Guid documentoRetificacao = await SemearDocumentoConfirmadoAsync(api, processoId);
        (await RetificarAsync(client, processoId, documentoRetificacao)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (Guid abertura, Guid retificacao) = await AtosDaCadeiaAsync(api, processoId);

        await AtoNoRegistroCentral.LerAsync(api, abertura);
        (await AtoNoRegistroCentral.LerAsync(api, retificacao)).AtoRetificadoId.Should().Be(abertura, "a cadeia em Publicações espelha a da Seleção");
        await ConferirUmaSoVagaHerdadaAsync(api, processoId, retificacao);
    }

    [Fact(DisplayName = "o mesmo id percorre a cadeia inteira: evento, versão e ato registrado")]
    public async Task IdentidadeDoAto_AtravessaEventoVersaoERegistro()
    {
        (CascadingApiFactory api, HttpClient client) = await PrepararAsync();
        DomainEventCollector collector = api.Services.GetRequiredService<DomainEventCollector>();
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(api, nameof(IdentidadeDoAto_AtravessaEventoVersaoERegistro));
        (await PublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        VersaoConfiguracao versao = await VersaoDoProcessoAsync(api, processoId);
        AtoNormativo ato = await AtoNoRegistroCentral.LerAsync(api, versao.AtoCriadorId);
        ProcessoPublicadoEvent? evento = await CascadingScenariosTests.EsperarEventoAsync(collector, processoId, TimeSpan.FromSeconds(20));

        ConferirAIdentidade(evento, versao, ato);
    }

    [Fact(DisplayName = "publicar declarando um tipo que não congela configuração é recusado com 422, sem escrever nada")]
    public async Task Publicar_ComTipoQueNaoCongela_Recusa422_SemEscrever()
    {
        (CascadingApiFactory api, HttpClient client) = await PrepararAsync();
        await TiposDeAtoSeeder.SemearTipoAsync(api.Services, "AVISO_NAO_CONGELANTE", unicoPorObjeto: false, congela: false);
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(api, nameof(Publicar_ComTipoQueNaoCongela_Recusa422_SemEscrever));

        HttpResponseMessage resposta = await PublicarAsync(client, processoId, documentoId, "AVISO_NAO_CONGELANTE");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await VersoesDoProcessoAsync(api, processoId)).Should().Be(0, "a recusa precede qualquer escrita");
    }

    [Fact(DisplayName = "retificar declarando um tipo que não congela configuração é recusado com 422, sem criar versão")]
    public async Task Retificar_ComTipoQueNaoCongela_Recusa422_SemEscrever()
    {
        (CascadingApiFactory api, HttpClient client) = await PrepararAsync();
        await TiposDeAtoSeeder.SemearTipoAsync(api.Services, "AVISO_NAO_CONGELANTE", unicoPorObjeto: false, congela: false);
        (Guid processoId, Guid documentoAbertura) = await SemearProcessoAsync(api, nameof(Retificar_ComTipoQueNaoCongela_Recusa422_SemEscrever));
        (await PublicarAsync(client, processoId, documentoAbertura)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Guid documentoRetificacao = await SemearDocumentoConfirmadoAsync(api, processoId);

        HttpResponseMessage resposta = await RetificarAsync(client, processoId, documentoRetificacao, "AVISO_NAO_CONGELANTE");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await VersoesDoProcessoAsync(api, processoId)).Should().Be(1, "só a versão da abertura existe");
    }

    private static void ConferirAIdentidade(ProcessoPublicadoEvent? evento, VersaoConfiguracao versao, AtoNormativo ato)
    {
        evento.Should().NotBeNull();
        evento!.EditalId.Should().Be(versao.AtoCriadorId, "evento e versão");
        evento.SnapshotPublicacaoId.Should().Be(versao.Id);
        evento.HashEdital.Should().Be(versao.AtoCriadorHash);
        evento.HashConfiguracao.Should().Be(versao.HashConfiguracao);
        ato.Id.Should().Be(versao.AtoCriadorId, "versão e ato registrado");
        ato.VersaoInvocada!.Id.Should().Be(versao.Id);
        ato.VersaoInvocada.Hash.Should().Be(versao.HashConfiguracao);
    }

    private static async Task ConferirGravadoNaTransacaoDaPublicacaoAsync(CascadingApiFactory api, Guid processoId, AtoNormativo ato)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        VersaoConfiguracao versao = await db.VersoesConfiguracao.AsNoTracking().SingleAsync(v => v.ProcessoSeletivoId == processoId);

        ato.PublicadoEm.Should().Be(versao.VigenteAPartirDe, "um só instante por comando: o da vigência da versão é o da divulgação do ato");
        (await db.Set<VinculoAtoEntidade>().AsNoTracking().AnyAsync(v => v.AtoId == ato.Id && v.EntidadeTipo == "PROCESSO_SELETIVO" && v.EntidadeId == processoId))
            .Should().BeTrue("sem o vínculo a consulta dos atos do certame não acha o ato");
        (await db.Set<VagaDeLinhagem>().AsNoTracking().AnyAsync(l => l.EntidadeId == processoId && l.RaizId == ato.Id))
            .Should().BeTrue("a vaga do objeto é reservada pela linhagem do próprio ato");
    }

    private static async Task ConferirUmaSoVagaHerdadaAsync(CascadingApiFactory api, Guid processoId, Guid retificacao)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        (await db.Set<VinculoAtoEntidade>().AsNoTracking().AnyAsync(v => v.AtoId == retificacao && v.EntidadeId == processoId))
            .Should().BeTrue("a retificação herda o vínculo com o certame");
        (await db.Set<VagaDeLinhagem>().AsNoTracking().CountAsync(l => l.EntidadeId == processoId && l.TipoCodigo == "EDITAL_ABERTURA"))
            .Should().Be(1, "a retificação pertence à linhagem que já ocupa o objeto");
    }

    private static async Task<(Guid Abertura, Guid Retificacao)> AtosDaCadeiaAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        IQueryable<VersaoConfiguracao> versoes = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>()
            .VersoesConfiguracao.AsNoTracking().Where(v => v.ProcessoSeletivoId == processoId);

        return (
            await versoes.Where(v => v.AtoCriadorRetificaId == null).Select(v => v.AtoCriadorId).SingleAsync(),
            await versoes.Where(v => v.AtoCriadorRetificaId != null).Select(v => v.AtoCriadorId).SingleAsync());
    }

    private static async Task<VersaoConfiguracao> VersaoDoProcessoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>()
            .VersoesConfiguracao.AsNoTracking().SingleAsync(v => v.ProcessoSeletivoId == processoId);
    }

    private static async Task<int> VersoesDoProcessoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>()
            .VersoesConfiguracao.AsNoTracking().CountAsync(v => v.ProcessoSeletivoId == processoId);
    }

    private async Task<(CascadingApiFactory Api, HttpClient Cliente)> PrepararAsync()
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        return (_fixture.Factory, _fixture.Factory.CreateClient());
    }
}

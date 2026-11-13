namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// O documento de designação dos perfis de governança e o anexo do pedido, pelo host real: o verificador, a leitura
/// assinada, a trilha e a serialização com a decisão.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class PedidosDeConcessaoAnexoTests : PedidosDeConcessaoTestBase
{
    public PedidosDeConcessaoAnexoTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Pedido do perfil de gestor de acessos aguarda o documento, fora da fila do gestor e sem aceite nem aprovação")]
    public async Task PerfilDeGestor_AguardaODocumento()
    {
        await ResetarAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(betina, PerfisDeSistema.GestorDeAcessosId, null, Leandro);

        (await SituacaoAsync(betina, id)).Should().Be("aguardando_documento");
        (await ListarFilaAsync(diogo, "gestor")).Should().NotContain(id);
        await ConferirRecusaAsync(await AceitarAsync(Cliente(Leandro), id), HttpStatusCode.UnprocessableEntity, "anexo_nao_verificado");
        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "situacao_invalida");
    }

    [Fact(DisplayName = "O anexo em verificação ainda não é o documento e não se lê")]
    public async Task AnexoEmVerificacao_NaoSatisfazNemSeLe()
    {
        await ResetarAsync();
        using HttpClient betina = Cliente(Betina);
        Guid id = await CriarAsync(betina, PerfisDeSistema.GestorDeAcessosId, null, Leandro);

        Guid anexo = await AnexarEmVerificacaoAsync(betina, id);

        (await SituacaoAsync(betina, id)).Should().Be("aguardando_documento", "o anexo em verificação ainda não é o documento verificado");
        using HttpResponseMessage naoVerificado = await betina.GetAsync(new Uri($"{Rota}/{id}/anexos/{anexo}", UriKind.Relative));
        await ConferirRecusaAsync(naoVerificado, HttpStatusCode.UnprocessableEntity, "anexo_nao_verificado");
    }

    [Fact(DisplayName = "O documento verificado libera o aceite e se lê por URL assinada, sem cache, pelo solicitante e pelo aprovador")]
    public async Task DocumentoVerificado_LiberaOAceiteESeLe()
    {
        await ResetarAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        (Guid id, Guid anexo) = await PedidoDeGestorComDocumentoAsync(betina);

        using HttpResponseMessage leitura = await betina.GetAsync(new Uri($"{Rota}/{id}/anexos/{anexo}", UriKind.Relative));
        JsonElement corpo = await CorpoAsync(leitura, HttpStatusCode.OK);
        leitura.Headers.CacheControl!.NoStore.Should().BeTrue();
        using HttpClient http = new();
        (await http.GetByteArrayAsync(new Uri(corpo.GetProperty("url").GetString()!, UriKind.Absolute))).Should().Equal(Pdf);
        (await diogo.GetAsync(new Uri($"{Rota}/{id}/anexos/{anexo}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Cliente(Isabella).GetAsync(new Uri($"{Rota}/{id}/anexos/{anexo}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ConferirQueOResultadoFoiAoAnexoAsync(anexo);
    }

    [Fact(DisplayName = "Com o documento, o pedido de gestor passa pelo gestor e pelo Representante, e a trilha registra as leituras")]
    public async Task DocumentoVerificado_PassaPelasDuasEtapas()
    {
        await ResetarAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        (Guid id, Guid anexo) = await PedidoDeGestorComDocumentoAsync(betina);
        await betina.GetAsync(new Uri($"{Rota}/{id}/anexos/{anexo}", UriKind.Relative));
        await diogo.GetAsync(new Uri($"{Rota}/{id}/anexos/{anexo}", UriKind.Relative));

        await AprovarNasDuasEtapasAsync(diogo, marlene, id);

        (await ConcessoesAsync(Leandro, PerfisDeSistema.GestorDeAcessosId)).Single().EscopoTipo.Should().Be(Autorizacao.Domain.Enums.TipoDeEscopo.Instituicao);
        (await TiposDaTrilhaAsync(diogo, id)).Should().Equal(
            "pedido_criado", "anexo_enviado", "anexo_verificado", "beneficiario_aceitou", "termo_aceito", "anexo_lido", "anexo_lido", "aprovado", "aprovado");
    }

    [Fact(DisplayName = "O veredicto de recusa do verificador encerra o anexo, e o pedido de governança continua sem documento")]
    public async Task AnexoRecusado_NaoSatisfazODocumento()
    {
        await ResetarAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(betina, PerfisDeSistema.GestorDeAcessosId, null, Leandro);
        Guid anexo = await AnexarEmVerificacaoAsync(betina, id);

        await InjetarResultadoAsync(anexo, VeredictoDaVerificacao.Recusado);
        await AguardarAsync(async () => await SituacaoDoAnexoAsync(betina, id, anexo) == "recusado", "o anexo não chegou a recusado");

        (await SituacaoAsync(betina, id)).Should().Be("aguardando_documento");
        (await ListarFilaAsync(diogo, "gestor")).Should().NotContain(id);
        (await TiposDaTrilhaAsync(betina, id)).Should().Contain("anexo_recusado").And.NotContain("anexo_verificado");
        (await EnviarAsync(betina, HttpMethod.Get, $"{Rota}/{id}/anexos/{anexo}")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Só o solicitante anexa, e o PDF tem de 1 byte a 10 MB")]
    public async Task Anexo_SoOSolicitanteEDentroDoTamanho()
    {
        await ResetarAsync();
        using HttpClient betina = Cliente(Betina);
        Guid id = await PedidoComumAceitoAsync(betina);

        (await EnviarAsync(Cliente(Leandro), HttpMethod.Post, $"{Rota}/{id}/anexos", new { tamanhoDoPdf = 100 })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ConferirRecusaAsync(await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/anexos", new { tamanhoDoPdf = 0 }), HttpStatusCode.UnprocessableEntity, "anexo_invalido");
        await ConferirRecusaAsync(await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/anexos", new { tamanhoDoPdf = 10L * 1024 * 1024 + 1 }), HttpStatusCode.UnprocessableEntity, "anexo_invalido");
    }

    [Fact(DisplayName = "A confirmação do anexo exige o ETag, e depois da primeira decisão o envio e a confirmação são recusados")]
    public async Task Anexo_ConfirmacaoEDepoisDaDecisao()
    {
        await ResetarAsync();
        using HttpClient betina = Cliente(Betina);
        Guid id = await PedidoComumAceitoAsync(betina);
        Guid pendente = (await CorpoAsync(
            await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/anexos", new { tamanhoDoPdf = 100 }), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        await ConferirRecusaAsync(
            await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/anexos/{pendente}/confirmacao", new { etag = "  " }), HttpStatusCode.UnprocessableEntity, "anexo_invalido");

        (await EnviarAsync(await ClienteComPermissoesAsync(Diogo, AprovarFuncional), HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        await ConferirRecusaAsync(await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/anexos", new { tamanhoDoPdf = 100 }), HttpStatusCode.UnprocessableEntity, "situacao_invalida");
        await ConferirRecusaAsync(
            await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/anexos/{pendente}/confirmacao", new { etag = "\"etag\"" }), HttpStatusCode.UnprocessableEntity, "situacao_invalida");
    }

    [Fact(DisplayName = "O veredicto do anexo na trilha tem o verificador como autor, e não o solicitante")]
    public async Task TrilhaDoAnexo_AutorEOVerificador()
    {
        await ResetarAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(betina, PerfisDeSistema.GestorDeAcessosId, null, Leandro);
        await AnexarVerificadoAsync(betina, id);
        await AceitarAsync(Cliente(Leandro), id);

        JsonElement trilha = await LerAsync(diogo, $"{Rota}/{id}/trilha");

        JsonElement verificado = trilha.EnumerateArray().Single(e => e.GetProperty("tipo").GetString() == "anexo_verificado");
        verificado.GetProperty("autor").GetProperty("sub").GetString().Should().Be("verificador");
        trilha.EnumerateArray().Single(e => e.GetProperty("tipo").GetString() == "anexo_enviado")
            .GetProperty("autor").GetProperty("sub").GetString().Should().Be(Betina.Sub.ToString());
    }

    [Fact(DisplayName = "Anexar e decidir o pedido não correm juntos: quem leu antes da aprovação não grava o anexo depois dela")]
    public async Task Anexo_SerializadoComAAprovacao()
    {
        await ResetarAsync();
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await PedidoAceitoAsync(Cliente(Betina), await SemearPerfilInternoAsync());

        await using AsyncServiceScope escopoDoAnexo = Fixture.Factory.Services.CreateAsyncScope();
        IPedidoDeConcessaoRepository repositorio = escopoDoAnexo.ServiceProvider.GetRequiredService<IPedidoDeConcessaoRepository>();
        PedidoDeConcessao lido = (await repositorio.ObterParaAlterarAsync(id))!;
        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        lido.NovoAnexo(1024).IsSuccess.Should().BeTrue("o pedido lido ainda aguardava o gestor");
        repositorio.Atualizar(lido);
        Func<Task> gravar = () => escopoDoAnexo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();

        await gravar.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    // O pedido do perfil de gestor de acessos para Leandro, com o documento verificado e posto no bucket definitivo, aceito.
    private async Task<(Guid Id, Guid Anexo)> PedidoDeGestorComDocumentoAsync(HttpClient betina)
    {
        Guid id = await CriarAsync(betina, PerfisDeSistema.GestorDeAcessosId, null, Leandro);
        Guid anexo = await AnexarEmVerificacaoAsync(betina, id);
        await InjetarResultadoAsync(anexo, VeredictoDaVerificacao.Verificado);
        await AguardarAsync(async () => await SituacaoDoAnexoAsync(betina, id, anexo) == "verificado", "o anexo não chegou a verificado");
        await PromoverObjetoAsync(id, anexo);
        (await SituacaoAsync(betina, id)).Should().Be("aguardando_beneficiario", "o documento verificado libera o pedido para o aceite");
        (await AceitarAsync(Cliente(Leandro), id)).StatusCode.Should().Be(HttpStatusCode.OK);

        return (id, anexo);
    }

    // O pedido de perfil comum, que não exige o termo, aceita anexo até a primeira decisão sem tê-lo declarado.
    private async Task<Guid> PedidoComumAceitoAsync(HttpClient betina) => await PedidoAceitoAsync(betina, await SemearPerfilInternoAsync());

    private async Task<Guid> PedidoAceitoAsync(HttpClient betina, Guid perfil)
    {
        Guid id = await CriarAsync(betina, perfil, await SemearUnidadeAsync(), Leandro);
        await AceitarAsync(Cliente(Leandro), id);

        return id;
    }

    private static async Task AprovarNasDuasEtapasAsync(HttpClient gestor, HttpClient representante, Guid id)
    {
        (await EnviarAsync(gestor, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SituacaoAsync(gestor, id)).Should().Be("aguardando_protecao_de_dados");
        (await EnviarAsync(representante, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task ConferirQueOResultadoFoiAoAnexoAsync(Guid anexo)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Instalacoes.AnyAsync(i => i.Id == anexo))
            .Should().BeFalse("o resultado do anexo é despachado ao anexo, e não à instalação");
    }

    private async Task PromoverObjetoAsync(Guid pedido, Guid anexo)
    {
        // O verificador real promove o PDF limpo ao bucket definitivo; aqui o resultado é injetado, e o objeto é posto à mão.
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        IStorageService storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
        using MemoryStream conteudo = new(Pdf);
        await storage.UploadAsync(
            MonolitoDaInstalacaoFixture.BucketDefinitivo, $"autorizacao/pedido/{pedido}/anexo/{anexo}/definitivo", conteudo, "application/pdf");
    }
}

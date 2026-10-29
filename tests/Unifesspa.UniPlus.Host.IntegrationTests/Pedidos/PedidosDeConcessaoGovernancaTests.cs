namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Os pedidos que a governança do acesso trata de modo próprio, pelo host real: o documento de designação dos perfis de
/// governança, a dispensa da segunda aprovação, o perfil que muda entre as etapas, a renovação da concessão vencida e a
/// concessão ativa igual.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class PedidosDeConcessaoGovernancaTests : PedidosDeConcessaoTestBase
{
    public PedidosDeConcessaoGovernancaTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Pedido do perfil de gestor de acessos exige o documento verificado; o anexo só se lê verificado, e a leitura entra na trilha")]
    public async Task PerfilDeGestor_ExigeDocumentoVerificado()
    {
        await ResetarAsync();
        Guid perfil = PerfisDeSistema.GestorDeAcessosId;
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        Guid id = await CriarAsync(betina, perfil, null, Leandro);
        await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite");

        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "documento_obrigatorio");

        Guid anexo = await AnexarEmVerificacaoAsync(betina, id);
        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "documento_obrigatorio");
        using HttpResponseMessage naoVerificado = await betina.GetAsync(new Uri($"{Rota}/{id}/anexos/{anexo}", UriKind.Relative));
        await ConferirRecusaAsync(naoVerificado, HttpStatusCode.UnprocessableEntity, "anexo_nao_verificado");

        await InjetarResultadoAsync(anexo, VeredictoDaVerificacao.Verificado);
        await AguardarAsync(async () => await SituacaoDoAnexoAsync(betina, id, anexo) == "verificado", "o anexo não chegou a verificado");
        await PromoverObjetoAsync(id, anexo);

        await using (AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Instalacoes.AnyAsync(i => i.Id == anexo))
                .Should().BeFalse("o resultado do anexo é despachado ao anexo, e não à instalação");
        }

        using HttpResponseMessage leitura = await betina.GetAsync(new Uri($"{Rota}/{id}/anexos/{anexo}", UriKind.Relative));
        JsonElement corpo = await CorpoAsync(leitura, HttpStatusCode.OK);
        leitura.Headers.CacheControl!.NoStore.Should().BeTrue();
        using HttpClient http = new();
        (await http.GetByteArrayAsync(new Uri(corpo.GetProperty("url").GetString()!, UriKind.Absolute))).Should().Equal(Pdf);
        (await diogo.GetAsync(new Uri($"{Rota}/{id}/anexos/{anexo}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);
        using HttpClient estranho = Cliente(Isabella);
        (await estranho.GetAsync(new Uri($"{Rota}/{id}/anexos/{anexo}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SituacaoAsync(betina, id)).Should().Be("aguardando_encarregada");
        (await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        Concessao concessao = (await ConcessoesAsync(Leandro, perfil)).Single();
        concessao.EscopoTipo.Should().Be(Autorizacao.Domain.Enums.TipoDeEscopo.Instituicao);
        JsonElement trilha = await LerAsync(diogo, $"{Rota}/{id}/trilha");
        string[] tipos = [.. trilha.EnumerateArray().Select(e => e.GetProperty("tipo").GetString()!)];
        tipos.Should().Equal("pedido_criado", "beneficiario_aceitou", "anexo_enviado", "anexo_verificado", "anexo_lido", "anexo_lido", "aprovado", "aprovado");
        trilha.EnumerateArray().Single(e => e.GetProperty("tipo").GetString() == "anexo_verificado")
            .GetProperty("detalhe").GetString().Should().Contain(new string('a', 64));
    }

    [Fact(DisplayName = "O veredicto de recusa do verificador encerra o anexo, e o pedido de governança continua sem documento")]
    public async Task AnexoRecusado_NaoSatisfazODocumento()
    {
        await ResetarAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(betina, PerfisDeSistema.GestorDeAcessosId, null, Leandro);
        await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite");
        Guid anexo = await AnexarEmVerificacaoAsync(betina, id);

        await InjetarResultadoAsync(anexo, VeredictoDaVerificacao.Recusado);
        await AguardarAsync(async () => await SituacaoDoAnexoAsync(betina, id, anexo) == "recusado", "o anexo não chegou a recusado");

        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "documento_obrigatorio");
        (await TiposDaTrilhaAsync(betina, id)).Should().Contain("anexo_recusado").And.NotContain("anexo_verificado");
        (await EnviarAsync(betina, HttpMethod.Get, $"{Rota}/{id}/anexos/{anexo}")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Só o solicitante anexa, o PDF tem de 1 byte a 10 MB, e depois da primeira decisão o anexo é recusado")]
    public async Task Anexo_RegrasDeEnvio()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilSensivelAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite");

        (await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/anexos", new { tamanhoDoPdf = 100 })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ConferirRecusaAsync(await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/anexos", new { tamanhoDoPdf = 0 }), HttpStatusCode.UnprocessableEntity, "anexo_invalido");
        await ConferirRecusaAsync(await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/anexos", new { tamanhoDoPdf = 10L * 1024 * 1024 + 1 }), HttpStatusCode.UnprocessableEntity, "anexo_invalido");
        Guid pendente = (await CorpoAsync(
            await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/anexos", new { tamanhoDoPdf = 100 }), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        await ConferirRecusaAsync(
            await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/anexos/{pendente}/confirmacao", new { etag = "  " }), HttpStatusCode.UnprocessableEntity, "anexo_invalido");

        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        await ConferirRecusaAsync(await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/anexos", new { tamanhoDoPdf = 100 }), HttpStatusCode.UnprocessableEntity, "situacao_invalida");
        await ConferirRecusaAsync(
            await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/anexos/{pendente}/confirmacao", new { etag = "\"etag\"" }), HttpStatusCode.UnprocessableEntity, "situacao_invalida");
    }

    [Fact(DisplayName = "O primeiro pedido da encarregada exige o documento de designação e passa só pelo gestor, com a dispensa na trilha")]
    public async Task PerfilDaEncarregada_PassaSoPeloGestor()
    {
        await ResetarAsync();
        Guid perfil = PerfisDeSistema.EncarregadaDeDadosId;
        using HttpClient betina = Cliente(Betina);
        using HttpClient marlene = Cliente(Marlene);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(betina, perfil, null, Marlene);
        await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/aceite");
        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "documento_obrigatorio");
        await AnexarVerificadoAsync(betina, id);

        JsonElement aprovado = await CorpoAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.OK);

        aprovado.GetProperty("situacao").GetString().Should().Be("aprovado");
        (await ConcessoesAsync(Marlene, perfil)).Should().ContainSingle();
        JsonElement trilha = await LerAsync(diogo, $"{Rota}/{id}/trilha");
        trilha.EnumerateArray().Single(e => e.GetProperty("tipo").GetString() == "segunda_aprovacao_dispensada")
            .GetProperty("detalhe").GetString().Should().Contain("designacao_da_encarregada");
    }

    [Fact(DisplayName = "Beneficiária que já é a encarregada tem o pedido sensível concluído pelo gestor, com a dispensa na trilha")]
    public async Task BeneficiariaJaEEncarregada_DispensaASegundaAprovacao()
    {
        await ResetarAsync();
        await SemearConcessaoAsync(Marlene, PerfisDeSistema.EncarregadaDeDadosId, EscopoDaConcessao.DaInstituicao());
        Guid perfil = await SemearPerfilSensivelAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient marlene = Cliente(Marlene);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(betina, perfil, unidade, Marlene);
        await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/aceite");

        JsonElement aprovado = await CorpoAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.OK);

        aprovado.GetProperty("situacao").GetString().Should().Be("aprovado");
        (await ConcessoesAsync(Marlene, perfil)).Should().ContainSingle();
        JsonElement trilha = await LerAsync(diogo, $"{Rota}/{id}/trilha");
        trilha.EnumerateArray().Single(e => e.GetProperty("tipo").GetString() == "segunda_aprovacao_dispensada")
            .GetProperty("detalhe").GetString().Should().Contain("beneficiaria_ja_e_encarregada");
    }

    [Fact(DisplayName = "Perfil alterado entre as etapas devolve o pedido ao gestor, responde perfil_alterado e nada é concedido")]
    public async Task PerfilAlterado_VoltaAoGestor()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilSensivelAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpClient isabella = await ClienteComPermissoesAsync(Isabella, AprovarFuncional);
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite");
        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
        await ExecutarSqlAsync(
            "INSERT INTO autorizacao.perfil_permissoes (perfil_id, codigo) VALUES ({0}, 'teste:inscricoes:operar')", perfil);

        await ConferirRecusaAsync(await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.Conflict, "perfil_alterado");

        (await SituacaoAsync(betina, id)).Should().Be("aguardando_gestor");
        (await ConcessoesAsync(Leandro, perfil)).Should().BeEmpty();
        JsonElement trilhaDaTentativa = await LerAsync(diogo, $"{Rota}/{id}/trilha");
        JsonElement devolucao = trilhaDaTentativa.EnumerateArray().Single(e => e.GetProperty("tipo").GetString() == "perfil_alterado_entre_etapas");
        devolucao.GetProperty("autor").GetProperty("sub").GetString().Should().Be(Marlene.Sub.ToString());
        devolucao.GetProperty("detalhe").GetString().Should().Contain("encarregada");
        (await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement final = await LerAsync(betina, $"{Rota}/{id}");
        final.GetProperty("decisoes").EnumerateArray().First().GetProperty("permissoesAvaliadas").EnumerateArray().Should().HaveCount(1);
        final.GetProperty("decisoes").EnumerateArray().Last().GetProperty("permissoesAvaliadas").EnumerateArray().Should().HaveCount(2);
        (await ConcessoesAsync(Leandro, perfil)).Should().ContainSingle();
    }

    [Fact(DisplayName = "Retry com a mesma chave depois de perfil_alterado reproduz a recusa guardada e não decide de novo")]
    public async Task PerfilAlterado_RetryComAMesmaChave_ReproduzARecusa()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilSensivelAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarFuncional, AprovarProtecaoDeDados);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite");
        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
        await ExecutarSqlAsync(
            "INSERT INTO autorizacao.perfil_permissoes (perfil_id, codigo) VALUES ({0}, 'teste:inscricoes:operar')", perfil);
        string chave = Guid.NewGuid().ToString();
        await ConferirRecusaAsync(await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/aprovacoes", chave: chave), HttpStatusCode.Conflict, "perfil_alterado");

        using HttpResponseMessage repeticao = await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/aprovacoes", chave: chave);

        await ConferirRecusaAsync(repeticao, HttpStatusCode.Conflict, "perfil_alterado");
        repeticao.Headers.Contains("Idempotency-Replayed").Should().BeTrue();
        (await SituacaoAsync(betina, id)).Should().Be("aguardando_gestor");
        (await ConcessoesAsync(Leandro, perfil)).Should().BeEmpty();
        (await TiposDaTrilhaAsync(diogo, id)).Count(t => t == "perfil_alterado_entre_etapas").Should().Be(1);
    }

    [Fact(DisplayName = "Concessão igual vencida e não revogada é revogada pelo sistema e renovada pelo pedido, na mesma transação")]
    public async Task ConcessaoVencida_ERenovada()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
        await SemearConcessaoAsync(Leandro, perfil, EscopoDaConcessao.DaUnidade(unidade));
        await ExecutarSqlAsync(
            "UPDATE autorizacao.concessoes SET concedida_em = now() - interval '3 days', valida_ate = now() - interval '1 day' WHERE perfil_id = {0}", perfil);
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite");

        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        Concessao[] concessoes = await ConcessoesAsync(Leandro, perfil);
        concessoes.Should().HaveCount(2);
        Concessao antiga = concessoes.Single(c => c.RevogadaEm is not null);
        antiga.MotivoRevogacao.Should().Be($"Renovação pelo pedido {id}.");
        antiga.RevogadaPorSub.Should().Be(Diogo.Sub.ToString());
        Concessao nova = concessoes.Single(c => c.RevogadaEm is null);
        nova.OrigemId.Should().Be(id);
        nova.EstaAtiva(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact(DisplayName = "Falha na gravação da renovação desfaz tudo: a vencida segue sem revogação e o pedido sem aprovação")]
    public async Task Renovacao_ComFalhaNaGravacao_NaoGravaNada()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
        await SemearConcessaoAsync(Leandro, perfil, EscopoDaConcessao.DaUnidade(unidade));
        await ExecutarSqlAsync(
            "UPDATE autorizacao.concessoes SET concedida_em = now() - interval '3 days', valida_ate = now() - interval '1 day' WHERE perfil_id = {0}", perfil);
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite");
        // Uma concessão revogada da mesma origem faz a inserção da nova violar o índice de origem.
        await using (AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            IConcessaoRepository repositorio = scope.ServiceProvider.GetRequiredService<IConcessaoRepository>();
            Perfil entidade = (await repositorio.ObterParaConcederAsync(perfil))!;
            Concessao revogada = Concessao.Conceder(
                IdentidadeDe(Leandro), entidade, EscopoDaConcessao.DaUnidade(Guid.NewGuid()),
                new OrigemDaConcessao(Autorizacao.Domain.Enums.TipoDeOrigem.Pedido, id), IdentidadeDe(Diogo), DateTimeOffset.UtcNow,
                scope.ServiceProvider.GetRequiredService<ICatalogoDePermissoesConsultavel>()).Value!;
            revogada.Revogar(IdentidadeDe(Diogo), "Semente do teste.", DateTimeOffset.UtcNow);
            await repositorio.AdicionarAsync(revogada);
            await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();
        }

        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.Conflict, "ja_tem_concessao");

        (await SituacaoAsync(betina, id)).Should().Be("aguardando_gestor");
        Concessao[] concessoes = await ConcessoesAsync(Leandro, perfil);
        concessoes.Where(c => c.UnidadeId == unidade).Should().ContainSingle().Which.RevogadaEm.Should().BeNull();
        (await LerAsync(betina, $"{Rota}/{id}")).GetProperty("decisoes").GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "Concessão ativa igual recusa a aprovação, e o pedido segue aguardando o gestor")]
    public async Task ConcessaoAtivaIgual_RecusaAAprovacao()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
        await SemearConcessaoAsync(Leandro, perfil, EscopoDaConcessao.DaUnidade(unidade));
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite");

        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.Conflict, "ja_tem_concessao");

        (await SituacaoAsync(betina, id)).Should().Be("aguardando_gestor");
        (await ConcessoesAsync(Leandro, perfil)).Should().ContainSingle();
        JsonElement pedido = await LerAsync(betina, $"{Rota}/{id}");
        pedido.GetProperty("decisoes").GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "Perfil desativado depois do pedido recusa a aprovação e deixa a concessão vencida intacta e o pedido sem decisão")]
    public async Task PerfilInativoNaAprovacao_NaoGravaNada()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
        await SemearConcessaoAsync(Leandro, perfil, EscopoDaConcessao.DaUnidade(unidade));
        await ExecutarSqlAsync(
            "UPDATE autorizacao.concessoes SET concedida_em = now() - interval '3 days', valida_ate = now() - interval '1 day' WHERE perfil_id = {0}", perfil);
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite");
        await ExecutarSqlAsync("UPDATE autorizacao.perfis SET ativo = false WHERE id = {0}", perfil);

        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "perfil_inativo");

        (await SituacaoAsync(betina, id)).Should().Be("aguardando_gestor");
        (await ConcessoesAsync(Leandro, perfil)).Should().ContainSingle().Which.RevogadaEm.Should().BeNull();
        (await LerAsync(betina, $"{Rota}/{id}")).GetProperty("decisoes").GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "O veredicto do anexo na trilha tem o verificador como autor, e não o solicitante")]
    public async Task TrilhaDoAnexo_AutorEOVerificador()
    {
        await ResetarAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(betina, PerfisDeSistema.GestorDeAcessosId, null, Leandro);
        await AnexarVerificadoAsync(betina, id);

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
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite");

        await using AsyncServiceScope escopoDoAnexo = Fixture.Factory.Services.CreateAsyncScope();
        IPedidoDeConcessaoRepository repositorio = escopoDoAnexo.ServiceProvider.GetRequiredService<IPedidoDeConcessaoRepository>();
        PedidoDeConcessao lido = (await repositorio.ObterParaAlterarAsync(id))!;
        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        lido.NovoAnexo(1024).IsSuccess.Should().BeTrue("o pedido lido ainda aguardava o gestor");
        repositorio.Atualizar(lido);
        Func<Task> gravar = () => escopoDoAnexo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();

        await gravar.Should().ThrowAsync<DbUpdateConcurrencyException>();
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

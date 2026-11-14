namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Os pedidos que a governança do acesso trata de modo próprio, pelo host real: o perfil que muda entre as etapas, o
/// prazo que vence entre elas, a renovação da concessão vencida e a concessão ativa igual.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class PedidosDeConcessaoGovernancaTests : PedidosDeConcessaoTestBase
{
    public PedidosDeConcessaoGovernancaTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Troca de versão sem acréscimo entre as etapas devolve o pedido ao gestor na própria troca, na versão nova")]
    public async Task TrocaSemAcrescimo_VoltaAoGestorNaVersaoNova()
    {
        (Guid perfil, Guid id) = await AprovadoPeloGestorAsync("teste:atendimento:analisar", "teste:laudos:consultar");

        IReadOnlyList<EnviarEmail> avisos = await TrocarComposicaoAsync(perfil, "teste:atendimento:analisar");

        avisos.Where(a => a.Finalidade != "perfil.versao_encerrada").Should().OnlyContain(a => a.Finalidade == "pedido.fila_do_gestor");
        JsonElement voltado = await LerAsync(Cliente(Betina), $"{Rota}/{id}");
        (voltado.GetProperty("situacao").GetString(), voltado.GetProperty("versao").GetInt32()).Should().Be(("aguardando_gestor", 2));
        JsonElement troca = voltado.GetProperty("trocasDeVersao").EnumerateArray().Single();
        (troca.GetProperty("versaoAnterior").GetInt32(), troca.GetProperty("versaoNova").GetInt32(), troca.GetProperty("desfecho").GetString())
            .Should().Be((1, 2, "voltou_ao_gestor"));
    }

    [Fact(DisplayName = "Devolvido ao gestor pela troca sem acréscimo, o pedido é aprovado de novo e a concessão vale na versão nova")]
    public async Task TrocaSemAcrescimo_AprovacaoFinalNaVersaoNova()
    {
        (Guid perfil, Guid id) = await AprovadoPeloGestorAsync("teste:atendimento:analisar", "teste:laudos:consultar");
        await TrocarComposicaoAsync(perfil, "teste:atendimento:analisar");
        using HttpClient isabella = await ClienteComPermissoesAsync(Isabella, AprovarFuncional);
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);

        (await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await ConcessoesAsync(Leandro, perfil)).Should().ContainSingle().Which.VersaoId.Should().NotBeNull();
    }

    [Fact(DisplayName = "Troca de versão com acréscimo entre as etapas devolve o pedido e avisa as duas partes")]
    public async Task TrocaComAcrescimo_DevolveOPedido()
    {
        (Guid perfil, Guid id) = await AprovadoPeloGestorAsync("teste:atendimento:analisar");

        IReadOnlyList<EnviarEmail> avisos = await TrocarComposicaoAsync(perfil, "teste:atendimento:analisar", "teste:laudos:consultar");

        avisos.Where(a => a.Finalidade == "perfil.pedido_devolvido_pela_janela").Select(a => a.Destinatario).Should().BeEquivalentTo([Betina.Email, Leandro.Email]);
        JsonElement devolvido = await LerAsync(Cliente(Betina), $"{Rota}/{id}");
        (devolvido.GetProperty("situacao").GetString(), devolvido.GetProperty("versao").GetInt32()).Should().Be(("devolvido", 1));
        devolvido.GetProperty("encerradoEm").ValueKind.Should().NotBe(JsonValueKind.Null);
        devolvido.GetProperty("trocasDeVersao").EnumerateArray().Single().GetProperty("desfecho").GetString().Should().Be("devolvido");
    }

    [Fact(DisplayName = "O pedido devolvido pela troca com acréscimo não aceita a segunda etapa, e nada é concedido")]
    public async Task TrocaComAcrescimo_NadaEConcedido()
    {
        (Guid perfil, Guid id) = await AprovadoPeloGestorAsync("teste:atendimento:analisar");
        await TrocarComposicaoAsync(perfil, "teste:atendimento:analisar", "teste:laudos:consultar");
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);

        await ConferirRecusaAsync(await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "situacao_invalida");

        (await ConcessoesAsync(Leandro, perfil)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Prazo do gestor vencido antes da proteção de dados devolve o pedido ao gestor com 409 e deixa o evento na trilha")]
    public async Task PrazoVencido_VoltaAoGestorERegistraNaTrilha()
    {
        (Guid perfil, Guid id) = await AprovadoPeloGestorAsync(new { validaAte = DateTimeOffset.UtcNow.AddDays(30) }, "teste:atendimento:analisar");
        await ExecutarSqlAsync("UPDATE autorizacao.pedidos_de_concessao SET valida_ate = now() - interval '1 day' WHERE id = {0}", id);
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);

        await ConferirRecusaAsync(await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.Conflict, "prazo_vencido");

        JsonElement pedido = await LerAsync(Cliente(Betina), $"{Rota}/{id}");
        pedido.GetProperty("situacao").GetString().Should().Be("aguardando_gestor");
        pedido.GetProperty("validaAte").ValueKind.Should().Be(JsonValueKind.Null);
        (await TiposDaTrilhaAsync(marlene, id)).Should().Contain("prazo_vencido_entre_etapas");
        (await ConcessoesAsync(Leandro, perfil)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Concessão igual vencida e não revogada é revogada pelo sistema e renovada pelo pedido, na mesma transação")]
    public async Task ConcessaoVencida_ERenovada()
    {
        (Guid perfil, Guid unidade) = await PerfilComConcessaoDeLeandroAsync(vencida: true);
        Guid id = await PedidoAceitoAsync(perfil, unidade);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);

        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        Concessao[] concessoes = await ConcessoesAsync(Leandro, perfil);
        concessoes.Should().HaveCount(2);
        Concessao antiga = concessoes.Single(c => c.RevogadaEm is not null);
        (antiga.MotivoRevogacao, antiga.RevogadaPorSub).Should().Be(($"Renovação pelo pedido {id}.", Diogo.Sub.ToString()));
        Concessao nova = concessoes.Single(c => c.RevogadaEm is null);
        nova.OrigemId.Should().Be(id);
        nova.EstaAtiva(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact(DisplayName = "Falha na gravação da renovação desfaz tudo: a vencida segue sem revogação e o pedido sem aprovação")]
    public async Task Renovacao_ComFalhaNaGravacao_NaoGravaNada()
    {
        (Guid perfil, Guid unidade) = await PerfilComConcessaoDeLeandroAsync(vencida: true);
        Guid id = await PedidoAceitoAsync(perfil, unidade);
        await SemearConcessaoRevogadaDaOrigemAsync(perfil, id);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);

        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.Conflict, "ja_tem_concessao");

        (await SituacaoAsync(diogo, id)).Should().Be("aguardando_gestor");
        (await ConcessoesAsync(Leandro, perfil)).Where(c => c.UnidadeId == unidade).Should().ContainSingle().Which.RevogadaEm.Should().BeNull();
        (await LerAsync(diogo, $"{Rota}/{id}")).GetProperty("decisoes").GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "Concessão ativa igual recusa a aprovação, e o pedido segue aguardando o gestor")]
    public async Task ConcessaoAtivaIgual_RecusaAAprovacao()
    {
        (Guid perfil, Guid unidade) = await PerfilComConcessaoDeLeandroAsync(vencida: false);
        Guid id = await PedidoAceitoAsync(perfil, unidade);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);

        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.Conflict, "ja_tem_concessao");

        (await SituacaoAsync(diogo, id)).Should().Be("aguardando_gestor");
        (await ConcessoesAsync(Leandro, perfil)).Should().ContainSingle();
        (await LerAsync(diogo, $"{Rota}/{id}")).GetProperty("decisoes").GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "Perfil desativado depois do pedido recusa a aprovação e deixa a concessão vencida intacta e o pedido sem decisão")]
    public async Task PerfilInativoNaAprovacao_NaoGravaNada()
    {
        (Guid perfil, Guid unidade) = await PerfilComConcessaoDeLeandroAsync(vencida: true);
        Guid id = await PedidoAceitoAsync(perfil, unidade);
        await ExecutarSqlAsync("UPDATE autorizacao.perfis SET ativo = false WHERE id = {0}", perfil);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);

        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "perfil_inativo");

        (await SituacaoAsync(diogo, id)).Should().Be("aguardando_gestor");
        (await ConcessoesAsync(Leandro, perfil)).Should().ContainSingle().Which.RevogadaEm.Should().BeNull();
        (await LerAsync(diogo, $"{Rota}/{id}")).GetProperty("decisoes").GetArrayLength().Should().Be(0);
    }

    // O pedido de Betina para Leandro, aceito por ele.
    private async Task<Guid> PedidoAceitoAsync(Guid perfil, Guid unidade)
    {
        Guid id = await CriarAsync(Cliente(Betina), perfil, unidade, Leandro);
        await AceitarAsync(Cliente(Leandro), id);
        return id;
    }

    private Task<(Guid Perfil, Guid Id)> AprovadoPeloGestorAsync(params string[] permissoes) => AprovadoPeloGestorAsync(null, permissoes);

    // O pedido aceito de um perfil novo com as permissões, aprovado por Diogo na etapa do gestor.
    private async Task<(Guid Perfil, Guid Id)> AprovadoPeloGestorAsync(object? corpo, params string[] permissoes)
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilAsync("Análise de atendimento", permissoes);
        Guid id = await PedidoAceitoAsync(perfil, await SemearUnidadeAsync());
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes", corpo)).StatusCode.Should().Be(HttpStatusCode.OK);
        return (perfil, id);
    }

    // Um perfil interno com a concessão de Leandro na unidade, vencida e não revogada quando pedido.
    private async Task<(Guid Perfil, Guid Unidade)> PerfilComConcessaoDeLeandroAsync(bool vencida)
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
        await SemearConcessaoAsync(Leandro, perfil, EscopoDaConcessao.DaUnidade(unidade));
        if (vencida)
        {
            await ExecutarSqlAsync(
                "UPDATE autorizacao.concessoes SET concedida_em = now() - interval '3 days', valida_ate = now() - interval '1 day' WHERE perfil_id = {0}", perfil);
        }

        return (perfil, unidade);
    }

    // Uma concessão revogada da mesma origem faz a inserção da nova violar o índice de origem.
    private async Task SemearConcessaoRevogadaDaOrigemAsync(Guid perfil, Guid pedido)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        IConcessaoRepository repositorio = scope.ServiceProvider.GetRequiredService<IConcessaoRepository>();
        Concessao revogada = ConcessaoBuilder
            .DoPerfil((await repositorio.ObterParaConcederAsync(perfil))!, scope.ServiceProvider.GetRequiredService<ICatalogoDePermissoesConsultavel>())
            .Para(IdentidadeDe(Leandro)).NaUnidade(Guid.NewGuid()).DaOrigem(new OrigemDaConcessao(Autorizacao.Domain.Enums.TipoDeOrigem.Pedido, pedido))
            .ConcedidaPor(IdentidadeDe(Diogo)).Em(DateTimeOffset.UtcNow).Construir();
        revogada.Revogar(new AtoDoAgente(IdentidadeDe(Diogo), DateTimeOffset.UtcNow), "Semente do teste.");
        await repositorio.AdicionarAsync(revogada);
        await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().SaveChangesAsync();
    }
}

namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A reclassificação de uma permissão em uso pelo host real: a subida de classe e a mudança de escopo suspendem a
/// permissão nas concessões, as etapas de revisão a devolvem e a segregação de funções vale em cada decisão.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class LiberacaoDaReclassificacaoNoHostTests : ReclassificacaoNoHostTestBase
{
    public LiberacaoDaReclassificacaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>O corpo da decisão que libera todas as concessões na etapa de proteção de dados.</summary>
    private static object LiberarTodasNaProtecao => new { etapa = EtapaProtecao, resultado = ResultadoDaLiberacaoCodigo.Liberada, todas = true };

    private RegistroDeDecisoesDeTeste Registro => Fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>();

    [Fact(DisplayName = "A subida de classe suspende a permissão só nas concessões do perfil e o resto do perfil continua")]
    public async Task Subida_SuspendeSoAPermissaoReclassificada()
    {
        Cenario perfil = await CenarioAsync([Consultar, Sensivel], Leandro, Betina);

        await SubirAClasseAsync();

        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().Equal(Consultar);
        (await PermissoesAsync(Betina, perfil.Perfil)).Should().Equal(Consultar);
        (await ReclassificacoesAsync(perfil.Perfil)).Should().Be(2);
    }

    [Fact(DisplayName = "A proteção de dados libera a permissão de uma das pessoas e só ela a recupera")]
    public async Task Liberacao_DevolveAPermissaoSoAQuemFoiLiberado()
    {
        Cenario perfil = await CenarioAsync([Consultar, Sensivel], Leandro, Betina);
        await SubirAClasseAsync();

        await LiberarAsync(Marlene, EtapaProtecao, perfil.Perfil, await ReclassificacaoAsync(perfil.Perfil, Sensivel), perfil.De(Leandro));

        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().BeEquivalentTo(Consultar, Sensivel);
        (await PermissoesAsync(Betina, perfil.Perfil)).Should().Equal(Consultar);
        (await OrigemDaBaseAsync(perfil.De(Leandro), Sensivel)).Should().Be(OrigemDaBaseCodigo.Liberacao);
    }

    [Fact(DisplayName = "A mudança de escopo é liberada pela gestão de acessos, sem a proteção de dados")]
    public async Task Escopo_LiberadoPelaGestaoDeAcessos()
    {
        Cenario perfil = await CenarioAsync([Consultar], Leandro);
        await ImplantarAsync(Consultar, ClassificacaoDeProtecao.Interno, deUnidade: false);
        await ImplantarRealAsync();
        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().BeEmpty();

        await LiberarAsync(Isabella, EtapaGestao, perfil.Perfil, await ReclassificacaoAsync(perfil.Perfil, Consultar));

        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().Equal(Consultar);
    }

    [Fact(DisplayName = "A descida que não leva a classe abaixo da base mantém a concessão suspensa na fila e a etapa exigida a libera")]
    public async Task Descida_ParcialDepoisDaSubida_MantemAConcessaoNaFilaELiberavel()
    {
        Cenario perfil = await CenarioAsync([Consultar], Leandro);
        await ImplantarAsync(Consultar, ClassificacaoDeProtecao.Publico);
        await ImplantarAsync(Consultar, ClassificacaoDeProtecao.Pessoal);
        await ImplantarRealAsync();
        Guid descida = await ReclassificacaoAsync(perfil.Perfil, Consultar);

        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().BeEmpty();
        (await FilaDaProtecaoAsync()).Should().Contain(descida.ToString());
        await LiberarAsync(Marlene, EtapaProtecao, perfil.Perfil, descida);

        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().Equal(Consultar);
        (await OrigemDaBaseAsync(perfil.De(Leandro), Consultar)).Should().Be(OrigemDaBaseCodigo.Liberacao);
        (await FilaDaProtecaoAsync()).Should().NotContain(descida.ToString());
    }

    [Fact(DisplayName = "A concessão que venceu sem ser revogada sai da fila da reclassificação")]
    public async Task ConcessaoVencida_SaiDaFila()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro);
        await SubirAClasseAsync();
        Guid reclassificacao = await ReclassificacaoAsync(perfil.Perfil, Sensivel);

        await VencerAsync(perfil.De(Leandro));

        (await FilaDaProtecaoAsync()).Should().NotContain(reclassificacao.ToString());
    }

    [Fact(DisplayName = "Liberar todas não libera a concessão que venceu: só a ativa ganha a liberação")]
    public async Task LiberarTodas_DeixaDeForaAConcessaoVencida()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro, Betina);
        await SubirAClasseAsync();
        Guid reclassificacao = await ReclassificacaoAsync(perfil.Perfil, Sensivel);
        await VencerAsync(perfil.De(Leandro));

        await LiberarAsync(Marlene, EtapaProtecao, perfil.Perfil, reclassificacao);

        (await LiberacoesAsync(reclassificacao)).Should().Be(1);
        (await PermissoesAsync(Betina, perfil.Perfil)).Should().Equal(Sensivel);
    }

    [Theory(DisplayName = "Subida de classe e mudança de escopo juntas pedem as duas etapas, em qualquer ordem")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SubidaEEscopoJuntos_PedemAsDuasEtapas(bool gestaoPrimeiro)
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro);
        await ImplantarAsync(Sensivel, ClassificacaoDeProtecao.Interno, deUnidade: false);
        await ImplantarRealAsync();
        Guid reclassificacao = await ReclassificacaoAsync(perfil.Perfil, Sensivel);
        (string primeira, Persona quemPrimeiro, string segunda, Persona quemDepois) = gestaoPrimeiro
            ? (EtapaGestao, Isabella, EtapaProtecao, Marlene)
            : (EtapaProtecao, Marlene, EtapaGestao, Isabella);

        await LiberarAsync(quemPrimeiro, primeira, perfil.Perfil, reclassificacao);
        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().BeEmpty();
        await LiberarAsync(quemDepois, segunda, perfil.Perfil, reclassificacao);

        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().Equal(Sensivel);
    }

    [Fact(DisplayName = "A decisão sobre a reclassificação superada dá conflito e a liberação da mais recente resolve")]
    public async Task Sucessivas_SoAMaisRecenteDecide()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro);
        await SubirAClasseAsync();
        Guid primeira = await ReclassificacaoAsync(perfil.Perfil, Sensivel);
        await SubirAClasseAsync();
        Guid segunda = await ReclassificacaoAsync(perfil.Perfil, Sensivel);

        using HttpResponseMessage superada = await DecidirAsync(Marlene, EtapaProtecao, perfil.Perfil, primeira);
        await LiberarAsync(Marlene, EtapaProtecao, perfil.Perfil, segunda);

        await ConferirRecusaDaReclassificacaoAsync(superada, HttpStatusCode.Conflict, "superada");
        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().Equal(Sensivel);
    }

    [Fact(DisplayName = "Quem tem a concessão em jogo não decide por ela, e a opção de todas deixa a própria de fora")]
    public async Task Segregacao_BeneficiarioNaoDecidePelaPropriaConcessao()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro, Marlene);
        await SubirAClasseAsync();
        Guid reclassificacao = await ReclassificacaoAsync(perfil.Perfil, Sensivel);

        using HttpResponseMessage propria = await DecidirAsync(Marlene, EtapaProtecao, perfil.Perfil, reclassificacao, perfil.De(Marlene));
        await LiberarAsync(Marlene, EtapaProtecao, perfil.Perfil, reclassificacao);

        await ConferirRecusaDaReclassificacaoAsync(propria, HttpStatusCode.UnprocessableEntity, "decisor_e_beneficiario");
        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().Equal(Sensivel);
        (await PermissoesAsync(Marlene, perfil.Perfil)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A mesma pessoa não decide as duas etapas da mesma reclassificação")]
    public async Task Segregacao_MesmaPessoaNasDuasEtapas()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro);
        await ImplantarAsync(Sensivel, ClassificacaoDeProtecao.Interno, deUnidade: false);
        await ImplantarRealAsync();
        Guid reclassificacao = await ReclassificacaoAsync(perfil.Perfil, Sensivel);
        await LiberarAsync(Isabella, EtapaProtecao, perfil.Perfil, reclassificacao);

        using HttpResponseMessage segunda = await DecidirAsync(Isabella, EtapaGestao, perfil.Perfil, reclassificacao, perfil.De(Leandro));

        await ConferirRecusaDaReclassificacaoAsync(segunda, HttpStatusCode.UnprocessableEntity, "mesmo_decisor_nas_duas_etapas");
    }

    [Fact(DisplayName = "No lote, a concessão cuja outra etapa o decisor decidiu recusa o lote inteiro e nenhuma concessão é liberada")]
    public async Task Segregacao_LoteComConcessaoDecididaNaOutraEtapa_RecusaOLote()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro, Betina);
        await ImplantarAsync(Sensivel, ClassificacaoDeProtecao.Interno, deUnidade: false);
        await ImplantarRealAsync();
        Guid reclassificacao = await ReclassificacaoAsync(perfil.Perfil, Sensivel);
        await LiberarAsync(Isabella, EtapaProtecao, perfil.Perfil, reclassificacao, perfil.De(Leandro));

        using HttpResponseMessage lote = await DecidirAsync(Isabella, EtapaGestao, perfil.Perfil, reclassificacao);

        await ConferirRecusaDaReclassificacaoAsync(lote, HttpStatusCode.UnprocessableEntity, "mesmo_decisor_nas_duas_etapas");
        (await LiberacoesAsync(reclassificacao)).Should().Be(1);
        (await PermissoesAsync(Betina, perfil.Perfil)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem a permissão da etapa, o id que existe e o que não existe recebem a mesma recusa, no detalhe e na decisão")]
    public async Task Acesso_RecusaIgualParaOIdQueExisteEOQueNaoExiste()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro);
        await SubirAClasseAsync();
        Guid existente = await ReclassificacaoAsync(perfil.Perfil, Sensivel);
        using HttpClient semPermissao = Cliente(Betina);
        object corpo = LiberarTodasNaProtecao;

        foreach (Guid id in new[] { existente, Guid.NewGuid() })
        {
            using HttpResponseMessage detalhe = await semPermissao.GetAsync(new Uri($"{RotaReclassificacoes}/{id}", UriKind.Relative));
            using HttpResponseMessage decisao = await EnviarAsync(semPermissao, HttpMethod.Post, $"{RotaReclassificacoes}/{id}/decisoes", corpo);

            await ConferirRecusaDaReclassificacaoAsync(detalhe, HttpStatusCode.Forbidden, "sem_permissao_na_etapa");
            await ConferirRecusaDaReclassificacaoAsync(decisao, HttpStatusCode.Forbidden, "sem_permissao_na_etapa");
        }
    }

    [Fact(DisplayName = "Quem tem só a gestão de acessos é recusado na etapa de proteção de dados da subida de classe")]
    public async Task Acesso_SoAGestao_RecusadaNaEtapaDeProtecao()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro);
        await SubirAClasseAsync();
        Guid reclassificacao = await ReclassificacaoAsync(perfil.Perfil, Sensivel);
        using HttpClient gestao = await GestorAsync();
        await LerODocumentoAsync(perfil.Perfil, Diogo);

        using HttpResponseMessage resposta = await EnviarAsync(
            gestao, HttpMethod.Post, $"{RotaReclassificacoes}/{reclassificacao}/decisoes", LiberarTodasNaProtecao);

        await ConferirRecusaDaReclassificacaoAsync(resposta, HttpStatusCode.Forbidden, "sem_permissao_na_etapa");
        (await LiberacoesAsync(reclassificacao)).Should().Be(0);
        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A trilha da recusa registra a reclassificação recebida como recurso, igual para o id que existe e o que não existe")]
    public async Task Trilha_DaRecusa_RegistraOIdRecebido()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro);
        await SubirAClasseAsync();
        Guid existente = await ReclassificacaoAsync(perfil.Perfil, Sensivel);
        using HttpClient semPermissao = Cliente(Betina);

        foreach (Guid id in new[] { existente, Guid.NewGuid() })
        {
            await ConferirTrilhaDaRecusaAsync(semPermissao, id);
        }
    }

    [Fact(DisplayName = "A trilha da decisão permitida registra a reclassificação decidida como recurso")]
    public async Task Trilha_DaPermitida_RegistraAReclassificacao()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro);
        await SubirAClasseAsync();
        Guid reclassificacao = await ReclassificacaoAsync(perfil.Perfil, Sensivel);
        int antes = Registro.Registrados.Count;

        await LiberarAsync(Marlene, EtapaProtecao, perfil.Perfil, reclassificacao);

        Registro.Registrados.Skip(antes).Where(r => r.AtorSubject == Marlene.Sub.ToString() && r.ResourceTipo == "reclassificacao-de-permissao")
            .Should().ContainSingle(r => r.ResourceId == reclassificacao && r.Resultado == ResultadoDoAcesso.Permitido && r.Permissao == "autorizacao:perfis:aprovar-protecao-de-dados");
    }

    [Fact(DisplayName = "Quem não leu o documento instituidor não decide a etapa")]
    public async Task Decisao_ExigeALeituraDoDocumento()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro);
        await SubirAClasseAsync();
        Guid reclassificacao = await ReclassificacaoAsync(perfil.Perfil, Sensivel);
        using HttpClient protecao = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);

        using HttpResponseMessage resposta = await EnviarAsync(
            protecao, HttpMethod.Post, $"{RotaReclassificacoes}/{reclassificacao}/decisoes", LiberarTodasNaProtecao);

        await ConferirRecusaDaReclassificacaoAsync(resposta, HttpStatusCode.UnprocessableEntity, "documento_nao_lido");
    }

    /// <summary>Tenta decidir a reclassificação sem a permissão da etapa e confere que a trilha registra só a negação com o id recebido.</summary>
    private async Task ConferirTrilhaDaRecusaAsync(HttpClient semPermissao, Guid id)
    {
        int antes = Registro.Registrados.Count;
        using HttpResponseMessage resposta = await EnviarAsync(
            semPermissao, HttpMethod.Post, $"{RotaReclassificacoes}/{id}/decisoes", LiberarTodasNaProtecao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        RegistroDecisaoAcesso[] registrados = [.. Registro.Registrados.Skip(antes).Where(r => r.AtorSubject == Betina.Sub.ToString())];
        registrados.Should().NotBeEmpty();
        registrados.Should().OnlyContain(r => r.ResourceTipo == "reclassificacao-de-permissao" && r.ResourceId == id && r.Resultado == ResultadoDoAcesso.Negado);
    }

    /// <summary>Recua a concessão uma hora e fecha a vigência meia hora depois, como a que venceu sem ser revogada.</summary>
    private Task VencerAsync(Guid concessao) =>
        ExecutarSqlAsync(
            "UPDATE autorizacao.concessoes SET concedida_em = concedida_em - interval '1 hour', valida_ate = concedida_em - interval '30 minutes' WHERE id = {0}",
            concessao);
}

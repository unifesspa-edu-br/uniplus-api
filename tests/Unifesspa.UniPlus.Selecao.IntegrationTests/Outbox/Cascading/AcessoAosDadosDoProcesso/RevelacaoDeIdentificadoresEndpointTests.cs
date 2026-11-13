namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A revelação do valor de um campo identificador de uma inscrição enviada, decidida pela permissão de revelar na unidade
/// dona do processo e pela de consultar o identificador mascarado: o valor completo sem cache, o campo de grupo
/// repetível pela ocorrência, a recusa que responde 404 sem entregar o valor, o impedimento da servidora inscrita e o
/// registro de cada decisão com o código do fato.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class RevelacaoDeIdentificadoresEndpointTests : IAsyncLifetime
{
    private readonly ConcessoesDasPersonas _concessoes;
    private readonly RequisicoesAutenticadas _http;
    private readonly DecisoesDeAcessoRegistradas _decisoes;
    private readonly SemeadorDoCenarioDeAcesso _cenarios;

    public RevelacaoDeIdentificadoresEndpointTests(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _concessoes = new ConcessoesDasPersonas(fixture);
        _http = new RequisicoesAutenticadas(fixture);
        _decisoes = new DecisoesDeAcessoRegistradas(fixture);
        _cenarios = new SemeadorDoCenarioDeAcesso(fixture);
    }

    public Task InitializeAsync() => _concessoes.RemoverDeTodasAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Revelar o CPF só com a permissão de revelar, sem a do identificador mascarado, responde 404, registra só a negativa da classe e não entrega o valor")]
    public async Task Revelacao_SemAPermissaoDaClasse_404ESoANegativaDaClasse()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Revelacao_SemAPermissaoDaClasse_404ESoANegativaDaClasse));
        await _concessoes.ConcederAsync(PersonasDoAcesso.Betina, cenario.Processo.UnidadeId, PermissaoSelecao.DadosPessoaisConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);

        using HttpResponseMessage resposta = await RevelarAsync(PersonasDoAcesso.Betina, cenario, cenario.InscricaoId, ConjuntoBasicoDaInscricao.Cpf);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor);
        _decisoes.DaRevelacao(cenario, cenario.InscricaoId).Should().BeEmpty("nada foi revelado");
        _decisoes.DoAtor(PersonasDoAcesso.Betina)
            .Where(r => r.ProcessoId == cenario.Processo.Id && r.InscricaoId == cenario.InscricaoId
                && r.Permissao == PermissaoSelecao.DadosIdentificadoresConsultar.Codigo)
            .Should().ContainSingle().Which.Permitido.Should().BeFalse();
    }

    [Fact(DisplayName = "Diogo revela o passaporte de Erick: recebe o número, sem cache, e fica registrado que revelou o campo daquela inscrição")]
    public async Task Revelacao_DoPassaporte_RevelaERegistra()
    {
        CenarioDeAcessoAosDados cenario = await SemearComPermissaoDeRevelarAsync(nameof(Revelacao_DoPassaporte_RevelaERegistra));

        using HttpResponseMessage resposta = await RevelarAsync(PersonasDoAcesso.Diogo, cenario, cenario.InscricaoDeErick, FatosDoCenario.Identificador);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        resposta.Headers.CacheControl!.NoStore.Should().BeTrue();
        using JsonDocument json = JsonDocument.Parse(corpo);
        json.RootElement.GetProperty("fatoCodigo").GetString().Should().Be(FatosDoCenario.Identificador);
        json.RootElement.GetProperty("valor").GetString().Should().Be(FatosDoCenario.PassaporteDeErick);
        ConferirRegistroDaRevelacaoDoPassaporte(cenario);
    }

    [Fact(DisplayName = "O campo dentro de grupo repetível se revela pela ocorrência informada: o valor é o da ocorrência e o registro nomeia o identificador dela")]
    public async Task Revelacao_DeCampoEmGrupo_PelaOcorrencia()
    {
        CenarioDeAcessoAosDados cenario = await SemearComPermissaoDeRevelarAsync(nameof(Revelacao_DeCampoEmGrupo_PelaOcorrencia));

        using HttpResponseMessage resposta = await RevelarAsync(
            PersonasDoAcesso.Diogo, cenario, cenario.InscricaoComGrupo, cenario.Processo.FatoDoGrupo, "membro-2");
        using HttpResponseMessage semOcorrencia = await RevelarAsync(PersonasDoAcesso.Diogo, cenario, cenario.InscricaoComGrupo, cenario.Processo.FatoDoGrupo);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        json.RootElement.GetProperty("valor").GetString().Should().Be(FatosDoCenario.RgDoSegundoResponsavel);
        semOcorrencia.StatusCode.Should().Be(HttpStatusCode.NotFound, "o campo em grupo exige a ocorrência");
        ConferirRegistroDaOcorrencia(cenario);
    }

    [Fact(DisplayName = "O CPF de Fernanda se revela pela mesma operação, com o valor completo")]
    public async Task Revelacao_DoCpf_UsaAMesmaOperacao()
    {
        CenarioDeAcessoAosDados cenario = await SemearComPermissaoDeRevelarAsync(nameof(Revelacao_DoCpf_UsaAMesmaOperacao));

        using HttpResponseMessage resposta = await RevelarAsync(PersonasDoAcesso.Diogo, cenario, cenario.InscricaoId, ConjuntoBasicoDaInscricao.Cpf);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        json.RootElement.GetProperty("valor").GetString().Should().Be(PersonasDeTeste.FernandaDrumond.Formatado);
    }

    [Fact(DisplayName = "Revelação recusada: Kevin sem a permissão recebe 404 e fica registrada a negativa com o código do fato")]
    public async Task Revelacao_SemAPermissao_404ERegistraANegativa()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Revelacao_SemAPermissao_404ERegistraANegativa));
        await _concessoes.ConcederAsync(PersonasDoAcesso.Leandro, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosIdentificadoresConsultar);

        using HttpResponseMessage resposta = await RevelarAsync(PersonasDoAcesso.Leandro, cenario, cenario.InscricaoId, ConjuntoBasicoDaInscricao.Cpf);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor);
        RegistroDecisaoAcesso negativa = _decisoes.DaRevelacao(cenario, cenario.InscricaoId).Should().ContainSingle().Subject;
        negativa.Permitido.Should().BeFalse();
        negativa.FatoCodigo.Should().Be(ConjuntoBasicoDaInscricao.Cpf);
    }

    [Fact(DisplayName = "Revelação de inscrição que não existe, por quem não tem a permissão de revelar: 404 e negativa registrada sem o id da inscrição")]
    public async Task Revelacao_InscricaoInexistenteSemAPermissao_RegistraANegativaSemId()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Revelacao_InscricaoInexistenteSemAPermissao_RegistraANegativaSemId));
        await _concessoes.ConcederAsync(PersonasDoAcesso.Leandro, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosIdentificadoresConsultar);

        using HttpResponseMessage resposta = await RevelarAsync(PersonasDoAcesso.Leandro, cenario, Guid.CreateVersion7(), ConjuntoBasicoDaInscricao.Cpf);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        RegistroDecisaoAcesso negativa = NegativaDeRevelar(PersonasDoAcesso.Leandro, cenario);
        negativa.Permitido.Should().BeFalse();
        negativa.ResourceId.Should().BeNull();
        negativa.InscricaoId.Should().BeNull();
        negativa.FatoCodigo.Should().BeNull();
    }

    [Fact(DisplayName = "A permissão em outra unidade, a inscrição de outro processo, o rascunho e o campo que não é identificador respondem o mesmo 404")]
    public async Task Revelacao_ForaDoAlcance_404()
    {
        CenarioDeAcessoAosDados cenario = await SemearComPermissaoDeRevelarAsync(nameof(Revelacao_ForaDoAlcance_404));
        CenarioDeAcessoAosDados outroProcesso = await _cenarios.SemearAsync(nameof(Revelacao_ForaDoAlcance_404) + "-outro");
        await _concessoes.ConcederAsync(PersonasDoAcesso.Betina, Guid.CreateVersion7(), PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);

        using HttpResponseMessage outraUnidade = await RevelarAsync(PersonasDoAcesso.Betina, cenario, cenario.InscricaoId, ConjuntoBasicoDaInscricao.Cpf);
        using HttpResponseMessage deOutroProcesso = await RevelarAsync(PersonasDoAcesso.Diogo, cenario, outroProcesso.InscricaoId, ConjuntoBasicoDaInscricao.Cpf);
        using HttpResponseMessage emRascunho = await RevelarAsync(PersonasDoAcesso.Diogo, cenario, cenario.Rascunho.Id, ConjuntoBasicoDaInscricao.Cpf);
        using HttpResponseMessage naoIdentificador = await RevelarAsync(PersonasDoAcesso.Diogo, cenario, cenario.InscricaoId, ConjuntoBasicoDaInscricao.Nome);

        ConferirTodasNaoEncontradas(outraUnidade, deOutroProcesso, emRascunho, naoIdentificador);
        ConferirTrilhaDaRevelacaoForaDoAlcance(cenario);
    }

    [Fact(DisplayName = "A servidora inscrita no processo, ainda que com a permissão de revelar, responde 403 impedida")]
    public async Task Revelacao_ServidoraInscrita_Impedida()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Revelacao_ServidoraInscrita_Impedida));
        await _concessoes.ConcederAsync(PersonasDoAcesso.Caroline, cenario.Processo.UnidadeId, PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);

        using HttpResponseMessage resposta = await RevelarAsync(PersonasDoAcesso.Caroline, cenario, cenario.InscricaoId, ConjuntoBasicoDaInscricao.Cpf);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<CenarioDeAcessoAosDados> SemearComPermissaoDeRevelarAsync(string nome)
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nome);
        await _concessoes.ConcederAsync(PersonasDoAcesso.Diogo, cenario.Processo.UnidadeId, PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);
        return cenario;
    }

    private Task<HttpResponseMessage> RevelarAsync(
        string persona, CenarioDeAcessoAosDados cenario, Guid inscricaoId, string fatoCodigo, string? ocorrencia = null) =>
        _http.SendAsync(persona, HttpMethod.Post, cenario.RotaDaRevelacao(inscricaoId, fatoCodigo, ocorrencia), null);

    private RegistroDecisaoAcesso NegativaDeRevelar(string persona, CenarioDeAcessoAosDados cenario) =>
        _decisoes.DoAtor(persona)
            .Where(r => r.ProcessoId == cenario.Processo.Id && r.Permissao == PermissaoSelecao.DadosIdentificadoresRevelar.Codigo)
            .Should().ContainSingle().Subject;

    private void ConferirRegistroDaRevelacaoDoPassaporte(CenarioDeAcessoAosDados cenario)
    {
        RegistroDecisaoAcesso decisao = _decisoes.DaRevelacao(cenario, cenario.InscricaoDeErick).Should().ContainSingle().Subject;
        decisao.Permitido.Should().BeTrue();
        decisao.Permissao.Should().Be(PermissaoSelecao.DadosIdentificadoresRevelar.Codigo);
        decisao.AtorSubject.Should().Be(PersonasDoAcesso.Diogo);
        decisao.FatoCodigo.Should().Be(FatosDoCenario.Identificador);
        decisao.InscricaoId.Should().Be(cenario.InscricaoDeErick);
        decisao.GrantId.Should().NotBeNull();
        JsonSerializer.Serialize(decisao).Should().NotContain(FatosDoCenario.PassaporteDeErick);
    }

    private void ConferirRegistroDaOcorrencia(CenarioDeAcessoAosDados cenario)
    {
        RegistroDecisaoAcesso decisao = _decisoes.DaRevelacao(cenario, cenario.InscricaoComGrupo)
            .Should().ContainSingle(r => r.Ocorrencia == "membro-2").Subject;
        decisao.Permitido.Should().BeTrue();
        decisao.FatoCodigo.Should().Be(cenario.Processo.FatoDoGrupo);
        decisao.Ocorrencia.Should().Be("membro-2");
    }

    private static void ConferirTodasNaoEncontradas(params HttpResponseMessage[] recusas)
    {
        foreach (HttpResponseMessage recusa in recusas)
        {
            recusa.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    private void ConferirTrilhaDaRevelacaoForaDoAlcance(CenarioDeAcessoAosDados cenario)
    {
        _decisoes.DaRevelacao(cenario, cenario.Rascunho.Id).Should().BeEmpty("a trilha nunca nomeia inscrição de outro processo nem em rascunho");
        _decisoes.DaRevelacao(cenario, cenario.InscricaoId).Should().Contain(
            r => r.FatoCodigo == ConjuntoBasicoDaInscricao.Nome, "a decisão de revelar vem antes de procurar a resposta, ainda que o campo não seja identificador");
    }
}

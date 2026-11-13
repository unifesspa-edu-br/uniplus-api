namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.ConsultaAdministrativaDeTeste;

/// <summary>
/// O primeiro nível da consulta administrativa da inscrição (ADR-0153): o acesso ao resource é decidido antes de qualquer
/// leitura do conteúdo, e a inscrição que a pessoa não alcança responde o mesmo 404 do que não existe.
/// </summary>
public sealed class ObterInscricaoAdministrativaAcessoAoResourceTests
{
    private const string NaoEncontrada = "Inscricao.NaoEncontrada";

    [Fact(DisplayName = "Inscrição de outro processo, inexistente ou em rascunho, pedida por quem tem a permissão da rota, responde 404 sem registro nem leitura do conteúdo")]
    public async Task ForaDoAlcance_404SemDecisao()
    {
        InscricaoAdministrativaDeTeste rascunho = DeFernanda(SituacaoInscricao.Rascunho);
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        InscricaoAdministrativaDeTeste.Mundo mundo = rascunho.Montar();

        Result<InscricaoAdministrativaDto>[] recusas = await PedirForaDoAlcanceAsync(rascunho, mundo, diogo);

        recusas.Should().AllSatisfy(static r => r.Error!.Code.Should().Be(NaoEncontrada));
        await diogo.NaoExigiuNadaAsync();
        await diogo.NaoRegistrouNadaAsync();
        await mundo.Inscricoes.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    [Fact(DisplayName = "Inscrição que não existe, pedida sem a permissão da rota, responde 404 e registra a negativa sem o id do resource")]
    public async Task Inexistente_SemAPermissaoDaRota_RegistraANegativaSemId()
    {
        InscricaoAdministrativaDeTeste cenario = DeFernanda();
        DecisoesDeAcessoDeTeste kevin = new("kevin.carvalho", PermissaoSelecao.DadosPessoaisConsultar);

        Result<InscricaoAdministrativaDto> resultado = await Executar(cenario.Montar(), kevin, cenario.ProcessoId, Guid.CreateVersion7());

        resultado.Error!.Code.Should().Be(NaoEncontrada);
        DecisaoNegada negada = kevin.DecisoesRegistradas().Should().ContainSingle().Which.Should().BeOfType<DecisaoNegada>().Subject;
        negada.Registro.Permissao.Should().Be(CodigoAcesso);
        negada.Registro.ResourceId.Should().BeNull();
        negada.Registro.MotivoNegativa.Should().Be("sem_concessao_aplicavel");
    }

    [Fact(DisplayName = "Processo da rota que não existe responde 404 sem decidir nem registrar")]
    public async Task ProcessoInexistente_404SemRegistro()
    {
        InscricaoAdministrativaDeTeste cenario = DeFernanda();
        DecisoesDeAcessoDeTeste kevin = new("kevin.carvalho", PermissaoSelecao.DadosPessoaisConsultar);

        Result<InscricaoAdministrativaDto> resultado = await Executar(cenario.Montar(), kevin, Guid.CreateVersion7(), cenario.InscricaoId);

        resultado.Error!.Code.Should().Be(NaoEncontrada);
        await kevin.NaoAvaliouNadaAsync();
        await kevin.NaoRegistrouNadaAsync();
    }

    [Fact(DisplayName = "Sem a permissão de acesso à inscrição responde 404 e nada do conteúdo é carregado nem decidido por classe")]
    public async Task SemAcessoAoResource_404SemCarregarOConteudo()
    {
        InscricaoAdministrativaDeTeste cenario = DeFernanda();
        DecisoesDeAcessoDeTeste kevin = new("kevin.carvalho", PermissaoSelecao.DadosPessoaisConsultar);
        InscricaoAdministrativaDeTeste.Mundo mundo = cenario.Montar();

        Result<InscricaoAdministrativaDto> resultado = await Executar(mundo, kevin, cenario.ProcessoId, cenario.InscricaoId);

        resultado.Error!.Code.Should().Be(NaoEncontrada);
        await mundo.Inscricoes.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await kevin.NaoAvaliouNadaAsync();
    }

    [Fact(DisplayName = "A inscrição que some entre a conferência do vínculo e a leitura responde 404 sem consultar a permissão de emitir o comprovante")]
    public async Task InscricaoQueSome_404SemConsultarAPermissaoDeEmitir()
    {
        InscricaoAdministrativaDeTeste cenario = DeFernanda();
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.InscricoesEmitirComprovante);
        InscricaoAdministrativaDeTeste.Mundo mundo = cenario.Montar();
        mundo.Inscricoes.ObterPorIdAsync(cenario.InscricaoId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Inscricao?>(null));

        Result<InscricaoAdministrativaDto> resultado = await Executar(mundo, diogo, cenario.ProcessoId, cenario.InscricaoId);

        resultado.Error!.Code.Should().Be(NaoEncontrada);
        await diogo.NaoConsultouACapacidadeAsync(CodigoEmitir);
    }

    [Fact(DisplayName = "Falha ao registrar o acesso recusa a consulta: a inscrição não sai")]
    public async Task FalhaAoRegistrar_RecusaAConsulta()
    {
        InscricaoAdministrativaDeTeste cenario = DeFernanda();
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        diogo.Verificador.RegistrarDesfechoAsync(Arg.Any<IReadOnlyCollection<DecisaoDeAcesso>>(), Arg.Any<IntencaoDoRegistro>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new RegistroDeAcessoNaoGravadoException(new InvalidOperationException("broker"))));

        Func<Task> consultar = () => Consultar(cenario, diogo);

        await consultar.Should().ThrowAsync<RegistroDeAcessoNaoGravadoException>();
    }

    private static async Task<Result<InscricaoAdministrativaDto>[]> PedirForaDoAlcanceAsync(
        InscricaoAdministrativaDeTeste rascunho, InscricaoAdministrativaDeTeste.Mundo mundo, DecisoesDeAcessoDeTeste persona) =>
    [
        await Executar(mundo, persona, rascunho.ProcessoId, rascunho.InscricaoId),
        await Executar(mundo, persona, Guid.CreateVersion7(), rascunho.InscricaoId),
        await Executar(mundo, persona, rascunho.ProcessoId, Guid.CreateVersion7()),
    ];
}

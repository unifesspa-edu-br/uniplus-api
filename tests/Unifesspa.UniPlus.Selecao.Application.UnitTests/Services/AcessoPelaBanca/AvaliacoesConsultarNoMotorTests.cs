namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.AcessoPelaBanca;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.AcessoPelaBanca;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// A permissão de consultar avaliações, decidida pelo motor real sobre o resource que a Seleção monta: a declaração da
/// permissão (base legal, contexto exigido, verificação de designação) e a fábrica do resource têm de se encaixar.
/// </summary>
public sealed class AvaliacoesConsultarNoMotorTests
{
    private readonly UnidadeDaFase _fase = new(Guid.CreateVersion7(), Guid.CreateVersion7());
    private readonly Guid _processo = Guid.CreateVersion7();
    private readonly Guid _etapa = Guid.CreateVersion7();
    private readonly Guid _banca = Guid.CreateVersion7();
    private readonly Guid _inscricao = Guid.CreateVersion7();

    [Fact(DisplayName = "Com a concessão na unidade da fase e a designação à banca, o motor concede")]
    public async Task ConcessaoEDesignacao_Concede()
    {
        AuthorizationDecision decisao = await Decidir(designada: true, Avaliacao());

        decisao.Allowed.Should().BeTrue();
    }

    [Fact(DisplayName = "Com a concessão e sem a designação, o motor recusa por fora_da_designacao")]
    public async Task SemDesignacao_ForaDaDesignacao()
    {
        AuthorizationDecision decisao = await Decidir(designada: false, Avaliacao());

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.ForaDaDesignacao);
    }

    [Fact(DisplayName = "O resource da banca da fase, sem etapa, não satisfaz o contexto da avaliação")]
    public async Task ResourceSemEtapa_ContextoObrigatorioAusente()
    {
        ResourceContext semEtapa = AcessoAoProcesso.BancaDaFaseNaInscricao(_fase, _processo, _banca, _inscricao).Value!;

        AuthorizationDecision decisao = await Decidir(designada: true, semEtapa);

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.ContextoObrigatorioAusente);
    }

    [Fact(DisplayName = "O resource da banca da fase sem inscrição não satisfaz o contexto da avaliação")]
    public async Task ResourceSemInscricao_ContextoObrigatorioAusente()
    {
        ResourceContext semInscricao = ResourceContext.From(
                ResourceTiposSelecao.AvaliacaoDaInscricao, ClassificacaoDeProtecao.Pessoal, _fase.UnidadeId, _processo).Value!
            .ComVinculosDaBanca(VinculosDaBanca.From(_fase.FaseId, _banca, _etapa).Value!);

        AuthorizationDecision decisao = await Decidir(designada: true, semInscricao);

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.ContextoObrigatorioAusente);
    }

    private ResourceContext Avaliacao() =>
        AcessoAoProcesso.AvaliacaoDaInscricao(_fase, _processo, _etapa, _banca, _inscricao).Value!;

    private Task<AuthorizationDecision> Decidir(bool designada, ResourceContext resource) =>
        Motor(designada).DecidirAsync(
            Sujeito(),
            PermissaoSelecao.AvaliacoesConsultar.Requisito,
            resource,
            AuthorizationRequestContext.From("req-1", DateTimeOffset.UtcNow, OrigemRequisicao.Api).Value!,
            CancellationToken.None);

    private AuthorizationSubject Sujeito()
    {
        EffectiveGrant concessao = EffectiveGrant.From(
            PermissaoSelecao.AvaliacoesConsultar.Codigo, FonteGrant.Concessao, escopoUnidadeId: _fase.UnidadeId).Value!;

        return AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "sub-banca").Value!, "jti", concessoesEfetivas: [concessao]).Value!;
    }

    private static AuthorizationDecisionService Motor(bool designada)
    {
        IDesignacaoDeBanca designacoes = Substitute.For<IDesignacaoDeBanca>();
        designacoes.TemDesignacaoVigenteAsync(default!, default!, default, default!, default).ReturnsForAnyArgs(designada);

        return new AuthorizationDecisionService(
            new GrantEfetivoAplicavelCheck(),
            new ImpedimentoPorInscricaoCheck(Substitute.For<IInscricaoDoSujeitoNoProcesso>()),
            [new DesignacaoNaBancaCheck(designacoes)]);
    }
}

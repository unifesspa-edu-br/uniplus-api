namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A decisão sem alvo próprio decide sobre o tipo fixo <c>operacao</c>, e não sobre o código da permissão.
/// </summary>
public sealed class VerificadorDeAcessoHttpTests
{
    private const string Permissao = "selecao:inscricoes:consultar";

    private static readonly Guid Unidade = Guid.CreateVersion7();

    private readonly IAuthorizationDecisionService _decisao = Substitute.For<IAuthorizationDecisionService>();
    private readonly IRegistroOperacionalRestrito _registro = Substitute.For<IRegistroOperacionalRestrito>();
    private readonly VerificadorDeAcessoHttp _verificador;

    public VerificadorDeAcessoHttpTests()
    {
        EffectiveGrant concessao = EffectiveGrant.From(Permissao, FonteGrant.Concessao, escopoUnidadeId: Unidade).Value!;
        AuthorizationSubject sujeito = AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "sub-opaco-123").Value!,
            "jti-abc",
            concessoesEfetivas: [concessao]).Value!;

        IAuthorizationSubjectResolver resolver = Substitute.For<IAuthorizationSubjectResolver>();
        resolver.ResolveAsync(Arg.Any<IUserContext>(), Arg.Any<AuthorizationRequestContext>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthorizationSubject>.Success(sujeito));

        ICorrelationIdAccessor correlacao = Substitute.For<ICorrelationIdAccessor>();
        correlacao.CorrelationId.Returns("req-1244");

        _decisao.DecideAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(AuthorizationDecision.Permitir(concessao));
        _decisao.DecidirSemRegistrarAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(AuthorizationDecision.Permitir(concessao));

        _verificador = new VerificadorDeAcessoHttp(
            Substitute.For<IUserContext>(),
            resolver,
            _decisao,
            _registro,
            Substitute.For<IHttpContextAccessor>(),
            correlacao,
            TimeProvider.System);
    }

    [Fact(DisplayName = "Sem resource informado, a decisão recai sobre o tipo operacao e não sobre o código da permissão")]
    public async Task Verificar_SemResource_DecideSobreOTipoOperacao()
    {
        await _verificador.VerificarAsync(Requisito());

        await _decisao.Received(1).DecideAsync(
            Arg.Any<AuthorizationSubject>(),
            Arg.Any<PermissionRequirement>(),
            Arg.Is<ResourceContext>(r => r.ResourceTipo == ResourceTipos.Operacao && r.UnidadeProprietariaId == null),
            Arg.Any<AuthorizationRequestContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A decisão por unidade recai sobre o tipo operacao, com a unidade da concessão")]
    public async Task VerificarEmAlgumaUnidade_DecideSobreOTipoOperacaoNaUnidade()
    {
        await _verificador.VerificarEmAlgumaUnidadeAsync(Requisito());

        await _decisao.Received(1).DecidirSemRegistrarAsync(
            Arg.Any<AuthorizationSubject>(),
            Arg.Any<PermissionRequirement>(),
            Arg.Is<ResourceContext>(r => r.ResourceTipo == ResourceTipos.Operacao && r.UnidadeProprietariaId == Unidade),
            Arg.Any<AuthorizationRequestContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Em alguma unidade, a permitida é a única registrada, num lote só")]
    public async Task VerificarEmAlgumaUnidade_Permitida_RegistraSoAPermitida()
    {
        ResultadoDoAcesso resultado = await _verificador.VerificarEmAlgumaUnidadeAsync(RequisitoPessoal());

        resultado.Should().Be(ResultadoDoAcesso.Permitido);
        await _registro.Received(1).RegistrarAsync(
            Arg.Is<IReadOnlyList<RegistroDecisaoAcesso>>(l => l.Count == 1 && l[0].Permitido && l[0].UnidadeProprietariaId == Unidade),
            Arg.Any<CancellationToken>());
        await _decisao.DidNotReceiveWithAnyArgs().DecideAsync(default!, default!, default!, default!, default);
    }

    [Fact(DisplayName = "Em alguma unidade, sem nenhuma permitida, as negativas vão todas num lote só")]
    public async Task VerificarEmAlgumaUnidade_NenhumaPermitida_RegistraAsNegativasEmLote()
    {
        _decisao.DecidirSemRegistrarAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel));

        ResultadoDoAcesso resultado = await _verificador.VerificarEmAlgumaUnidadeAsync(RequisitoPessoal());

        resultado.Should().Be(ResultadoDoAcesso.Negado);
        await _registro.Received(1).RegistrarAsync(
            Arg.Is<IReadOnlyList<RegistroDecisaoAcesso>>(l => l.Count == 1 && !l[0].Permitido),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Em alguma unidade, sem concessão da permissão em unidade alguma, a negativa é registrada sem unidade")]
    public async Task VerificarEmAlgumaUnidade_SemConcessao_RegistraANegativa()
    {
        PermissionRequirement semConcessao = PermissionRequirement.From(
            "selecao:outra:coisa", ClassificacaoDeProtecao.Pessoal, "Lei nº 13.709/2018 (LGPD), art. 7º, III").Value!;

        ResultadoDoAcesso resultado = await _verificador.VerificarEmAlgumaUnidadeAsync(semConcessao);

        resultado.Should().Be(ResultadoDoAcesso.Negado);
        await _registro.Received(1).RegistrarAsync(
            Arg.Is<IReadOnlyList<RegistroDecisaoAcesso>>(l =>
                l.Count == 1 && !l[0].Permitido && l[0].UnidadeProprietariaId == null && l[0].MotivoNegativa == "sem_concessao_aplicavel"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Em alguma unidade, com concessão só global, grava o que o ponto de decisão devolveu, e não afirma que não há concessão")]
    public async Task VerificarEmAlgumaUnidade_SoConcessaoGlobal_RegistraADecisaoDoPontoDeDecisao()
    {
        EffectiveGrant global = EffectiveGrant.From(Permissao, FonteGrant.Concessao, escopoInstitucional: true).Value!;
        AuthorizationSubject sujeito = AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "sub-opaco-123").Value!, "jti-abc", concessoesEfetivas: [global]).Value!;
        IAuthorizationSubjectResolver resolver = Substitute.For<IAuthorizationSubjectResolver>();
        resolver.ResolveAsync(Arg.Any<IUserContext>(), Arg.Any<AuthorizationRequestContext>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthorizationSubject>.Success(sujeito));
        ICorrelationIdAccessor correlacao = Substitute.For<ICorrelationIdAccessor>();
        correlacao.CorrelationId.Returns("req-1244");
        IAuthorizationDecisionService decisao = Substitute.For<IAuthorizationDecisionService>();
        decisao.DecidirSemRegistrarAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(AuthorizationDecision.Negar(MotivoNegativa.ContextoObrigatorioAusente));
        IRegistroOperacionalRestrito registro = Substitute.For<IRegistroOperacionalRestrito>();
        VerificadorDeAcessoHttp verificador = new(
            Substitute.For<IUserContext>(), resolver, decisao, registro, Substitute.For<IHttpContextAccessor>(), correlacao, TimeProvider.System);

        ResultadoDoAcesso resultado = await verificador.VerificarEmAlgumaUnidadeAsync(RequisitoPessoal());

        resultado.Should().Be(ResultadoDoAcesso.Negado);
        await registro.Received(1).RegistrarAsync(
            Arg.Is<IReadOnlyList<RegistroDecisaoAcesso>>(l => l.Count == 1 && l[0].MotivoNegativa == "contexto_obrigatorio_ausente"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Em alguma unidade, com concessão só global que o ponto de decisão permitiria, o veredito segue negado e nada é publicado como permitido")]
    public async Task VerificarEmAlgumaUnidade_SoConcessaoGlobalQuePermitiria_NegadoSemRegistro()
    {
        EffectiveGrant global = EffectiveGrant.From(Permissao, FonteGrant.Concessao, escopoInstitucional: true).Value!;
        AuthorizationSubject sujeito = AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "sub-opaco-123").Value!, "jti-abc", concessoesEfetivas: [global]).Value!;
        IAuthorizationSubjectResolver resolver = Substitute.For<IAuthorizationSubjectResolver>();
        resolver.ResolveAsync(Arg.Any<IUserContext>(), Arg.Any<AuthorizationRequestContext>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthorizationSubject>.Success(sujeito));
        ICorrelationIdAccessor correlacao = Substitute.For<ICorrelationIdAccessor>();
        correlacao.CorrelationId.Returns("req-1244");
        IAuthorizationDecisionService decisao = Substitute.For<IAuthorizationDecisionService>();
        decisao.DecidirSemRegistrarAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(AuthorizationDecision.Permitir(global));
        IRegistroOperacionalRestrito registro = Substitute.For<IRegistroOperacionalRestrito>();
        VerificadorDeAcessoHttp verificador = new(
            Substitute.For<IUserContext>(), resolver, decisao, registro, Substitute.For<IHttpContextAccessor>(), correlacao, TimeProvider.System);

        ResultadoDoAcesso resultado = await verificador.VerificarEmAlgumaUnidadeAsync(RequisitoPessoal());

        resultado.Should().Be(ResultadoDoAcesso.Negado);
        await registro.DidNotReceiveWithAnyArgs().RegistrarAsync(default!, default);
    }

    [Fact(DisplayName = "Em alguma unidade, a permissão operacional não publica registro")]
    public async Task VerificarEmAlgumaUnidade_Operacional_NaoRegistra()
    {
        await _verificador.VerificarEmAlgumaUnidadeAsync(Requisito());

        await _registro.DidNotReceiveWithAnyArgs().RegistrarAsync(default!, default);
    }

    [Fact(DisplayName = "A consulta de capacidade responde sem gravar registro algum")]
    public async Task Pode_NaoGravaRegistro()
    {
        ResultadoDoAcesso resultado = await _verificador.PodeAsync(Requisito());

        resultado.Should().Be(ResultadoDoAcesso.Permitido);
        await _registro.DidNotReceiveWithAnyArgs().RegistrarAsync(default!, default);
        await _decisao.DidNotReceiveWithAnyArgs().DecideAsync(default!, default!, default!, default!, default);
    }

    [Fact(DisplayName = "Decidir sem registrar não grava, e registrar a permitida para N inscrições grava um registro por id em um lote")]
    public async Task RegistrarParaCada_GravaUmLotePorId()
    {
        Guid fernanda = Guid.CreateVersion7();
        Guid caroline = Guid.CreateVersion7();
        DecisaoDeAcesso decisao = await _verificador.DecidirSemRegistrarAsync(Requisito());
        await _registro.DidNotReceiveWithAnyArgs().RegistrarAsync(default!, default);

        await _verificador.RegistrarParaCadaAsync(decisao, "inscricao", [fernanda, caroline]);

        await _registro.Received(1).RegistrarAsync(
            Arg.Is<IReadOnlyList<RegistroDecisaoAcesso>>(l => l.Count == 2 && l.All(r => r.ResourceTipo == "inscricao")),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A decisão negada não se registra por esta via")]
    public async Task Registrar_Negada_Recusa()
    {
        _decisao.DecidirSemRegistrarAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel));
        DecisaoDeAcesso negada = await _verificador.DecidirSemRegistrarAsync(Requisito());

        Func<Task> acao = () => _verificador.RegistrarAsync(negada);

        await acao.Should().ThrowAsync<InvalidOperationException>();
        await _registro.DidNotReceiveWithAnyArgs().RegistrarAsync(default!, default);
    }

    private static PermissionRequirement RequisitoPessoal()
        => PermissionRequirement.From(Permissao, ClassificacaoDeProtecao.Pessoal, "Lei nº 13.709/2018 (LGPD), art. 7º, III").Value!;

    private static PermissionRequirement Requisito()
        => PermissionRequirement.From(Permissao, ClassificacaoDeProtecao.Interno).Value!;
}

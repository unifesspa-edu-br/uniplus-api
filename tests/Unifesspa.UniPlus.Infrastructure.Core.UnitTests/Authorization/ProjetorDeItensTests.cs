namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// O projetor decide cada par pela permissão que o módulo declara, omite o que não foi permitido e registra só a decisão
/// permitida, numa gravação (ADR-0153).
/// </summary>
public sealed class ProjetorDeItensTests
{
    private static readonly Guid Unidade = Guid.CreateVersion7();

    private readonly IVerificadorDeAcesso _verificador = Substitute.For<IVerificadorDeAcesso>();
    private readonly DecisoesDaRequisicaoHttp _requisicao;
    private readonly ProjetorDeItens _projetor;
    private readonly ResourceContext _inscricao = ResourceContext.From(
        "inscricao", ClassificacaoDeProtecao.Pessoal, Unidade, Guid.CreateVersion7(), resourceId: Guid.CreateVersion7()).Value!;

    public ProjetorDeItensTests()
    {
        IHttpContextAccessor acessor = Substitute.For<IHttpContextAccessor>();
        acessor.HttpContext.Returns(new DefaultHttpContext());
        _requisicao = new DecisoesDaRequisicaoHttp(acessor);
        _projetor = new ProjetorDeItens(_verificador, _requisicao);
    }

    [Fact(DisplayName = "Kevin com dados pessoais e sem sensíveis: o par negado é omitido e só o permitido é registrado")]
    public async Task Decidir_RegistraSoAPermitida()
    {
        PermissionRequirement pessoais = Requisito("selecao:dados-pessoais:consultar", ClassificacaoDeProtecao.Pessoal);
        PermissionRequirement sensiveis = Requisito("selecao:dados-sensiveis:consultar", ClassificacaoDeProtecao.Sensivel);
        _verificador.DecidirSemRegistrarAsync(pessoais, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(pessoais, permitida: true));
        _verificador.DecidirSemRegistrarAsync(sensiveis, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(sensiveis, permitida: false));

        DecisoesDaProjecao decisoes = await _projetor.DecidirAsync(
            Porta(pessoais, sensiveis), _inscricao, Pares(ClassificacaoDeProtecao.Pessoal, ClassificacaoDeProtecao.Sensivel), default);
        await _projetor.RegistrarAsync(decisoes, default);

        decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal).Should().BeTrue();
        decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Sensivel).Should().BeFalse();
        await _verificador.Received(1).RegistrarAsync(
            Arg.Is<IReadOnlyCollection<DecisaoDeAcesso>>(d => d.Count == 1 && d.Single().Registro!.Permissao == pessoais.Permissao),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Cada par é decidido com a sensibilidade do dado que a permissão libera, e não com a do resource de origem")]
    public async Task Decidir_UsaASensibilidadeDaPermissaoDoPar()
    {
        PermissionRequirement publicos = Requisito("selecao:dados-publicos:consultar", ClassificacaoDeProtecao.Publico);
        _verificador.DecidirSemRegistrarAsync(publicos, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(publicos, permitida: true));

        await _projetor.DecidirAsync(Porta(publicos), _inscricao, Pares(ClassificacaoDeProtecao.Publico), default);

        await _verificador.Received(1).DecidirSemRegistrarAsync(
            publicos, Arg.Is<ResourceContext?>(r => r!.Sensibilidade == ClassificacaoDeProtecao.Publico && r.InscricaoId == _inscricao.InscricaoId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O par sem permissão no módulo não é decidido: ninguém vê o item")]
    public async Task Decidir_ParSemPermissao_NaoDecide()
    {
        DecisoesDaProjecao decisoes = await _projetor.DecidirAsync(
            Porta(), _inscricao, Pares(ClassificacaoDeProtecao.Pessoal), default);

        decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal).Should().BeFalse();
        await _verificador.DidNotReceiveWithAnyArgs().DecidirSemRegistrarAsync(default!, default, default);
    }

    [Fact(DisplayName = "As decisões permitidas ficam na requisição para a serialização defensiva")]
    public async Task Decidir_GuardaNaRequisicao()
    {
        PermissionRequirement publicos = Requisito("selecao:dados-publicos:consultar", ClassificacaoDeProtecao.Publico);
        _verificador.DecidirSemRegistrarAsync(publicos, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(publicos, permitida: true));

        await _projetor.DecidirAsync(Porta(publicos), _inscricao, Pares(ClassificacaoDeProtecao.Publico), default);

        _requisicao.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Publico).Should().BeTrue();
        _requisicao.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal).Should().BeFalse();
    }

    [Fact(DisplayName = "Sem nada a registrar a gravação não acontece")]
    public async Task Registrar_SemPermitidas_NaoGrava()
    {
        await _projetor.RegistrarAsync(DecisoesDaProjecao.Vazia, default);

        await _verificador.DidNotReceiveWithAnyArgs().RegistrarAsync(default(IReadOnlyCollection<DecisaoDeAcesso>)!, default);
    }

    private static IEnumerable<(NaturezaDoItem, ClassificacaoDeProtecao)> Pares(params ClassificacaoDeProtecao[] classes) =>
        classes.Select(static c => (NaturezaDoItem.Dado, c));

    private static PermissionRequirement Requisito(string codigo, ClassificacaoDeProtecao sensibilidade) =>
        PermissionRequirement.From(codigo, sensibilidade).Value!;

    private static IPortaDePermissaoPorPar Porta(params PermissionRequirement[] requisitos)
    {
        IPortaDePermissaoPorPar porta = Substitute.For<IPortaDePermissaoPorPar>();
        porta.Requisito(Arg.Any<NaturezaDoItem>(), Arg.Any<ClassificacaoDeProtecao>())
            .Returns(chamada => requisitos.SingleOrDefault(r => r.Sensibilidade == chamada.ArgAt<ClassificacaoDeProtecao>(1)));
        return porta;
    }

    private DecisaoDeAcesso Decisao(PermissionRequirement requisito, bool permitida)
    {
        EffectiveGrant concessao = EffectiveGrant.From(requisito.Permissao, FonteGrant.Concessao, escopoUnidadeId: Unidade).Value!;
        AuthorizationSubject sujeito = AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "kevin").Value!, "jti", concessoesEfetivas: [concessao]).Value!;
        AuthorizationRequestContext contexto = AuthorizationRequestContext.From(
            "req-1", DateTimeOffset.UtcNow, OrigemRequisicao.Api).Value!;
        AuthorizationDecision decisao = permitida
            ? AuthorizationDecision.Permitir(concessao)
            : AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel);

        return DecisaoDeAcesso.Tomada(
            permitida ? ResultadoDoAcesso.Permitido : ResultadoDoAcesso.Negado,
            RegistroDecisaoAcesso.De(sujeito, requisito, _inscricao, contexto, decisao));
    }
}

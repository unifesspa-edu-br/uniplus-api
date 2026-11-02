namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

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
        _verificador.AvaliarAsync(pessoais, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(pessoais, permitida: true));
        _verificador.AvaliarAsync(sensiveis, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(sensiveis, permitida: false));

        DecisoesDaProjecao decisoes = await _projetor.DecidirAsync(
            Porta(pessoais, sensiveis), _inscricao, Pares(ClassificacaoDeProtecao.Pessoal, ClassificacaoDeProtecao.Sensivel), default);
        await _projetor.RegistrarAsync(decisoes, default);

        decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal).Should().BeTrue();
        decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Sensivel).Should().BeFalse();
        await _verificador.Received(1).RegistrarDesfechoAsync(
            Arg.Is<IReadOnlyCollection<DecisaoDeAcesso>>(d => d.Count == 1 && ((DecisaoPermitida)d.Single()).Registro.Permissao == pessoais.Permissao),
            IntencaoDoRegistro.Projecao,
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "As capacidades avaliadas pelo módulo ficam nas decisões guardadas na requisição, sem virar decisão de par")]
    public async Task Decidir_GuardaAsCapacidadesNaRequisicao()
    {
        (NaturezaDoItem, ClassificacaoDeProtecao) identificador = (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Identificador);

        DecisoesDaProjecao decisoes = await _projetor.DecidirAsync(Porta(), _inscricao, [], [identificador], default);

        decisoes.TemCapacidade(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Identificador).Should().BeTrue();
        decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Identificador).Should().BeFalse();
        _requisicao.TemCapacidade(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Identificador).Should().BeTrue();
    }

    [Fact(DisplayName = "A decisão permitida de dado público também é registrada, ainda que a regra de sensibilidade não a exija")]
    public async Task Registrar_DecisaoPublica_EhRegistrada()
    {
        PermissionRequirement publicos = Requisito("selecao:dados-publicos:consultar", ClassificacaoDeProtecao.Publico);
        _verificador.AvaliarAsync(publicos, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(publicos, permitida: true));

        DecisoesDaProjecao decisoes = await _projetor.DecidirAsync(
            Porta(publicos), _inscricao, Pares(ClassificacaoDeProtecao.Publico), default);
        await _projetor.RegistrarAsync(decisoes, default);

        await _verificador.Received(1).RegistrarDesfechoAsync(
            Arg.Is<IReadOnlyCollection<DecisaoDeAcesso>>(d => d.Count == 1 && ((DecisaoPermitida)d.Single()).Registro.Permissao == publicos.Permissao),
            IntencaoDoRegistro.Projecao,
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Cada par é decidido com a sensibilidade do dado que a permissão libera, e não com a do resource de origem")]
    public async Task Decidir_UsaASensibilidadeDaPermissaoDoPar()
    {
        PermissionRequirement publicos = Requisito("selecao:dados-publicos:consultar", ClassificacaoDeProtecao.Publico);
        _verificador.AvaliarAsync(publicos, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(publicos, permitida: true));

        await _projetor.DecidirAsync(Porta(publicos), _inscricao, Pares(ClassificacaoDeProtecao.Publico), default);

        await _verificador.Received(1).AvaliarAsync(
            publicos, Arg.Is<ResourceContext?>(r => r!.Sensibilidade == ClassificacaoDeProtecao.Publico && r.InscricaoId == _inscricao.InscricaoId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A classificação não resolvida é decidida pela permissão de dado sensível, a mesma que a serialização aplica")]
    public async Task Decidir_ClassificacaoNaoResolvida_VaiComoSensivel()
    {
        PermissionRequirement sensiveis = Requisito("selecao:dados-sensiveis:consultar", ClassificacaoDeProtecao.Sensivel);
        _verificador.AvaliarAsync(sensiveis, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(sensiveis, permitida: true));

        DecisoesDaProjecao decisoes = await _projetor.DecidirAsync(
            Porta(sensiveis), _inscricao, Pares(ClassificacaoDeProtecao.Nenhuma), default);

        decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Nenhuma).Should().BeTrue();
        await _verificador.Received(1).AvaliarAsync(sensiveis, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O par sem permissão no módulo não é decidido: ninguém vê o item")]
    public async Task Decidir_ParSemPermissao_NaoDecide()
    {
        DecisoesDaProjecao decisoes = await _projetor.DecidirAsync(
            Porta(), _inscricao, Pares(ClassificacaoDeProtecao.Pessoal), default);

        decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal).Should().BeFalse();
        await _verificador.DidNotReceiveWithAnyArgs().AvaliarAsync(default!, default, default);
    }

    [Fact(DisplayName = "As decisões permitidas ficam na requisição para a serialização defensiva")]
    public async Task Decidir_GuardaNaRequisicao()
    {
        PermissionRequirement publicos = Requisito("selecao:dados-publicos:consultar", ClassificacaoDeProtecao.Publico);
        _verificador.AvaliarAsync(publicos, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(publicos, permitida: true));

        await _projetor.DecidirAsync(Porta(publicos), _inscricao, Pares(ClassificacaoDeProtecao.Publico), default);

        _requisicao.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Publico).Should().BeTrue();
        _requisicao.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal).Should().BeFalse();
    }

    [Fact(DisplayName = "Sem nada a registrar a gravação não acontece")]
    public async Task Registrar_SemPermitidas_NaoGrava()
    {
        await _projetor.RegistrarAsync(DecisoesDaProjecao.Vazia, default);

        await _verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
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

    private DecisaoDeAcesso Decisao(PermissionRequirement requisito, bool permitida) =>
        DecisaoDeAcessoDeTeste.Tomada(
            permitida ? ResultadoDoAcesso.Permitido : ResultadoDoAcesso.Negado, requisito, _inscricao);
}

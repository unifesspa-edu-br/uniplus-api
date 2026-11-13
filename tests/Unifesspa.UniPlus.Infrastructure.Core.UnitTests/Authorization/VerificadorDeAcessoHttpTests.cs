namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O verificador decide sobre o tipo fixo <c>operacao</c> quando não há alvo próprio, nunca grava ao avaliar ou ao
/// perguntar a capacidade, e grava o pedido pela política de registro.
/// </summary>
public sealed class VerificadorDeAcessoHttpTests
{
    private const string Permissao = "selecao:inscricoes:consultar";

    private const string LeituraPorTitularidade = RegistroDeAcessoDeTeste.LeituraPorTitularidade;

    private static readonly Guid Unidade = Guid.CreateVersion7();

    private readonly IAuthorizationDecisionService _decisao = Substitute.For<IAuthorizationDecisionService>();
    private readonly IRegistroOperacionalRestrito _registro = Substitute.For<IRegistroOperacionalRestrito>();
    private readonly EffectiveGrant _concessao = EffectiveGrant.From(Permissao, FonteGrant.Concessao, escopoUnidadeId: Unidade).Value!;
    private readonly VerificadorDeAcessoHttp _verificador;

    public VerificadorDeAcessoHttpTests()
    {
        _decisao.DecidirAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(AuthorizationDecision.Permitir(_concessao));
        _verificador = Verificador(_decisao, _registro, Sujeito(_concessao));
    }

    [Fact(DisplayName = "Sem resource informado, a decisão recai sobre o tipo operacao e não sobre o código da permissão")]
    public async Task Exigir_SemResource_DecideSobreOTipoOperacao()
    {
        await _verificador.ExigirAsync(Requisito());

        await _decisao.Received(1).DecidirAsync(
            Arg.Any<AuthorizationSubject>(),
            Arg.Any<PermissionRequirement>(),
            Arg.Is<ResourceContext>(r => r.ResourceTipo == ResourceTipos.Operacao && r.UnidadeProprietariaId == null),
            Arg.Any<AuthorizationRequestContext>(),
            Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "O pedido grava a decisão de dado pessoal e não a de dado interno")]
    [InlineData(ClassificacaoDeProtecao.Pessoal, 1)]
    [InlineData(ClassificacaoDeProtecao.Interno, 0)]
    public async Task Exigir_GravaPelaPoliticaDeRegistro(ClassificacaoDeProtecao sensibilidade, int gravados)
    {
        ResultadoDoAcesso resultado = await _verificador.ExigirAsync(Requisito(sensibilidade));

        resultado.Should().Be(ResultadoDoAcesso.Permitido);
        await _registro.Received(gravados).RegistrarAsync(
            Arg.Is<IReadOnlyList<RegistroDecisaoAcesso>>(l => l.Count == 1 && l[0].Permitido), Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "O pedido grava a negativa de qualquer permissão e a permitida só quando a permissão é de dado pessoal ou mais restrito")]
    [InlineData(PermissaoAutorizacao.PerfisManter, true)]
    [InlineData(PermissaoAutorizacao.PerfisManter, false)]
    [InlineData(PermissaoAutorizacao.ConsultarCatalogo, true)]
    [InlineData(PermissaoAutorizacao.ConsultarCatalogo, false)]
    [InlineData(PermissaoAutorizacao.ConcessoesAdministrar, true)]
    [InlineData(PermissaoAutorizacao.ConcessoesAdministrar, false)]
    [InlineData(PermissaoAutorizacao.PedidosAprovarFuncional, true)]
    [InlineData(PermissaoAutorizacao.PedidosAprovarFuncional, false)]
    [InlineData(PermissaoAutorizacao.PedidosAprovarProtecaoDeDados, true)]
    [InlineData(PermissaoAutorizacao.PedidosAprovarProtecaoDeDados, false)]
    public async Task Exigir_PermissoesDoCatalogoDeAutorizacao_GravaANegativaEAPermitidaSoDeDadoPessoal(PermissaoAutorizacao permissao, bool permitida)
    {
        PermissionRequirement requisito = CatalogoDePermissoes<PermissaoAutorizacao>.Obter(permissao).Requisito;
        _decisao.DecidirAsync(default!, default!, default!, default!, default).ReturnsForAnyArgs(
            permitida ? AuthorizationDecision.Permitir(_concessao) : AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel));

        await _verificador.ExigirAsync(requisito);

        // A negativa grava sempre; a permitida, só quando a permissão é de dado pessoal ou mais restrito.
        int esperados = !permitida || requisito.Sensibilidade >= ClassificacaoDeProtecao.Pessoal ? 1 : 0;
        await _registro.Received(esperados).RegistrarAsync(
            Arg.Is<IReadOnlyList<RegistroDecisaoAcesso>>(l =>
                l.Count == 1 && l[0].Permissao == requisito.Permissao && l[0].Permitido == permitida),
            Arg.Any<CancellationToken>());
        await _registro.Received(esperados).RegistrarAsync(Arg.Any<IReadOnlyList<RegistroDecisaoAcesso>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Falha ao gravar o registro exigido sobe como erro: o pedido não é entregue nem vira negativa")]
    public async Task Exigir_FalhaAoRegistrar_Lanca()
    {
        _registro.RegistrarAsync(default!, default)
            .ReturnsForAnyArgs(Task.FromException(new RegistroDeAcessoNaoGravadoException(new InvalidOperationException())));

        Func<Task> acao = () => _verificador.ExigirAsync(RequisitoPessoal());

        await acao.Should().ThrowAsync<RegistroDeAcessoNaoGravadoException>();
    }

    [Fact(DisplayName = "A decisão por unidade recai sobre o tipo operacao, com a unidade da concessão, e termina na primeira permitida")]
    public async Task AvaliarEmAlgumaUnidade_DecideSobreOTipoOperacaoNaUnidade()
    {
        IReadOnlyList<DecisaoDeAcesso> decisoes = await _verificador.AvaliarEmAlgumaUnidadeAsync(Requisito());

        decisoes.Should().ContainSingle().Which.Should().BeOfType<DecisaoPermitida>();
        await _decisao.Received(1).DecidirAsync(
            Arg.Any<AuthorizationSubject>(),
            Arg.Any<PermissionRequirement>(),
            Arg.Is<ResourceContext>(r => r.ResourceTipo == ResourceTipos.Operacao && r.UnidadeProprietariaId == Unidade),
            Arg.Any<AuthorizationRequestContext>(),
            Arg.Any<CancellationToken>());
        await _registro.DidNotReceiveWithAnyArgs().RegistrarAsync(default!, default);
    }

    [Fact(DisplayName = "Em alguma unidade, sem nenhuma permitida, devolve a negativa de cada unidade")]
    public async Task AvaliarEmAlgumaUnidade_NenhumaPermitida_DevolveAsNegativas()
    {
        _decisao.DecidirAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel));

        IReadOnlyList<DecisaoDeAcesso> decisoes = await _verificador.AvaliarEmAlgumaUnidadeAsync(RequisitoPessoal());

        decisoes.Should().ContainSingle().Which.Should().BeOfType<DecisaoNegada>()
            .Which.Registro.UnidadeProprietariaId.Should().Be(Unidade);
    }

    [Fact(DisplayName = "Em alguma unidade, sem concessão da permissão em unidade alguma, a negativa é de ausência de concessão e sem unidade")]
    public async Task AvaliarEmAlgumaUnidade_SemConcessao_NegaSemConsultarOPontoDeDecisao()
    {
        PermissionRequirement semConcessao = PermissionRequirement.From(
            "selecao:outra:coisa", ClassificacaoDeProtecao.Pessoal, "Lei nº 13.709/2018 (LGPD), art. 7º, III").Value!;

        IReadOnlyList<DecisaoDeAcesso> decisoes = await _verificador.AvaliarEmAlgumaUnidadeAsync(semConcessao);

        DecisaoNegada negada = decisoes.Should().ContainSingle().Which.Should().BeOfType<DecisaoNegada>().Subject;
        negada.Registro.UnidadeProprietariaId.Should().BeNull();
        negada.Registro.MotivoNegativa.Should().Be("sem_concessao_aplicavel");
        await _decisao.DidNotReceiveWithAnyArgs().DecidirAsync(default!, default!, default!, default!, default);
    }

    [Fact(DisplayName = "Em alguma unidade, com concessão só global negada pelo ponto de decisão, devolve a decisão dele e não afirma que não há concessão")]
    public async Task AvaliarEmAlgumaUnidade_SoConcessaoGlobal_DevolveADecisaoDoPontoDeDecisao()
    {
        _decisao.DecidirAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(AuthorizationDecision.Negar(MotivoNegativa.ContextoObrigatorioAusente));
        VerificadorDeAcessoHttp verificador = Verificador(_decisao, _registro, Sujeito(ConcessaoGlobal()));

        IReadOnlyList<DecisaoDeAcesso> decisoes = await verificador.AvaliarEmAlgumaUnidadeAsync(RequisitoPessoal());

        decisoes.Should().ContainSingle().Which.Should().BeOfType<DecisaoNegada>()
            .Which.Registro.MotivoNegativa.Should().Be("contexto_obrigatorio_ausente");
    }

    [Fact(DisplayName = "Em alguma unidade, a concessão só global que o ponto de decisão permitiria é negada com o motivo próprio, e a política a grava")]
    public async Task AvaliarEmAlgumaUnidade_SoConcessaoGlobalQuePermitiria_NegaPorConcessaoEmUnidadeExigida()
    {
        EffectiveGrant global = ConcessaoGlobal();
        _decisao.DecidirAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(AuthorizationDecision.Permitir(global));
        VerificadorDeAcessoHttp verificador = Verificador(_decisao, _registro, Sujeito(global));

        IReadOnlyList<DecisaoDeAcesso> decisoes = await verificador.AvaliarEmAlgumaUnidadeAsync(RequisitoPessoal());

        decisoes.Should().ContainSingle().Which.Should().BeOfType<DecisaoNegada>()
            .Which.Registro.MotivoNegativa.Should().Be("concessao_em_unidade_exigida");
        await verificador.RegistrarDesfechoAsync(decisoes.ToArray(), IntencaoDoRegistro.Pedido);
        await _registro.Received(1).RegistrarAsync(
            Arg.Is<IReadOnlyList<RegistroDecisaoAcesso>>(l => l.Count == 1 && l[0].MotivoNegativa == "concessao_em_unidade_exigida"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Filtro com alternativa e só concessão global na alternativa: 403, e as negativas da principal e da alternativa são gravadas")]
    public async Task Filtro_AlternativaComSoConcessaoGlobal_Responde403EGravaAsDuasNegativas()
    {
        EffectiveGrant global = EffectiveGrant.From(
            "autorizacao:pedidos:aprovar-funcional", FonteGrant.Concessao, escopoInstitucional: true).Value!;
        PermitirSo(global);

        AuthorizationFilterContext contexto = await FiltrarAsync(
            Verificador(_decisao, _registro, Sujeito(global)),
            new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConcessoesAdministrar) { OuEmAlgumaUnidade = PermissaoAutorizacao.PedidosAprovarFuncional });

        contexto.Result.Should().BeOfType<ForbidResult>();
        await _registro.Received(1).RegistrarAsync(
            Arg.Is<IReadOnlyList<RegistroDecisaoAcesso>>(l => SaoAsNegativasDaPrincipalEDaAlternativa(l)), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem sujeito, avaliar devolve a decisão sem sujeito, exigir responde identidade incompleta e nada é gravado")]
    public async Task SemSujeito_NaoDecideNemGrava()
    {
        VerificadorDeAcessoHttp semSujeito = Verificador(_decisao, _registro, sujeito: null);

        (await semSujeito.AvaliarAsync(RequisitoPessoal())).Should().BeSameAs(SemSujeito.Instancia);
        (await semSujeito.ExigirAsync(RequisitoPessoal())).Should().Be(ResultadoDoAcesso.IdentidadeIncompleta);
        (await semSujeito.AvaliarEmAlgumaUnidadeAsync(RequisitoPessoal())).Should().ContainSingle().Which.Should().BeSameAs(SemSujeito.Instancia);
        await _decisao.DidNotReceiveWithAnyArgs().DecidirAsync(default!, default!, default!, default!, default);
        await _registro.DidNotReceiveWithAnyArgs().RegistrarAsync(default!, default);
    }

    [Fact(DisplayName = "A consulta de capacidade responde sem gravar registro algum")]
    public async Task Pode_NaoGravaRegistro()
    {
        ResultadoDoAcesso resultado = await _verificador.PodeAsync(RequisitoPessoal());

        resultado.Should().Be(ResultadoDoAcesso.Permitido);
        await _registro.DidNotReceiveWithAnyArgs().RegistrarAsync(default!, default);
    }

    [Fact(DisplayName = "Avaliar não grava, e registrar a permitida para N inscrições grava um registro por id em um lote")]
    public async Task RegistrarParaCada_GravaUmLotePorId()
    {
        Guid fernanda = Guid.CreateVersion7();
        Guid caroline = Guid.CreateVersion7();
        DecisaoPermitida decisao = (DecisaoPermitida)await _verificador.AvaliarAsync(Requisito());
        await _registro.DidNotReceiveWithAnyArgs().RegistrarAsync(default!, default);

        await _verificador.RegistrarParaCadaAsync(decisao, "inscricao", [fernanda, caroline]);

        await _registro.Received(1).RegistrarAsync(
            Arg.Is<IReadOnlyList<RegistroDecisaoAcesso>>(l => l.Count == 2 && l.All(r => r.ResourceTipo == "inscricao")),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A leitura por titularidade grava um registro permitido por resource, com a fonte titularidade e sem concessão, sem passar pelo ponto de decisão")]
    public async Task RegistrarLeituraPorTitularidade_UmRegistroPorResourceSemDecisao()
    {
        Guid[] pedidos = [Guid.CreateVersion7(), Guid.CreateVersion7()];

        (ResultadoDoAcesso resultado, IReadOnlyList<RegistroDecisaoAcesso> gravados) = await LerPorTitularidadeAsync(_verificador, pedidos);

        resultado.Should().Be(ResultadoDoAcesso.Permitido);
        gravados.Select(static r => r.ResourceId).Should().Equal(pedidos.Select(static p => (Guid?)p));
        gravados.Should().AllSatisfy(static r => (r.Permissao, r.FonteGrant, r.ResourceTipo, r.Sensibilidade, r.Resultado, r.AtorSubject)
            .Should().Be((LeituraPorTitularidade, "titularidade", "pedido", "pessoal", ResultadoDoAcesso.Permitido, "sub-opaco-123")));
        gravados.Should().AllSatisfy(static r => (r.GrantId, r.PerfilId, r.VersaoId, r.EscopoUnidadeId).Should().Be(((Guid?)null, (Guid?)null, (Guid?)null, (Guid?)null)));
        await _decisao.DidNotReceiveWithAnyArgs().DecidirAsync(default!, default!, default!, default!, default);
    }

    [Fact(DisplayName = "O registro serializado da leitura por titularidade leva a fonte titularidade, o código fixo e os campos da concessão nulos")]
    public async Task RegistrarLeituraPorTitularidade_RegistroSerializado()
    {
        (_, IReadOnlyList<RegistroDecisaoAcesso> gravados) = await LerPorTitularidadeAsync(_verificador, [Guid.CreateVersion7()]);

        JsonNode registro = JsonNode.Parse(RegistroDeAcessoKafka.ValorDoLote(gravados))![0]!["registro"]!;

        (registro["fonteGrant"]!.GetValue<string>(), registro["permissao"]!.GetValue<string>(), registro["resultado"]!.GetValue<string>(), registro["permitido"]!.GetValue<bool>())
            .Should().Be(("titularidade", LeituraPorTitularidade, "permitido", true));
        new[] { "grantId", "perfilId", "versaoId", "escopoUnidadeId" }.Should().AllSatisfy(campo => registro[campo].Should().BeNull());
    }

    [Fact(DisplayName = "Sem sujeito, a leitura por titularidade responde identidade incompleta e nada é gravado")]
    public async Task RegistrarLeituraPorTitularidade_SemSujeito_IdentidadeIncompletaSemGravar()
    {
        VerificadorDeAcessoHttp semSujeito = Verificador(_decisao, _registro, sujeito: null);

        ResultadoDoAcesso resultado = await semSujeito.RegistrarLeituraPorTitularidadeAsync(LeituraDoTitular(), "pedido", [Guid.CreateVersion7()]);

        resultado.Should().Be(ResultadoDoAcesso.IdentidadeIncompleta);
        await _registro.DidNotReceiveWithAnyArgs().RegistrarAsync(default!, default);
    }

    [Fact(DisplayName = "Sem resource exibido, a leitura por titularidade não grava nem consulta o registro")]
    public async Task RegistrarLeituraPorTitularidade_SemResource_NaoGrava()
    {
        ResultadoDoAcesso resultado = await _verificador.RegistrarLeituraPorTitularidadeAsync(LeituraDoTitular(), "pedido", []);

        resultado.Should().Be(ResultadoDoAcesso.Permitido);
        await _registro.DidNotReceiveWithAnyArgs().RegistrarAsync(default!, default);
    }

    [Fact(DisplayName = "Falha ao gravar a leitura por titularidade sobe como erro: a leitura não é entregue")]
    public async Task RegistrarLeituraPorTitularidade_FalhaAoRegistrar_Lanca()
    {
        _registro.RegistrarAsync(default!, default)
            .ReturnsForAnyArgs(Task.FromException(new RegistroDeAcessoNaoGravadoException(new InvalidOperationException())));

        Func<Task> acao = () => _verificador.RegistrarLeituraPorTitularidadeAsync(LeituraDoTitular(), "pedido", [Guid.CreateVersion7()]);

        await acao.Should().ThrowAsync<RegistroDeAcessoNaoGravadoException>();
    }

    private async Task<(ResultadoDoAcesso Resultado, IReadOnlyList<RegistroDecisaoAcesso> Gravados)> LerPorTitularidadeAsync(
        VerificadorDeAcessoHttp verificador, Guid[] pedidos)
    {
        IReadOnlyList<RegistroDecisaoAcesso> gravados = [];
        _registro.RegistrarAsync(Arg.Do<IReadOnlyList<RegistroDecisaoAcesso>>(lote => gravados = lote), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        ResultadoDoAcesso resultado = await verificador.RegistrarLeituraPorTitularidadeAsync(LeituraDoTitular(), "pedido", pedidos);

        return (resultado, gravados);
    }

    private static PermissionRequirement LeituraDoTitular() =>
        PermissionRequirement.From(LeituraPorTitularidade, ClassificacaoDeProtecao.Pessoal).Value!;

    // O ponto de decisão permite só a permissão da concessão e nega as demais por falta de concessão.
    private void PermitirSo(EffectiveGrant concessao) =>
        _decisao.DecidirAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(chamada => chamada.ArgAt<PermissionRequirement>(1).Permissao == concessao.PermissaoCodigo
                ? AuthorizationDecision.Permitir(concessao)
                : AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel));

    private static async Task<AuthorizationFilterContext> FiltrarAsync(IVerificadorDeAcesso verificador, ExigePermissaoAttribute<PermissaoAutorizacao> filtro)
    {
        ServiceProvider servicos = new ServiceCollection().AddSingleton(verificador).BuildServiceProvider();
        AuthorizationFilterContext contexto = new(
            new ActionContext(new DefaultHttpContext { RequestServices = servicos }, new RouteData(), new ActionDescriptor()), []);

        await filtro.OnAuthorizationAsync(contexto);

        return contexto;
    }

    private static bool SaoAsNegativasDaPrincipalEDaAlternativa(IReadOnlyList<RegistroDecisaoAcesso> lote) =>
        lote.Count == 2
        && lote.All(static r => !r.Permitido)
        && lote[0].Permissao == "autorizacao:concessoes:administrar"
        && lote[1].Permissao == "autorizacao:pedidos:aprovar-funcional"
        && lote[1].MotivoNegativa == "concessao_em_unidade_exigida";

    private static VerificadorDeAcessoHttp Verificador(
        IAuthorizationDecisionService decisao, IRegistroOperacionalRestrito registro, AuthorizationSubject? sujeito)
    {
        IAuthorizationSubjectResolver resolver = Substitute.For<IAuthorizationSubjectResolver>();
        resolver.ResolveAsync(Arg.Any<IUserContext>(), Arg.Any<AuthorizationRequestContext>(), Arg.Any<CancellationToken>())
            .Returns(sujeito is null
                ? Result<AuthorizationSubject>.Failure(new DomainError("Autorizacao.SujeitoIncompleto", "Token sem sujeito."))
                : Result<AuthorizationSubject>.Success(sujeito));
        ICorrelationIdAccessor correlacao = Substitute.For<ICorrelationIdAccessor>();
        correlacao.CorrelationId.Returns("req-1244");

        return new VerificadorDeAcessoHttp(
            Substitute.For<IUserContext>(), resolver, decisao, registro, Substitute.For<IHttpContextAccessor>(), correlacao, TimeProvider.System);
    }

    private static AuthorizationSubject Sujeito(EffectiveGrant concessao) =>
        AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "sub-opaco-123").Value!,
            "jti-abc",
            concessoesEfetivas: [concessao]).Value!;

    private static EffectiveGrant ConcessaoGlobal() =>
        EffectiveGrant.From(Permissao, FonteGrant.Concessao, escopoInstitucional: true).Value!;

    private static PermissionRequirement RequisitoPessoal() => Requisito(ClassificacaoDeProtecao.Pessoal);

    private static PermissionRequirement Requisito(ClassificacaoDeProtecao sensibilidade = ClassificacaoDeProtecao.Interno) =>
        PermissionRequirement.From(
            Permissao,
            sensibilidade,
            sensibilidade >= ClassificacaoDeProtecao.Pessoal ? "Lei nº 13.709/2018 (LGPD), art. 7º, III" : null).Value!;
}

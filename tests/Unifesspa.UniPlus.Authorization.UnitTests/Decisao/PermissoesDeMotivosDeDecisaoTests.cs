namespace Unifesspa.UniPlus.Authorization.UnitTests.Decisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

using static Unifesspa.UniPlus.Authorization.UnitTests.Decisao.CenariosDeDecisao;

/// <summary>
/// Cenários de aceite da Task #1228 sobre as duas permissões que governam os
/// motivos de decisão de isenção: manter o catálogo e consultar a trilha
/// protegida.
/// </summary>
/// <remarks>
/// A independência entre manutenção e consulta é hoje consequência da
/// comparação por código em <see cref="GrantEfetivoAplicavelCheck"/>, e não de
/// uma regra escrita para ela. É justamente por ser consequência que precisa de
/// teste: qualquer hierarquia entre permissões que venha a ser introduzida — um
/// prefixo que englobe, uma permissão que implique outra — passaria a conceder
/// leitura protegida a quem só recebeu escrita, e o contrário.
/// </remarks>
public sealed class PermissoesDeMotivosDeDecisaoTests
{
    private const string Manter = "selecao:motivos-decisao-recursal:manter";

    private const string ConsultarAuditoria = "selecao:motivos-decisao-recursal:consultar-auditoria";

    private static readonly PermissionRequirement RequisitoDeManutencao =
        PermissionRequirement.From(Manter, Sensibilidade.Interna).Value!;

    private static readonly PermissionRequirement RequisitoDeConsulta =
        PermissionRequirement.From(ConsultarAuditoria, Sensibilidade.Interna).Value!;

    private readonly RegistroEmMemoria _registro = new();

    [Fact(DisplayName = "Quem só tem a manutenção não consulta a trilha protegida")]
    public async Task Decide_ComManutencao_NaoConcedeConsultaDeAuditoria()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao(Manter)),
            RequisitoDeConsulta);

        decisao.Allowed.Should().BeFalse(
            "manter o catálogo é escrever nele, e não ler a justificativa de quem o manteve");
        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "Quem só tem a consulta não mantém o catálogo")]
    public async Task Decide_ComConsultaDeAuditoria_NaoConcedeManutencao()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao(ConsultarAuditoria)),
            RequisitoDeManutencao);

        decisao.Allowed.Should().BeFalse(
            "a consulta auditável existe para o auditor que não escreve — conceder escrita por ela "
            + "desfaria a separação que motivou as duas permissões");
        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "A recusa da consulta sem concessão fica registrada com o motivo canônico")]
    public async Task Decide_ConsultaSemConcessao_RegistraARecusa()
    {
        await Decidir(
            Sujeito(concessoes: Concessao(Manter)),
            RequisitoDeConsulta);

        RegistroDecisaoAcesso registro = _registro.Registrados.Should().ContainSingle().Subject;
        registro.Permitido.Should().BeFalse();
        registro.Permissao.Should().Be(ConsultarAuditoria);
        registro.MotivoNegativa.Should().Be("sem_concessao_aplicavel");
        registro.FonteGrant.Should().BeNull();
    }

    [Fact(DisplayName = "A consulta com concessão vencida nega por concessao_expirada, não por ausência")]
    public async Task Decide_ConsultaComConcessaoVencida_NegaPorExpiracao()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao(
                ConsultarAuditoria,
                fonte: FonteGrant.OidcGroupBinding,
                validoAte: Agora.AddSeconds(-1))),
            RequisitoDeConsulta);

        decisao.Allowed.Should().BeFalse();
        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.ConcessaoExpirada);
    }

    [Fact(DisplayName = "A consulta concedida fora do escopo do recurso é recusada")]
    public async Task Decide_ConsultaComEscopoIncompativel_Nega()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao(
                ConsultarAuditoria,
                escopoProcessoId: Guid.CreateVersion7())),
            RequisitoDeConsulta,
            Recurso(processoId: Guid.CreateVersion7()));

        decisao.Allowed.Should().BeFalse(
            "concessão restrita a um processo não alcança a trilha de outro");
        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "Cada permissão é concedida pela sua própria concessão")]
    public async Task Decide_ComAsDuasConcessoes_ConcedeAsDuasOperacoes()
    {
        AuthorizationSubject sujeito = Sujeito(
            concessoes: [Concessao(Manter), Concessao(ConsultarAuditoria)]);

        (await Decidir(sujeito, RequisitoDeManutencao))
            .Allowed.Should().BeTrue();

        (await Decidir(
            sujeito,
            RequisitoDeConsulta))
            .Allowed.Should().BeTrue(
                "acumular as duas é decisão de quem concede, e nada nas permissões impede");
    }

    private Task<AuthorizationDecision> Decidir(
        AuthorizationSubject sujeito,
        PermissionRequirement requisito,
        ResourceContext? recurso = null)
    {
        AuthorizationDecisionService servico = new(
            new GrantEfetivoAplicavelCheck(),
            Array.Empty<IPermissaoCheck>(),
            _registro);

        return servico.DecideAsync(
            sujeito,
            requisito,
            recurso ?? Recurso(),
            Requisicao(),
            CancellationToken.None);
    }
}

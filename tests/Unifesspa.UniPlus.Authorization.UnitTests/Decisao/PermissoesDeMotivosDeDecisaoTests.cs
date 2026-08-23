namespace Unifesspa.UniPlus.Authorization.UnitTests.Decisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization;
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
/// <para>
/// As conferências gerais do vocabulário percorrem <see cref="UniPlusPermissions.Todas"/>,
/// que é derivada das propriedades da classe — uma permissão apagada sai da
/// lista e sai junto de toda conferência, sem nada acusar. Estes testes nomeiam
/// as duas permissões, de modo que remover ou renomear qualquer uma delas falhe
/// aqui.
/// </para>
/// <para>
/// A independência entre manutenção e consulta é hoje consequência da
/// comparação por código em <see cref="GrantEfetivoAplicavelCheck"/>, e não de
/// uma regra escrita para ela. É justamente por ser consequência que precisa de
/// teste: qualquer hierarquia entre permissões que venha a ser introduzida — um
/// prefixo que englobe, uma permissão que implique outra — passaria a conceder
/// leitura protegida a quem só recebeu escrita, e o contrário.
/// </para>
/// </remarks>
public sealed class PermissoesDeMotivosDeDecisaoTests
{
    private const string Manter = UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalManter;

    private const string ConsultarAuditoria =
        UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalConsultarAuditoria;

    private readonly RegistroEmMemoria _registro = new();

    [Theory(DisplayName = "As permissões de manutenção e de consulta estão declaradas no catálogo")]
    [InlineData(Manter)]
    [InlineData(ConsultarAuditoria)]
    public void Catalogo_DeclaraAsDuasPermissoes(string codigo) =>
        UniPlusPermissions.Todas.Select(static permissao => permissao.Permissao)
            .Should().Contain(codigo);

    [Theory(DisplayName = "As duas permissões declaram apenas metadados que a decisão sabe verificar")]
    [InlineData(Manter)]
    [InlineData(ConsultarAuditoria)]
    public void Requisitos_DeclaramMetadadosVerificaveis(string codigo)
    {
        // O requisito conferido é o que o catálogo publica, e não a propriedade
        // lida direto: é o que está em Todas que a decisão alcança.
        PermissionRequirement requisito = UniPlusPermissions.Todas
            .Should().ContainSingle(permissao => permissao.Permissao == codigo).Subject;

        // Uma permissão que exigisse multifator ou dupla aprovação seria negada
        // em toda requisição, porque não há quem os verifique — declará-los aqui
        // não protegeria nada, apenas tornaria a operação inalcançável. O escopo
        // de contexto obrigatório fica vazio porque o catálogo é institucional:
        // não pertence a unidade, processo nem chamada, e exigir um desses
        // campos negaria por contexto ausente todo acesso legítimo.
        requisito.RequerMfa.Should().BeFalse();
        requisito.RequerDuplaAprovacao.Should().BeFalse();
        requisito.EscopoContextoObrigatorio.Should().BeEmpty();
        requisito.VerificacoesDeContexto.Should().BeEmpty();

        // Interna, e não pessoal nem sensível: o catálogo guarda código,
        // descrição e fundamento institucionais — nada de candidato. Classificar
        // como pessoal exigiria base legal declarada e negaria as duas operações.
        requisito.Sensibilidade.Should().Be(Sensibilidade.Interna);
        requisito.BaseLegalPadrao.Should().BeEmpty();
    }

    [Fact(DisplayName = "Quem só tem a manutenção não consulta a trilha protegida")]
    public async Task Decide_ComManutencao_NaoConcedeConsultaDeAuditoria()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao(Manter)),
            UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalConsultarAuditoriaRequirement);

        decisao.Allowed.Should().BeFalse(
            "manter o catálogo é escrever nele, e não ler a justificativa de quem o manteve");
        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "Quem só tem a consulta não mantém o catálogo")]
    public async Task Decide_ComConsultaDeAuditoria_NaoConcedeManutencao()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao(ConsultarAuditoria)),
            UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalManterRequirement);

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
            UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalConsultarAuditoriaRequirement);

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
            UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalConsultarAuditoriaRequirement);

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
            UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalConsultarAuditoriaRequirement,
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

        (await Decidir(sujeito, UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalManterRequirement))
            .Allowed.Should().BeTrue();

        (await Decidir(
            sujeito,
            UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalConsultarAuditoriaRequirement))
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

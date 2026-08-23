namespace Unifesspa.UniPlus.Authorization.Enums;

using System.ComponentModel;

/// <summary>
/// Tradução dos enums do contrato de autorização para o <b>valor canônico de
/// serialização externa</b> que cada membro documenta (<c>snake_case</c> /
/// <c>kebab-case</c>).
/// </summary>
/// <remarks>
/// Existe porque o identificador C# está em PascalCase e o valor canônico não:
/// deixar a conversão a cargo de um serializador genérico grava
/// <c>ConcessaoExpirada</c> onde o contrato promete <c>concessao_expirada</c>, e
/// o desvio só aparece na leitura do registro. A tradução é explícita e
/// exaustiva — um membro novo no enum sem entrada aqui falha em tempo de
/// execução na primeira conversão, e o teste de exaustividade o pega antes.
/// </remarks>
public static class ValoresCanonicos
{
    /// <summary>Valor canônico do motivo de negativa.</summary>
    public static string De(MotivoNegativa motivo) => motivo switch
    {
        MotivoNegativa.MultifatorNaoSatisfeito => "multifator_nao_satisfeito",
        MotivoNegativa.DuplaAprovacaoInvalida => "dupla_aprovacao_invalida",
        MotivoNegativa.SemConcessaoAplicavel => "sem_concessao_aplicavel",
        MotivoNegativa.ConcessaoExpirada => "concessao_expirada",
        MotivoNegativa.FaseFechada => "fase_fechada",
        MotivoNegativa.EstadoDoRecursoIncompativel => "estado_do_recurso_incompativel",
        MotivoNegativa.EscopoDeAuditoriaInativo => "escopo_de_auditoria_inativo",
        MotivoNegativa.EquipeInativaNoProcesso => "equipe_inativa_no_processo",
        MotivoNegativa.AtribuicaoDocumentalInativa => "atribuicao_documental_inativa",
        MotivoNegativa.BaseLegalAusente => "base_legal_ausente",
        MotivoNegativa.ConformidadeLegalNaoValidada => "conformidade_legal_nao_validada",
        MotivoNegativa.ContextoObrigatorioAusente => "contexto_obrigatorio_ausente",
        MotivoNegativa.MecanismoRevogacaoDegradado => "mecanismo_revogacao_degradado",
        _ => throw new InvalidEnumArgumentException(nameof(motivo), (int)motivo, typeof(MotivoNegativa)),
    };

    /// <summary>Valor canônico da fonte da concessão.</summary>
    public static string De(FonteGrant fonte) => fonte switch
    {
        FonteGrant.Token => "token",
        FonteGrant.OidcGroupBinding => "oidc_group_binding",
        FonteGrant.PermissaoExcecional => "permissao_excecional",
        _ => throw new InvalidEnumArgumentException(nameof(fonte), (int)fonte, typeof(FonteGrant)),
    };

    /// <summary>Valor canônico do canal de origem da requisição.</summary>
    public static string De(OrigemRequisicao origem) => origem switch
    {
        OrigemRequisicao.Api => "api",
        OrigemRequisicao.Jobs => "jobs",
        OrigemRequisicao.AdminCli => "admin-cli",
        _ => throw new InvalidEnumArgumentException(nameof(origem), (int)origem, typeof(OrigemRequisicao)),
    };

    /// <summary>
    /// Valor canônico da sensibilidade. Igual ao vocabulário aceito no campo
    /// <c>sensibilidade</c> do catálogo declarativo (ADR-0080).
    /// </summary>
    public static string De(Sensibilidade sensibilidade) => sensibilidade switch
    {
        Sensibilidade.Publica => "publica",
        Sensibilidade.Interna => "interna",
        Sensibilidade.Pessoal => "pessoal",
        Sensibilidade.Sensivel => "sensivel",
        _ => throw new InvalidEnumArgumentException(nameof(sensibilidade), (int)sensibilidade, typeof(Sensibilidade)),
    };
}

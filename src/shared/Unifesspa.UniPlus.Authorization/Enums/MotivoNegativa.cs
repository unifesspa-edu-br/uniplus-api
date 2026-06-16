namespace Unifesspa.UniPlus.Authorization.Enums;

/// <summary>
/// Conjunto fechado de motivos pelos quais a decisão de autorização nega o
/// acesso (ADR-0078). O motivo é estável e <b>sem dados pessoais</b> — serve a
/// diagnóstico e auditoria. São exatamente 13 motivos; o identificador C# está
/// em PascalCase e o valor canônico de serialização externa em <c>snake_case</c>
/// está indicado em cada membro.
/// </summary>
public enum MotivoNegativa
{
    /// <summary>Valor canônico: <c>multifator_nao_satisfeito</c>.</summary>
    MultifatorNaoSatisfeito = 0,

    /// <summary>Valor canônico: <c>dupla_aprovacao_invalida</c>.</summary>
    DuplaAprovacaoInvalida = 1,

    /// <summary>Valor canônico: <c>sem_concessao_aplicavel</c>.</summary>
    SemConcessaoAplicavel = 2,

    /// <summary>Valor canônico: <c>concessao_expirada</c>.</summary>
    ConcessaoExpirada = 3,

    /// <summary>Valor canônico: <c>fase_fechada</c>.</summary>
    FaseFechada = 4,

    /// <summary>Valor canônico: <c>estado_do_recurso_incompativel</c>.</summary>
    EstadoDoRecursoIncompativel = 5,

    /// <summary>Valor canônico: <c>escopo_de_auditoria_inativo</c>.</summary>
    EscopoDeAuditoriaInativo = 6,

    /// <summary>Valor canônico: <c>equipe_inativa_no_processo</c>.</summary>
    EquipeInativaNoProcesso = 7,

    /// <summary>Valor canônico: <c>atribuicao_documental_inativa</c>.</summary>
    AtribuicaoDocumentalInativa = 8,

    /// <summary>Valor canônico: <c>base_legal_ausente</c>.</summary>
    BaseLegalAusente = 9,

    /// <summary>Valor canônico: <c>conformidade_legal_nao_validada</c>.</summary>
    ConformidadeLegalNaoValidada = 10,

    /// <summary>Valor canônico: <c>contexto_obrigatorio_ausente</c>.</summary>
    ContextoObrigatorioAusente = 11,

    /// <summary>Valor canônico: <c>mecanismo_revogacao_degradado</c>.</summary>
    MecanismoRevogacaoDegradado = 12,
}

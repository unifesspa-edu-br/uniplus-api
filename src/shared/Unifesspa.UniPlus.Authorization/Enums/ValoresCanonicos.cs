namespace Unifesspa.UniPlus.Authorization.Enums;

using System.Collections.Frozen;
using System.ComponentModel;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

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
    // O vocabulário de motivos cresce a cada verificação nova da decisão. Como é um mapeamento puro, fica numa tabela, e a
    // tradução de qualquer motivo é uma busca só.
    private static readonly FrozenDictionary<MotivoNegativa, string> Motivos = new Dictionary<MotivoNegativa, string>
    {
        [MotivoNegativa.MultifatorNaoSatisfeito] = "multifator_nao_satisfeito",
        [MotivoNegativa.DuplaAprovacaoInvalida] = "dupla_aprovacao_invalida",
        [MotivoNegativa.SemConcessaoAplicavel] = "sem_concessao_aplicavel",
        [MotivoNegativa.ConcessaoExpirada] = "concessao_expirada",
        [MotivoNegativa.FaseFechada] = "fase_fechada",
        [MotivoNegativa.EstadoDoResourceIncompativel] = "estado_do_resource_incompativel",
        [MotivoNegativa.EscopoDeAuditoriaInativo] = "escopo_de_auditoria_inativo",
        [MotivoNegativa.EquipeInativaNoProcesso] = "equipe_inativa_no_processo",
        [MotivoNegativa.AtribuicaoDocumentalInativa] = "atribuicao_documental_inativa",
        [MotivoNegativa.BaseLegalAusente] = "base_legal_ausente",
        [MotivoNegativa.ConformidadeLegalNaoValidada] = "conformidade_legal_nao_validada",
        [MotivoNegativa.ContextoObrigatorioAusente] = "contexto_obrigatorio_ausente",
        [MotivoNegativa.MecanismoRevogacaoDegradado] = "mecanismo_revogacao_degradado",
        [MotivoNegativa.ImpedidoPorInscricaoNoProcesso] = "impedido_por_inscricao_no_processo",
        [MotivoNegativa.ConcessaoEmUnidadeExigida] = "concessao_em_unidade_exigida",
        [MotivoNegativa.ForaDaDesignacao] = "fora_da_designacao",
    }.ToFrozenDictionary();

    /// <summary>Valor canônico do motivo de negativa.</summary>
    public static string De(MotivoNegativa motivo) =>
        Motivos.TryGetValue(motivo, out string? valor)
            ? valor
            : throw new InvalidEnumArgumentException(nameof(motivo), (int)motivo, typeof(MotivoNegativa));

    /// <summary>
    /// Valor canônico do resultado de uma decisão tomada. A identidade incompleta não é resultado de decisão: não há
    /// registro nem cabeçalho que a nomeie.
    /// </summary>
    public static string De(ResultadoDoAcesso resultado) => resultado switch
    {
        ResultadoDoAcesso.Permitido => "permitido",
        ResultadoDoAcesso.Negado => "negado",
        ResultadoDoAcesso.Impedido => "impedido",
        _ => throw new InvalidEnumArgumentException(nameof(resultado), (int)resultado, typeof(ResultadoDoAcesso)),
    };

    /// <summary>Valor canônico da fonte do acesso: a da concessão usada ou, na leitura da parte, a titularidade.</summary>
    public static string De(FonteGrant fonte) => fonte switch
    {
        FonteGrant.Concessao => "concessao",
        FonteGrant.OidcGroupBinding => "oidc_group_binding",
        FonteGrant.PermissaoExcecional => "permissao_excecional",
        FonteGrant.Titularidade => "titularidade",
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
    /// Valor canônico da sensibilidade, no vocabulário dos fatos do candidato
    /// (<c>publico</c>, <c>interno</c>, <c>pessoal</c>, <c>identificador</c>, <c>sensivel</c>),
    /// que é o mesmo do campo <c>sensibilidade</c> do catálogo de permissões (ADR-0151).
    /// </summary>
    public static string De(ClassificacaoDeProtecao sensibilidade) =>
        ClassificacoesDeProtecao.ParaTokenCanonico(sensibilidade).ToLowerInvariant();
}

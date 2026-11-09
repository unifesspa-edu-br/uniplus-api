namespace Unifesspa.UniPlus.Authorization.Contracts;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Evento do registro operacional restrito: o que fica gravado, de forma durável, sobre <b>um uso</b>
/// de permissão — ator, instante, origem, permissão exigida, resource alvo,
/// resultado e, conforme o caso, o motivo da negativa ou a concessão que
/// autorizou (fonte, identificador, perfil e unidade) (CA-07 de <c>uniplus-api#1197</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>O ator é o par emissor e <i>subject</i> do token</b>, que são opacos: o registro
/// precisa dizer quem acessou, e a consulta por pessoa depende disso. O <c>jti</c>
/// identifica a sessão e não entra. A correlação com a requisição fica pelo
/// <see cref="RequestId"/>. O resource entra por metadados (tipo, identificadores
/// de escopo e, na revelação, o código do fato), nunca por conteúdo.
/// </para>
/// <para>
/// Os valores de enumeração são gravados já no <b>valor canônico</b>
/// (<see cref="ValoresCanonicos"/>), e não no identificador C#: o registro é
/// lido por fora do processo e o contrato do vocabulário é o canônico.
/// </para>
/// </remarks>
public sealed record RegistroDecisaoAcesso
{
    /// <summary>Instante do acesso, em UTC — o mesmo do contexto da requisição.</summary>
    public DateTimeOffset Instante { get; }

    /// <summary>Emissor do token de quem acessou.</summary>
    public string AtorEmissor { get; }

    /// <summary><i>Subject</i> opaco do token de quem acessou.</summary>
    public string AtorSubject { get; }

    /// <summary>Correlação da requisição, para cruzar com logs e mensagens. Vem do cliente e não identifica o acesso.</summary>
    public string RequestId { get; }

    /// <summary>Execução do servidor que originou o acesso — a mesma para todo acesso da mesma requisição.</summary>
    public Guid ExecucaoId { get; }

    /// <summary>Código da permissão exigida.</summary>
    public string Permissao { get; }

    /// <summary>Tipo do resource alvo.</summary>
    public string ResourceTipo { get; private init; }

    /// <summary>
    /// Sensibilidade efetiva do dado alcançado, em valor canônico: a maior entre
    /// a que a permissão classifica e a que o contexto do resource declara — a
    /// mesma que decidiu a exigência de base legal. Registrar só a do contexto
    /// faria uma operação sensível constar como interna, e consultas de
    /// conformidade sobre o registro deixariam de encontrá-la.
    /// </summary>
    public string Sensibilidade { get; }

    /// <summary>Unidade proprietária do resource, quando o escopo se aplica.</summary>
    public Guid? UnidadeProprietariaId { get; }

    /// <summary>Processo seletivo do resource, quando o escopo se aplica.</summary>
    public Guid? ProcessoId { get; }

    /// <summary>Chamada do resource, quando o escopo se aplica.</summary>
    public Guid? ChamadaId { get; }

    /// <summary>Registro alvo da operação (por exemplo, a inscrição), quando a decisão recai sobre um único registro.</summary>
    public Guid? ResourceId { get; private init; }

    /// <summary>Inscrição a que o acesso se refere, quando o resource é a inscrição ou um item dela.</summary>
    public Guid? InscricaoId { get; private init; }

    /// <summary>
    /// Código do fato revelado, quando o acesso recai sobre um único campo. É o código do catálogo, que não é dado
    /// pessoal (ADR-0019).
    /// </summary>
    public string? FatoCodigo { get; }

    /// <summary>Ocorrência do fato revelado, quando o campo está num grupo que se repete.</summary>
    public string? Ocorrencia { get; }

    /// <summary>Canal pelo qual a requisição chegou, em valor canônico.</summary>
    public string Origem { get; }

    /// <summary>
    /// Client OIDC que obteve o token do acesso (claim <c>azp</c>, assinado pelo provedor de identidade), e assim a
    /// aplicação que o fez: a tela da Seleção, a da Configuração ou um token de serviço. Nulo quando o token não o traz.
    /// </summary>
    public string? ClienteOidc { get; }

    /// <summary>
    /// Desfecho da decisão, gravado no valor canônico: <c>permitido</c>, <c>negado</c> ou <c>impedido</c>. O impedido é
    /// uma negativa com motivo próprio, e quem lê o registro não deve ter de reconhecê-lo pelo texto do motivo.
    /// </summary>
    [JsonConverter(typeof(ResultadoDoAcessoCanonicoConverter))]
    public ResultadoDoAcesso Resultado { get; }

    /// <summary>Veredito da decisão, derivado do <see cref="Resultado"/>.</summary>
    public bool Permitido => Resultado == ResultadoDoAcesso.Permitido;

    /// <summary>Motivo canônico da negativa — presente se, e somente se, negada.</summary>
    public string? MotivoNegativa { get; }

    /// <summary>Fonte canônica da concessão usada — presente se, e somente se, permitida.</summary>
    public string? FonteGrant { get; }

    /// <summary>Identificador da concessão usada, quando ela o possui.</summary>
    public Guid? GrantId { get; }

    /// <summary>Perfil de acesso da concessão usada, quando ela é de perfil.</summary>
    public Guid? PerfilId { get; }

    /// <summary>Versão do perfil a que a concessão usada está presa; nula quando a concessão não é de perfil comum.</summary>
    public Guid? VersaoId { get; private init; }

    /// <summary>Unidade do escopo da concessão usada; nula quando o escopo é a instituição.</summary>
    public Guid? EscopoUnidadeId { get; }

    private RegistroDecisaoAcesso(
        string atorEmissor,
        string atorSubject,
        DateTimeOffset instante,
        string requestId,
        Guid execucaoId,
        string permissao,
        string resourceTipo,
        string sensibilidade,
        Guid? unidadeProprietariaId,
        Guid? processoId,
        Guid? chamadaId,
        Guid? resourceId,
        Guid? inscricaoId,
        string? fatoCodigo,
        string? ocorrencia,
        string origem,
        string? clienteOidc,
        ResultadoDoAcesso resultado,
        string? motivoNegativa,
        string? fonteGrant,
        Guid? grantId,
        Guid? perfilId,
        Guid? escopoUnidadeId)
    {
        AtorEmissor = atorEmissor;
        AtorSubject = atorSubject;
        Instante = instante;
        RequestId = requestId;
        ExecucaoId = execucaoId;
        Permissao = permissao;
        ResourceTipo = resourceTipo;
        Sensibilidade = sensibilidade;
        UnidadeProprietariaId = unidadeProprietariaId;
        ProcessoId = processoId;
        ChamadaId = chamadaId;
        ResourceId = resourceId;
        InscricaoId = inscricaoId;
        FatoCodigo = fatoCodigo;
        Ocorrencia = ocorrencia;
        Origem = origem;
        ClienteOidc = clienteOidc;
        Resultado = resultado;
        MotivoNegativa = motivoNegativa;
        FonteGrant = fonteGrant;
        GrantId = grantId;
        PerfilId = perfilId;
        EscopoUnidadeId = escopoUnidadeId;
    }

    /// <summary>
    /// Deriva o registro dos mesmos contratos que a decisão recebeu, mais a
    /// decisão tomada. É o único caminho de construção: a escolha do que entra —
    /// e, sobretudo, do que não entra — no registro fica em um lugar só, em vez
    /// de ser recomposta por cada chamador.
    /// </summary>
    public static RegistroDecisaoAcesso De(
        AuthorizationSubject subject,
        PermissionRequirement requirement,
        ResourceContext resource,
        AuthorizationRequestContext request,
        AuthorizationDecision decision)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(decision);

        EffectiveGrant? grant = decision.GrantUsed;

        return new RegistroDecisaoAcesso(
            subject.Usuario.Emissor,
            subject.Usuario.Subject,
            request.DataAcesso,
            request.RequestId,
            request.ExecucaoId,
            requirement.Permissao,
            resource.ResourceTipo,
            ValoresCanonicos.De(SensibilidadeEfetiva(requirement, resource)),
            resource.UnidadeProprietariaId,
            resource.ProcessoId,
            resource.ChamadaId,
            resource.ResourceId,
            resource.InscricaoId,
            resource.FatoCodigo,
            resource.Ocorrencia,
            ValoresCanonicos.De(request.Origem),
            request.ClienteOidc,
            decision.Resultado,
            decision.DenyReason is { } motivo ? ValoresCanonicos.De(motivo.Codigo) : null,
            grant is not null ? ValoresCanonicos.De(grant.Fonte) : null,
            grant?.GrantId,
            grant?.PerfilId,
            grant?.EscopoUnidadeId).ComVersaoDe(grant);
    }

    private RegistroDecisaoAcesso ComVersaoDe(EffectiveGrant? grant) => this with { VersaoId = grant?.VersaoId };

    /// <summary>
    /// Um registro por id de resource, a partir do registro de uma decisão <b>permitida</b>: cada
    /// um herda dele o ator, a permissão, a sensibilidade, a concessão e o escopo, e troca o tipo e o id do
    /// resource. É como a listagem registra uma inscrição por linha tendo decidido uma vez só, sobre o processo.
    /// </summary>
    /// <param name="resourceTipo">Tipo do resource de cada registro.</param>
    /// <param name="resourceIds">Um registro por id distinto.</param>
    /// <param name="resourceEhInscricao">Se o resource é a inscrição: o id passa a ser também a inscrição do registro.</param>
    public IReadOnlyList<RegistroDecisaoAcesso> ParaCadaResource(
        string resourceTipo, IEnumerable<Guid> resourceIds, bool resourceEhInscricao = false)
    {
        ArgumentNullException.ThrowIfNull(resourceIds);

        if (!Permitido)
        {
            throw new InvalidOperationException("Só a decisão permitida gera registro por resource.");
        }

        if (FatoCodigo is not null)
        {
            throw new InvalidOperationException("A revelação de um campo é sempre de um resource só.");
        }

        if (!ResourceTipos.SegueAConvencao(resourceTipo))
        {
            throw new ArgumentException(
                $"O tipo de resource '{resourceTipo}' está fora da convenção kebab-case.", nameof(resourceTipo));
        }

        return [.. resourceIds.Distinct().Select(id => this with
        {
            ResourceTipo = resourceTipo,
            ResourceId = id,
            InscricaoId = resourceEhInscricao ? id : InscricaoId,
        })];
    }

    /// <summary>
    /// Identifica o acesso dentro da execução do servidor: quem acessou, o que e onde. Repetir o mesmo acesso na mesma
    /// execução não o duplica; outra execução, ou outra pessoa, é outro acesso. Não usa o identificador de correlação,
    /// que o cliente escolhe.
    /// </summary>
    [JsonIgnore]
    public string Identidade =>
        JsonSerializer.Serialize(new object?[]
        {
            ExecucaoId,
            AtorEmissor,
            AtorSubject,
            Permissao,
            ResourceTipo,
            ResourceId,
            FatoCodigo,
            Ocorrencia,
            UnidadeProprietariaId,
            ProcessoId,
            ChamadaId,
            Permitido,
        });

    /// <summary>
    /// A sensibilidade efetiva do dado alcançado: a maior entre a que a permissão classifica e a que o resource declara.
    /// É a que consta do registro e a que decide a exigência de base legal e de registro.
    /// </summary>
    public static ClassificacaoDeProtecao SensibilidadeEfetiva(PermissionRequirement requirement, ResourceContext resource)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(resource);

        return MaisRestritiva(requirement.Sensibilidade, resource.Sensibilidade);
    }

    /// <summary>
    /// Chave do acesso: resumo SHA-256 da <see cref="Identidade"/>. Vai no valor da mensagem publicada, e o consumidor
    /// deduplica por ela quando o mesmo acesso chega mais de uma vez. A chave da mensagem é a execução.
    /// </summary>
    [JsonIgnore]
    public string Chave => ResumoSha256(Identidade);

    /// <summary>Resumo SHA-256 do texto, em hexadecimal minúsculo: a forma única das chaves do registro e do lote.</summary>
    public static string ResumoSha256(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
    }

    // A escala é crescente por construção (Publico < Interno < Pessoal <
    // Identificador < Sensivel), então a mais restritiva é a de maior valor.
    private static ClassificacaoDeProtecao MaisRestritiva(ClassificacaoDeProtecao primeira, ClassificacaoDeProtecao segunda)
        => (ClassificacaoDeProtecao)Math.Max((int)primeira, (int)segunda);
}

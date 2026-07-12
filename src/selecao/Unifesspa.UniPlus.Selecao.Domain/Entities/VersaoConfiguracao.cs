namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Versão congelada da configuração de um <see cref="ProcessoSeletivo"/>
/// (RN08, ADR-0104): agregado próprio, e não um apêndice do documento que a
/// publicou. Quem ordena as versões é <see cref="VigenteAPartirDe"/> — o
/// relógio do sistema —, nunca a data documental do ato, que a retificação
/// republica inalterada.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IForensicEntity"/> per ADR-0063: append-only, sem soft-delete e
/// sem audit fields. <c>UPDATE</c>/<c>DELETE</c>/<c>TRUNCATE</c> são bloqueados
/// no próprio banco por trigger — o passado da configuração não se muta.
/// </para>
/// <para>
/// <see cref="AtoCriadorId"/> é referência <b>por valor</b> ao ato que criou
/// esta versão (ADR-0061): hoje o <see cref="Edital"/> interno do módulo;
/// depois de #804, o ato registrado em <c>Publicacoes</c>. Não há chave
/// estrangeira em nenhum dos dois casos — a garantia forense é local
/// (<c>NOT NULL</c> + <c>UNIQUE</c>): toda versão tem exatamente um ato
/// criador, e um ato cria no máximo uma versão.
/// </para>
/// <para>
/// <see cref="HashConfiguracao"/> e <see cref="ConfiguracaoCongelada"/> são
/// DERIVADOS de <see cref="ConfiguracaoCongeladaCanonica"/> dentro da factory
/// — nunca aceitos como parâmetros independentes, para que a evidência
/// persistida jamais divirja dos bytes que a fundamentam (ADR-0100 itens 6/7).
/// </para>
/// </remarks>
public sealed class VersaoConfiguracao : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid ProcessoSeletivoId { get; private init; }

    /// <summary>Monotônico e contíguo por processo — a versão 1 é a da abertura.</summary>
    public int NumeroVersao { get; private init; }

    /// <summary>
    /// Relógio do SISTEMA (ADR-0068) — é o que ordena as versões (#803). Sem
    /// restrição de unicidade: duas versões no mesmo instante não colidem, e o
    /// desempate cabe a <see cref="NumeroVersao"/>.
    /// </summary>
    public DateTimeOffset VigenteAPartirDe { get; private init; }

    public string SchemaVersion { get; private init; } = null!;

    public string AlgoritmoHash { get; private init; } = null!;

    /// <summary>Bytes canônicos (ADR-0100) — a base do hash; fonte única de verdade.</summary>
#pragma warning disable CA1819 // Properties should not return arrays — entidade EF Core mapeia bytea diretamente; sem value-equality de record.
    public byte[] ConfiguracaoCongeladaCanonica { get; private init; } = null!;
#pragma warning restore CA1819

    /// <summary>Derivado por parsing UTF-8 dos bytes canônicos — só consulta (jsonb).</summary>
    public string ConfiguracaoCongelada { get; private init; } = null!;

    /// <summary>SHA-256 (hex minúsculo) de <see cref="ConfiguracaoCongeladaCanonica"/> — nunca recebido como parâmetro.</summary>
    public string HashConfiguracao { get; private init; } = null!;

    /// <summary>Referência por valor ao ato que criou esta versão — sem FK (ADR-0061).</summary>
    public Guid AtoCriadorId { get; private init; }

    /// <summary>SHA-256 (hex minúsculo) do documento do ato criador.</summary>
    public string AtoCriadorHash { get; private init; } = null!;

    /// <summary>
    /// O ato que o ato criador retifica — nulo na versão 1, obrigatório em
    /// toda versão <c>N &gt; 1</c> (contrato simétrico). É o que torna
    /// verificável, sem consultar <c>Publicacoes</c>, que existe exatamente
    /// uma cadeia de versões por certame.
    /// </summary>
    public Guid? AtoCriadorRetificaId { get; private init; }

    /// <summary>Sub do usuário que publicou — evidência forense de autoria.</summary>
    public string AtorUsuarioSub { get; private init; } = null!;

    // Construtor de materialização do EF Core.
    private VersaoConfiguracao()
    {
    }

    /// <summary>
    /// Abre a cadeia de versões do processo: a versão 1, criada pelo ato de
    /// abertura. Não retifica ato algum — <see cref="AtoCriadorRetificaId"/>
    /// nasce nulo, e é a única versão em que isso é válido.
    /// </summary>
    public static VersaoConfiguracao Abrir(
        Guid processoSeletivoId,
        byte[] configuracaoCongeladaCanonica,
        string schemaVersion,
        string algoritmoHash,
        Guid atoCriadorId,
        string atoCriadorHash,
        string atorUsuarioSub,
        DateTimeOffset instante)
    {
        GuardarFormato(
            processoSeletivoId,
            configuracaoCongeladaCanonica,
            schemaVersion,
            algoritmoHash,
            atoCriadorId,
            atoCriadorHash,
            atorUsuarioSub);

        return Congelar(
            processoSeletivoId,
            numeroVersao: 1,
            configuracaoCongeladaCanonica,
            schemaVersion,
            algoritmoHash,
            atoCriadorId,
            atoCriadorHash,
            atoCriadorRetificaId: null,
            atorUsuarioSub,
            instante);
    }

    /// <summary>
    /// Sucede <paramref name="anterior"/> na cadeia do mesmo processo: a versão
    /// <c>N + 1</c>, criada por um ato que retifica o ato criador da versão
    /// <c>N</c>. O número é derivado da anterior — buraco de numeração é
    /// impossível por construção.
    /// </summary>
    /// <remarks>
    /// As invariantes de cadeia que dependem do ESTADO do certame (a versão
    /// corrente ser deste processo; o ato criador emendar o ato criador dela)
    /// são verificadas por <see cref="ProcessoSeletivo.Retificar"/> — a raiz é
    /// quem tem o contexto para recusá-las como regra de negócio (422,
    /// ADR-0102). Os guards aqui são a última linha: relançam como erro de
    /// programação o que a raiz já deveria ter barrado, para que o agregado
    /// nunca materialize em estado inválido — mesmo critério de
    /// <c>AtoNormativo.Registrar</c> (ADR-0063).
    /// </remarks>
    public static VersaoConfiguracao Suceder(
        VersaoConfiguracao anterior,
        byte[] configuracaoCongeladaCanonica,
        string schemaVersion,
        string algoritmoHash,
        Guid atoCriadorId,
        string atoCriadorHash,
        Guid atoCriadorRetificaId,
        string atorUsuarioSub,
        DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(anterior);
        GuardarFormato(
            anterior.ProcessoSeletivoId,
            configuracaoCongeladaCanonica,
            schemaVersion,
            algoritmoHash,
            atoCriadorId,
            atoCriadorHash,
            atorUsuarioSub);

        if (atoCriadorRetificaId != anterior.AtoCriadorId)
        {
            throw new ArgumentException(
                "O ato criador de uma versão sucessora deve retificar o ato criador da versão anterior.",
                nameof(atoCriadorRetificaId));
        }

        if (atoCriadorId == anterior.AtoCriadorId)
        {
            throw new ArgumentException(
                "Um ato cria no máximo uma versão de configuração.",
                nameof(atoCriadorId));
        }

        // A vigência de uma sucessora nunca precede a da versão que ela sucede.
        // O relógio do sistema pode andar para trás (ajuste NTP em degrau, troca
        // de hora do host) — e como é a vigência que ORDENA as versões, um
        // retrocesso faria a versão N continuar sendo resolvida como vigente
        // depois de a N+1 existir. Empatar no instante da anterior é seguro por
        // desenho: o empate é permitido (não há unicidade de instante) e o
        // desempate por NumeroVersao decrescente elege a mais nova (ADR-0104).
        DateTimeOffset vigenteAPartirDe = instante < anterior.VigenteAPartirDe
            ? anterior.VigenteAPartirDe
            : instante;

        return Congelar(
            anterior.ProcessoSeletivoId,
            anterior.NumeroVersao + 1,
            configuracaoCongeladaCanonica,
            schemaVersion,
            algoritmoHash,
            atoCriadorId,
            atoCriadorHash,
            atoCriadorRetificaId,
            atorUsuarioSub,
            vigenteAPartirDe);
    }

    private static VersaoConfiguracao Congelar(
        Guid processoSeletivoId,
        int numeroVersao,
        byte[] configuracaoCongeladaCanonica,
        string schemaVersion,
        string algoritmoHash,
        Guid atoCriadorId,
        string atoCriadorHash,
        Guid? atoCriadorRetificaId,
        string atorUsuarioSub,
        DateTimeOffset vigenteAPartirDe)
    {
        // Cópia defensiva: os bytes são a base do hash e do jsonb derivados logo
        // abaixo. Guardar a referência do caller deixaria uma janela em que
        // mutá-la, antes do INSERT, persistiria bytes que não correspondem ao
        // hash — a evidência forense deixaria de provar o que diz provar.
        byte[] canonico = [.. configuracaoCongeladaCanonica];

        return new VersaoConfiguracao
        {
            ProcessoSeletivoId = processoSeletivoId,
            NumeroVersao = numeroVersao,
            VigenteAPartirDe = vigenteAPartirDe,
            SchemaVersion = schemaVersion,
            AlgoritmoHash = algoritmoHash,
            ConfiguracaoCongeladaCanonica = canonico,
            ConfiguracaoCongelada = Encoding.UTF8.GetString(canonico),
            HashConfiguracao = HashCanonicalComputer.ComputeSha256Hex(canonico),
            AtoCriadorId = atoCriadorId,
            AtoCriadorHash = atoCriadorHash,
            AtoCriadorRetificaId = atoCriadorRetificaId,
            AtorUsuarioSub = atorUsuarioSub,
        };
    }

    /// <summary>
    /// Checagens de shape comuns às duas factories. Lançam
    /// <see cref="ArgumentException"/> — e não devolvem <see cref="DomainError"/>
    /// — porque não são regra de negócio exposta ao usuário: são invariantes que
    /// o caller (<see cref="ProcessoSeletivo.Publicar"/>/<see cref="ProcessoSeletivo.Retificar"/>)
    /// já garante, e cuja violação é erro de programação (mesmo critério de
    /// <see cref="ObrigatoriedadeLegalHistorico.Snapshot"/> e de
    /// <c>AtoNormativo.Registrar</c>, ADR-0063). As regras de negócio genuínas —
    /// a cadeia do certame — são da raiz, que as recusa como 422 (ADR-0102)
    /// antes de chegar aqui.
    /// </summary>
    private static void GuardarFormato(
        Guid processoSeletivoId,
        byte[] configuracaoCongeladaCanonica,
        string schemaVersion,
        string algoritmoHash,
        Guid atoCriadorId,
        string atoCriadorHash,
        string atorUsuarioSub)
    {
        ArgumentNullException.ThrowIfNull(configuracaoCongeladaCanonica);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(algoritmoHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(atorUsuarioSub);

        if (processoSeletivoId == Guid.Empty)
        {
            throw new ArgumentException(
                "A versão de configuração deve estar vinculada a um Processo Seletivo.",
                nameof(processoSeletivoId));
        }

        if (configuracaoCongeladaCanonica.Length == 0)
        {
            throw new ArgumentException(
                "A configuração congelada não pode ser vazia.",
                nameof(configuracaoCongeladaCanonica));
        }

        if (atoCriadorId == Guid.Empty)
        {
            throw new ArgumentException(
                "A versão de configuração deve declarar o ato que a criou.",
                nameof(atoCriadorId));
        }

        if (!HashCanonicalComputer.IsValidHashShape(atoCriadorHash))
        {
            throw new ArgumentException(
                "O hash do documento do ato criador deve ser um SHA-256 em hexadecimal minúsculo (64 caracteres).",
                nameof(atoCriadorHash));
        }
    }
}

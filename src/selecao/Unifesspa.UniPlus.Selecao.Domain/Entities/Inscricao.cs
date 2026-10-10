namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Events;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A inscrição de um candidato num processo seletivo (issue #1858, UNI-REQ-0023): uma por CPF e
/// processo. Nasce <see cref="SituacaoInscricao.Rascunho"/> ao <see cref="Iniciar"/> e passa a
/// <see cref="SituacaoInscricao.Submetida"/> na primeira <see cref="Submeter"/> — não existe
/// terceiro estado para "corrigida": uma submissão seguinte é uma correção da mesma inscrição, com
/// comprovante novo, e a anterior não muda (ela é <see cref="ComprovanteInscricao"/>, append-only).
/// </summary>
/// <remarks>
/// <para>
/// <b>A versão do formulário é fixada ao iniciar</b> (UNI-REQ-0084) e nunca muda depois — uma
/// retificação publicada em seguida não altera uma inscrição em curso. A leitura e a validação da
/// inscrição usam sempre <see cref="VersaoFormularioId"/>, nunca a versão vigente por relógio.
/// </para>
/// <para>
/// <b><see cref="Respostas"/> não separa números de documento.</b> O domínio guarda todas as
/// respostas juntas, pelo código do fato; é o repositório, na fronteira (ADR-0121), que decide —
/// a partir de <see cref="FatosIdentificadorDeclarados"/>, congelado aqui no momento de
/// <see cref="Iniciar"/> — quais chaves vão para o envelope cifrado e quais para o jsonb em claro.
/// O domínio não precisa saber de cifra para ficar correto.
/// </para>
/// <para>
/// <b>Concorrência (ADR-0119).</b> <see cref="Revisao"/> é o token de concorrência: a Infrastructure
/// o mapeia como <c>IsConcurrencyToken</c>, então uma corrida de verdade (duas gravações que leram a
/// mesma revisão) estoura <c>DbUpdateConcurrencyException</c> no <c>SaveChanges</c>, mesmo que
/// <see cref="ConferirPrecondicao"/> — chamado ANTES de mutar, para devolver um <b>412</b> limpo em
/// vez de depender só da exceção — tenha passado com dados já defasados entre a leitura do handler e
/// o commit.
/// </para>
/// </remarks>
public sealed class Inscricao : EntityBase
{
    public Guid ProcessoSeletivoId { get; private set; }

    public Cpf Cpf { get; private set; } = null!;

    /// <summary>O <c>sub</c> do token de quem iniciou — a inscrição nunca troca de dono.</summary>
    public string Sub { get; private set; } = string.Empty;

    public string EmissorToken { get; private set; } = string.Empty;

    public SituacaoInscricao Situacao { get; private set; }

    public Guid VersaoFormularioId { get; private set; }

    public Guid? OfertaCursoId { get; private set; }

    /// <summary>Gerado na primeira submissão (sequência do banco, formatada); nulo em rascunho.</summary>
    public string? NumeroInscricao { get; private set; }

    public IReadOnlyDictionary<string, JsonElement> Respostas { get; private set; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> Grupos { get; private set; } =
        new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal);

    public IReadOnlyList<AceiteDeTermo> Aceites { get; private set; } = [];

    /// <summary>
    /// Os códigos de fato cuja classificação de proteção, na versão fixada, é <c>IDENTIFICADOR</c>
    /// (congelada na publicação — issue #1857). Decide o que o repositório separa do jsonb.
    /// </summary>
    public IReadOnlySet<string> FatosIdentificadorDeclarados { get; private set; } = new HashSet<string>(StringComparer.Ordinal);

    private readonly List<ComprovanteInscricao> _comprovantes = [];

    public IReadOnlyList<ComprovanteInscricao> Comprovantes => _comprovantes.AsReadOnly();

    /// <summary>
    /// Nasce em <b>1</b> e é incrementada por toda mutação aceita (<see cref="Salvar"/> e
    /// <see cref="Submeter"/>) — token de concorrência, ver os comentários da classe.
    /// </summary>
    public int Revisao { get; private set; }

    private Inscricao() { }

    /// <summary>Cria o rascunho, fixando a versão do formulário divulgada no instante do início.</summary>
    public static Result<Inscricao> Iniciar(
        Guid processoSeletivoId,
        Cpf cpf,
        string sub,
        string emissorToken,
        Guid versaoFormularioId,
        IReadOnlySet<string> fatosIdentificadorDeclarados)
    {
        ArgumentNullException.ThrowIfNull(cpf);
        ArgumentNullException.ThrowIfNull(fatosIdentificadorDeclarados);

        if (string.IsNullOrWhiteSpace(sub))
        {
            return Result<Inscricao>.Failure(new DomainError(
                InscricaoErrorCodes.SubObrigatorio,
                "O token precisa trazer o claim sub para iniciar a inscrição."));
        }

        if (string.IsNullOrWhiteSpace(emissorToken))
        {
            return Result<Inscricao>.Failure(new DomainError(
                InscricaoErrorCodes.EmissorObrigatorio,
                "O token precisa trazer o emissor para iniciar a inscrição."));
        }

        return Result<Inscricao>.Success(new Inscricao
        {
            ProcessoSeletivoId = processoSeletivoId,
            Cpf = cpf,
            Sub = sub,
            EmissorToken = emissorToken,
            Situacao = SituacaoInscricao.Rascunho,
            VersaoFormularioId = versaoFormularioId,
            FatosIdentificadorDeclarados = fatosIdentificadorDeclarados,
            Revisao = 1,
        });
    }

    /// <summary>
    /// Reconstrói a inscrição a partir do estado persistido — nunca "cria": quem chama (o
    /// repositório de Infrastructure, por isso <see langword="public"/> — Domain não dá
    /// <c>InternalsVisibleTo</c> a Infrastructure neste módulo) já tem os valores que a gravação
    /// produziu, <see cref="Kernel.Domain.Entities.EntityBase.CreatedAt"/>/
    /// <see cref="Kernel.Domain.Entities.EntityBase.UpdatedAt"/> inclusos. Nunca chamar para
    /// construir uma inscrição nova — isso é <see cref="Iniciar"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Existe porque <see cref="Inscricao"/> precisa do mesmo desenho de tradução record↔domínio de
    /// <c>VinculoDiscenteRepository</c> (ADR-0121, cifra na fronteira) — e, diferente de
    /// <c>VinculoDiscente</c>, herda <see cref="Kernel.Domain.Entities.EntityBase"/>: os setters de
    /// <c>CreatedAt</c>/<c>Id</c> são <c>protected</c>/<c>protected init</c>, inacessíveis a uma
    /// classe de Infrastructure. Esta factory, por viver dentro do corpo de <see cref="Inscricao"/>,
    /// pode atribuí-los — mesmo padrão de <c>VinculoDiscente.Reidratar</c>.
    /// </para>
    /// <para>
    /// Não recebe os <see cref="Comprovantes"/> já emitidos: nenhuma mutação do agregado precisa
    /// deles em memória (<see cref="Submeter"/> decide a primeira submissão por
    /// <see cref="NumeroInscricao"/>, não pela contagem de comprovantes), e a leitura de um
    /// comprovante específico é por consulta direta (<c>GET .../comprovantes/{n}</c>), não pelo
    /// agregado inteiro — mesmo raciocínio de <c>ObrigatoriedadeLegalHistorico</c>, nunca
    /// recarregado no agregado que o gerou.
    /// </para>
    /// </remarks>
    public static Inscricao Reidratar(
        Guid id,
        Guid processoSeletivoId,
        Cpf cpf,
        string sub,
        string emissorToken,
        SituacaoInscricao situacao,
        Guid versaoFormularioId,
        Guid? ofertaCursoId,
        string? numeroInscricao,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlyList<AceiteDeTermo> aceites,
        IReadOnlySet<string> fatosIdentificadorDeclarados,
        int revisao,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt)
    {
        ArgumentNullException.ThrowIfNull(cpf);
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(aceites);
        ArgumentNullException.ThrowIfNull(fatosIdentificadorDeclarados);

        return new Inscricao
        {
            Id = id,
            ProcessoSeletivoId = processoSeletivoId,
            Cpf = cpf,
            Sub = sub,
            EmissorToken = emissorToken,
            Situacao = situacao,
            VersaoFormularioId = versaoFormularioId,
            OfertaCursoId = ofertaCursoId,
            NumeroInscricao = numeroInscricao,
            Respostas = respostas,
            Grupos = grupos,
            Aceites = aceites,
            FatosIdentificadorDeclarados = fatosIdentificadorDeclarados,
            Revisao = revisao,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };
    }

    /// <summary>O ETag forte da revisão corrente — identidade da inscrição dentro dele (mesmo motivo do ETag de <see cref="RascunhoRetificacao"/>: ABA sem ela).</summary>
    public string ETag => $"\"{Id}:{Revisao}\"";

    /// <summary>Confere o <c>If-Match</c> contra o <see cref="ETag"/> corrente — <see langword="null"/> quando a mutação pode prosseguir.</summary>
    public DomainError? ConferirPrecondicao(PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        if (!precondicao.Presente)
        {
            return new DomainError(
                InscricaoErrorCodes.PrecondicaoRequerida,
                "Informe o If-Match com o ETag da inscrição antes de salvar ou enviar.");
        }

        if (!precondicao.Casa(ETag))
        {
            return new DomainError(
                InscricaoErrorCodes.PrecondicaoFalhou,
                "O If-Match informado não corresponde ao estado atual da inscrição — releia e refaça a operação.");
        }

        return null;
    }

    /// <summary>
    /// O token tem de pertencer a quem abriu a inscrição — <c>sub</c> diferente do gravado para o
    /// mesmo CPF nunca é "outra inscrição", é identidade trocada.
    /// </summary>
    public DomainError? ConferirDono(string sub)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);

        return string.Equals(Sub, sub, StringComparison.Ordinal)
            ? null
            : new DomainError(InscricaoErrorCodes.DonoDivergente, "Esta inscrição pertence a outro candidato.");
    }

    /// <summary>
    /// As respostas de nível superior após o mesmo merge que <see cref="Salvar"/> e
    /// <see cref="Submeter"/> aplicam internamente — ausência no corpo recebido preserva o valor já
    /// gravado, nunca apaga. O veredito de submissão (issue #1859) avalia sobre este resultado, não
    /// sobre o corpo recebido isolado: o candidato pode ter preenchido um campo numa chamada
    /// anterior e não reenviá-lo nesta, e o veredito precisa refletir o estado que será persistido.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> RespostasAposMesclar(IReadOnlyDictionary<string, JsonElement> recebida)
    {
        ArgumentNullException.ThrowIfNull(recebida);
        return MesclarPreservandoAusentes(Respostas, recebida);
    }

    /// <summary>
    /// Salva o rascunho: validação estrutural, sem exigir obrigatórios — a validação completa é da
    /// submissão (issue #1859). Preserva um número de documento ausente do corpo: ausência nunca
    /// apaga, só um valor enviado substitui.
    /// </summary>
    public Result Salvar(
        Guid? ofertaCursoId,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlyList<AceiteDeTermo> aceites,
        PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(aceites);

        if (ConferirPrecondicao(precondicao) is { } erroPrecondicao)
        {
            return Result.Failure(erroPrecondicao);
        }

        OfertaCursoId = ofertaCursoId;
        Respostas = MesclarPreservandoAusentes(Respostas, respostas);
        Grupos = grupos;
        Aceites = aceites;
        Revisao++;

        return Result.Success();
    }

    /// <summary>
    /// Envia ou corrige: gera o número de inscrição na primeira vez (a sequência vem de fora — o
    /// domínio não acessa o banco), muda a situação para <see cref="SituacaoInscricao.Submetida"/> e
    /// grava um <see cref="ComprovanteInscricao"/> novo, sem tocar nos anteriores. A validação
    /// completa (obrigatórios, restrições, impedimentos, termos, Geo) é da issue #1859 — aqui só o
    /// ciclo de vida do agregado.
    /// </summary>
    public Result<ComprovanteInscricao> Submeter(
        string numeroGeradoSeNecessario,
        Guid? ofertaCursoId,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlyList<AceiteDeTermo> aceites,
        string hashIntegridade,
        PrecondicaoIfMatch precondicao,
        DateTimeOffset instante)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numeroGeradoSeNecessario);
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(aceites);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashIntegridade);

        if (ConferirPrecondicao(precondicao) is { } erroPrecondicao)
        {
            return Result<ComprovanteInscricao>.Failure(erroPrecondicao);
        }

        bool primeiraSubmissao = NumeroInscricao is null;
        NumeroInscricao ??= numeroGeradoSeNecessario;

        OfertaCursoId = ofertaCursoId;
        Respostas = MesclarPreservandoAusentes(Respostas, respostas);
        Grupos = grupos;
        Aceites = aceites;
        Situacao = SituacaoInscricao.Submetida;
        Revisao++;

        ComprovanteInscricao comprovante = ComprovanteInscricao.Emitir(
            Id, NumeroInscricao, instante, hashIntegridade, OfertaCursoId,
            SemNumerosDeDocumento(Respostas), SemNumerosDeDocumentoNosGrupos(Grupos), Aceites);
        _comprovantes.Add(comprovante);

        AddDomainEvent(primeiraSubmissao
            ? (IDomainEvent)new InscricaoSubmetidaEvent(Id, ProcessoSeletivoId, NumeroInscricao, instante)
            : new InscricaoCorrigidaEvent(Id, ProcessoSeletivoId, NumeroInscricao, instante));

        return Result<ComprovanteInscricao>.Success(comprovante);
    }

    /// <summary>
    /// Um número de documento ausente do corpo preserva o valor gravado — ausência nunca apaga, só
    /// um valor enviado substitui. As demais chaves da entrada sempre valem, inclusive para
    /// apagar uma resposta que o candidato limpou.
    /// </summary>
    private static Dictionary<string, JsonElement> MesclarPreservandoAusentes(
        IReadOnlyDictionary<string, JsonElement> atual,
        IReadOnlyDictionary<string, JsonElement> recebida)
    {
        Dictionary<string, JsonElement> mesclado = new(atual, StringComparer.Ordinal);
        foreach ((string codigo, JsonElement valor) in recebida)
        {
            mesclado[codigo] = valor;
        }

        return mesclado;
    }

    /// <summary>
    /// O comprovante nunca carrega número de documento (§4.1 do plano da #1855) — o domínio aplica
    /// esta invariante por si, independente do que a Infrastructure faça com o jsonb de respostas.
    /// </summary>
    private Dictionary<string, JsonElement> SemNumerosDeDocumento(IReadOnlyDictionary<string, JsonElement> respostas) =>
        respostas
            .Where(r => !FatosIdentificadorDeclarados.Contains(r.Key))
            .ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal);

    /// <summary>
    /// A mesma exclusão de <see cref="SemNumerosDeDocumento"/>, aplicada às respostas de cada
    /// ocorrência dentro de cada grupo — um grupo repetível também pode coletar um fato
    /// <c>Identificador</c> por membro (ex.: documento de um dependente).
    /// </summary>
    private Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> SemNumerosDeDocumentoNosGrupos(
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos) =>
        grupos.ToDictionary(
            static g => g.Key,
            g => (IReadOnlyList<OcorrenciaDeGrupoInscricao>)
                [.. g.Value.Select(o => new OcorrenciaDeGrupoInscricao(o.Id, SemNumerosDeDocumento(o.Respostas)))],
            StringComparer.Ordinal);
}

/// <summary>Uma ocorrência de grupo repetível da inscrição, pelo id estável do membro.</summary>
public sealed record OcorrenciaDeGrupoInscricao(string Id, IReadOnlyDictionary<string, JsonElement> Respostas);

/// <summary>O aceite de um termo, com a versão e o hash que a inscrição carrega da versão fixada.</summary>
public sealed record AceiteDeTermo(string Codigo, Guid VersaoId, string HashVersao);

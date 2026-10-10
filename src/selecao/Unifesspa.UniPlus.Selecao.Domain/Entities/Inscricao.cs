namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Events;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
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
/// <b>Cada valor gravado tem definição.</b> <see cref="DefinicoesDasRespostas"/> diz o tipo de cada fato respondido e se
/// ele é número de documento; um fato sem definição na versão fixada, ou um valor fora do tipo dela, é
/// recusado antes de mudar o agregado. O repositório cifra os números de documento na fronteira
/// (ADR-0121), guiado pela definição.
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

    /// <summary>O que o candidato digitou ou selecionou.</summary>
    public IReadOnlyDictionary<string, JsonElement> Respostas { get; private set; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    /// <summary>O que o sistema calculou das respostas na submissão, como a UF a partir do endereço.</summary>
    public IReadOnlyDictionary<string, JsonElement> Derivados { get; private set; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> Grupos { get; private set; } =
        new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal);

    public IReadOnlyList<AceiteDeTermo> Aceites { get; private set; } = [];

    public IReadOnlyDictionary<string, DefinicaoDaResposta> DefinicoesDasRespostas { get; private set; } =
        new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal);

    private const string CampoDaOferta = "ofertaCursoId";

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
        Guid versaoFormularioId)
    {
        ArgumentNullException.ThrowIfNull(cpf);

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
        IReadOnlyDictionary<string, JsonElement> derivados,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlyList<AceiteDeTermo> aceites,
        IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes,
        int revisao,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt)
    {
        ArgumentNullException.ThrowIfNull(cpf);
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(derivados);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(aceites);
        ArgumentNullException.ThrowIfNull(definicoes);

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
            Derivados = derivados,
            Grupos = grupos,
            Aceites = aceites,
            DefinicoesDasRespostas = definicoes,
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

    /// <summary>Depois do envio, a inscrição só muda por novo envio, que emite novo comprovante.</summary>
    public DomainError? ConferirAindaEmRascunho() =>
        Situacao == SituacaoInscricao.Submetida
            ? new DomainError(
                InscricaoErrorCodes.RascunhoSoAntesDoEnvio,
                "Sua inscrição já foi enviada. Para corrigi-la, envie de novo: cada envio gera um novo comprovante.")
            : null;

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
    /// apaga, só um valor enviado substitui. Depois do envio, a inscrição que vale é a enviada
    /// (UNI-REQ-0132): só um novo envio, com novo comprovante, a muda.
    /// </summary>
    public Result Salvar(
        Guid? ofertaCursoId,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlyList<AceiteDeTermo> aceites,
        RespostasAdmitidas admitidas,
        PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(aceites);
        ArgumentNullException.ThrowIfNull(admitidas);

        if (ConferirPrecondicao(precondicao) is { } erroPrecondicao)
        {
            return Result.Failure(erroPrecondicao);
        }

        if (ConferirAindaEmRascunho() is { } erroRascunho)
        {
            return Result.Failure(erroRascunho);
        }

        List<FieldError> recusas =
        [
            .. ConferirValores(respostas, grupos, admitidas.Definicoes),
            .. RecusasDoCpf(respostas),
            .. RecusasDaOferta(ofertaCursoId, admitidas.OfertasDeCurso, exigida: false),
        ];
        if (recusas.Count > 0)
        {
            return Result.ValidationFailure(recusas);
        }

        OfertaCursoId = ofertaCursoId;
        Respostas = MesclarPreservandoAusentes(Respostas, respostas);
        Grupos = SemValoresNulos(grupos);
        Aceites = aceites;
        DefinicoesDasRespostas = admitidas.Definicoes;
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
    /// <param name="derivados">Substituem os da submissão anterior; o candidato nunca os informa.</param>
    public Result<ComprovanteInscricao> Submeter(
        string numeroGeradoSeNecessario,
        Guid? ofertaCursoId,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlyList<AceiteDeTermo> aceites,
        RespostasAdmitidas admitidas,
        IReadOnlyDictionary<string, JsonElement> derivados,
        IReadOnlyDictionary<string, DefinicaoDaResposta> definicoesDosDerivados,
        PrecondicaoIfMatch precondicao,
        DateTimeOffset instante)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numeroGeradoSeNecessario);
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(derivados);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(aceites);
        ArgumentNullException.ThrowIfNull(admitidas);
        ArgumentNullException.ThrowIfNull(definicoesDosDerivados);

        if (ConferirPrecondicao(precondicao) is { } erroPrecondicao)
        {
            return Result<ComprovanteInscricao>.Failure(erroPrecondicao);
        }

        IReadOnlyList<FieldError> recusas =
        [
            .. ConferirValores(respostas, grupos, admitidas.Definicoes),
            .. ConferirValores(derivados, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), definicoesDosDerivados),
            .. RecusasDoCpf(respostas),
            .. RecusasDaOferta(ofertaCursoId, admitidas.OfertasDeCurso, exigida: admitidas.OfertasDeCurso.Count > 0),
        ];
        if (recusas.Count > 0)
        {
            return Result<ComprovanteInscricao>.ValidationFailure(recusas);
        }

        bool primeiraSubmissao = NumeroInscricao is null;
        NumeroInscricao ??= numeroGeradoSeNecessario;

        OfertaCursoId = ofertaCursoId;
        Derivados = MesclarPreservandoAusentes(new Dictionary<string, JsonElement>(StringComparer.Ordinal), derivados);
        Respostas = MesclarPreservandoAusentes(Respostas, respostas)
            .Where(r => !Derivados.ContainsKey(r.Key))
            .ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal);
        Grupos = SemValoresNulos(grupos);
        Aceites = aceites;
        DefinicoesDasRespostas = admitidas.Definicoes.Concat(definicoesDosDerivados)
            .GroupBy(static f => f.Key, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.Last().Value, StringComparer.Ordinal);
        Situacao = SituacaoInscricao.Submetida;
        Revisao++;

        ComprovanteInscricao comprovante = ComprovanteInscricao.Emitir(
            Id, NumeroInscricao, instante, OfertaCursoId,
            SemNumerosDeDocumento(Respostas), SemNumerosDeDocumento(Derivados), SemNumerosDeDocumentoNosGrupos(Grupos), Aceites);
        _comprovantes.Add(comprovante);

        AddDomainEvent(primeiraSubmissao
            ? (IDomainEvent)new InscricaoSubmetidaEvent(Id, ProcessoSeletivoId, NumeroInscricao, instante)
            : new InscricaoCorrigidaEvent(Id, ProcessoSeletivoId, NumeroInscricao, instante));

        return Result<ComprovanteInscricao>.Success(comprovante);
    }

    /// <summary>
    /// Um número de documento ausente do corpo preserva o valor gravado — ausência nunca apaga, só
    /// um valor enviado substitui. As demais chaves da entrada sempre valem; <c>null</c> apaga a
    /// resposta que o candidato limpou.
    /// </summary>
    private static Dictionary<string, JsonElement> MesclarPreservandoAusentes(
        IReadOnlyDictionary<string, JsonElement> atual,
        IReadOnlyDictionary<string, JsonElement> recebida)
    {
        Dictionary<string, JsonElement> mesclado = new(atual, StringComparer.Ordinal);
        foreach ((string codigo, JsonElement valor) in recebida)
        {
            if (valor.ValueKind == JsonValueKind.Null)
            {
                mesclado.Remove(codigo);
            }
            else
            {
                mesclado[codigo] = valor;
            }
        }

        return mesclado;
    }

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> SemValoresNulos(
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos) =>
        grupos.ToDictionary(
            static g => g.Key,
            static g => (IReadOnlyList<OcorrenciaDeGrupoInscricao>)
                [.. g.Value.Select(static o => o with { Respostas = MesclarPreservandoAusentes(new Dictionary<string, JsonElement>(StringComparer.Ordinal), o.Respostas) })],
            StringComparer.Ordinal);

    /// <summary>
    /// Todo valor tem de ter definição e caber no tipo dela; a ocorrência de grupo, um id único dentro
    /// do grupo, porque é por ele que a ocorrência é gravada.
    /// </summary>
    private static List<FieldError> ConferirValores(
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes)
    {
        List<FieldError> recusas = [.. RecusasDeValor(respostas, definicoes, prefixo: string.Empty)];

        foreach ((string grupoCodigo, IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias) in grupos)
        {
            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (OcorrenciaDeGrupoInscricao ocorrencia in ocorrencias)
            {
                if (string.IsNullOrWhiteSpace(ocorrencia.Id) || !ids.Add(ocorrencia.Id))
                {
                    recusas.Add(new FieldError($"{grupoCodigo}.{ocorrencia.Id}", new DomainError(
                        InscricaoErrorCodes.GrupoIdDuplicado,
                        $"Cada ocorrência do grupo '{grupoCodigo}' precisa de um id próprio, não vazio e não repetido.")));
                    continue;
                }

                recusas.AddRange(RecusasDeValor(ocorrencia.Respostas, definicoes, prefixo: $"{grupoCodigo}.{ocorrencia.Id}."));
            }
        }

        return recusas;
    }

    /// <summary>A inscrição é do CPF do token; a resposta do CPF não pode ser de outra pessoa.</summary>
    private IEnumerable<FieldError> RecusasDoCpf(IReadOnlyDictionary<string, JsonElement> respostas)
    {
        if (!respostas.TryGetValue(ConjuntoBasicoDaInscricao.Cpf, out JsonElement valor) || valor.ValueKind == JsonValueKind.Null)
        {
            yield break;
        }

        bool doDono = valor.ValueKind == JsonValueKind.String
            && Cpf.Criar(valor.GetString()) is { IsSuccess: true } respondido
            && respondido.Value!.Valor == Cpf.Valor;
        if (!doDono)
        {
            yield return new FieldError(ConjuntoBasicoDaInscricao.Cpf, new DomainError(
                InscricaoErrorCodes.CpfDiferenteDoDono, "O CPF informado tem de ser o do titular da conta."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaOferta(Guid? oferta, IReadOnlySet<Guid> ofertasDeCurso, bool exigida)
    {
        if (oferta is { } escolhida && !ofertasDeCurso.Contains(escolhida))
        {
            yield return new FieldError(CampoDaOferta, new DomainError(
                InscricaoErrorCodes.OfertaForaDoProcesso, "A oferta de curso escolhida não é deste processo seletivo."));
        }
        else if (oferta is null && exigida)
        {
            yield return new FieldError(CampoDaOferta, new DomainError(
                InscricaoErrorCodes.OfertaObrigatoria, "Escolha a oferta de curso para enviar a inscrição."));
        }
    }

    private static IEnumerable<FieldError> RecusasDeValor(
        IReadOnlyDictionary<string, JsonElement> respostas, IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes, string prefixo)
    {
        foreach ((string codigo, JsonElement valor) in respostas)
        {
            if (!definicoes.TryGetValue(codigo, out DefinicaoDaResposta? definicao))
            {
                yield return new FieldError(prefixo + codigo, new DomainError(
                    InscricaoErrorCodes.FatoForaDoFormulario, $"'{codigo}' não é um campo do formulário desta inscrição."));
            }
            else if (valor.ValueKind != JsonValueKind.Null && !definicao.Aceita(valor))
            {
                yield return new FieldError(prefixo + codigo, new DomainError(
                    InscricaoErrorCodes.ValorForaDoTipo, $"A resposta de '{codigo}' não tem o tipo que o campo pede."));
            }
        }
    }

    /// <summary>
    /// O comprovante nunca carrega número de documento (§4.1 do plano da #1855) — o domínio aplica
    /// esta invariante por si, independente do que a Infrastructure faça com o jsonb de respostas.
    /// </summary>
    private Dictionary<string, JsonElement> SemNumerosDeDocumento(IReadOnlyDictionary<string, JsonElement> respostas) =>
        respostas
            .Where(r => DefinicoesDasRespostas.TryGetValue(r.Key, out DefinicaoDaResposta? definicao) && !definicao.Identificador)
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

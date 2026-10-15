namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Events;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.Services;
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

    /// <summary>A recusa de quem não tem inscrição no processo. Não cita o CPF nem o processo: vai para a resposta e para o log.</summary>
    public static DomainError NaoEncontrada { get; } = new(
        InscricaoErrorCodes.NaoEncontrada, "Você ainda não tem inscrição neste processo seletivo.");

    /// <summary>Depois do envio, a inscrição só muda por novo envio, que emite novo comprovante.</summary>
    public DomainError? ConferirAindaEmRascunho() =>
        Situacao == SituacaoInscricao.Submetida
            ? new DomainError(
                InscricaoErrorCodes.RascunhoSoAntesDoEnvio,
                "Sua inscrição já foi enviada. Para corrigi-la, envie de novo: cada envio gera um novo comprovante.")
            : null;

    /// <summary>
    /// O token tem de pertencer a quem abriu a inscrição — <c>sub</c> diferente do gravado para o
    /// mesmo CPF nunca é "outra inscrição", é identidade trocada. Sem <c>sub</c>, não há dono a
    /// conferir: a recusa é a mesma do iniciar.
    /// </summary>
    public DomainError? ConferirDono(string? sub)
    {
        if (string.IsNullOrWhiteSpace(sub))
        {
            return new DomainError(InscricaoErrorCodes.SubObrigatorio, "O token precisa trazer o claim sub para acessar a inscrição.");
        }

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
    /// Os grupos repetíveis após o mesmo merge que <see cref="Salvar"/> e <see cref="Submeter"/>
    /// aplicam internamente: o grupo ausente do corpo recebido preserva as ocorrências gravadas, e o
    /// grupo enviado, mesmo com a lista vazia, substitui as dele. O veredito de submissão avalia
    /// sobre este resultado, como avalia as respostas de <see cref="RespostasAposMesclar"/>.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> GruposAposMesclar(
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> recebidos)
    {
        ArgumentNullException.ThrowIfNull(recebidos);
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> mesclado = new(Grupos, StringComparer.Ordinal);
        foreach ((string codigo, IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias) in recebidos)
        {
            mesclado[codigo] = ocorrencias;
        }

        return mesclado;
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
            .. ConferirRespostas(respostas, grupos, admitidas),
            .. RecusasDoCpf(respostas),
            .. RecusasDaOferta(ofertaCursoId, admitidas.OfertasDeCurso, exigida: false),
            .. RecusasDosAceites(aceites, admitidas.Termos),
        ];
        if (recusas.Count > 0)
        {
            return Result.ValidationFailure(recusas);
        }

        OfertaCursoId = ofertaCursoId;
        Respostas = NaFormaCanonica(MesclarPreservandoAusentes(Respostas, respostas), admitidas.Definicoes);
        Grupos = NaFormaCanonicaNosGrupos(SemValoresNulos(GruposAposMesclar(grupos)), admitidas.Definicoes);
        Aceites = aceites;
        DefinicoesDasRespostas = admitidas.Definicoes;
        Revisao++;

        return Result.Success();
    }

    /// <summary>
    /// Envia ou corrige: gera o número de inscrição na primeira vez (a sequência vem de fora — o
    /// domínio não acessa o banco), muda a situação para <see cref="SituacaoInscricao.Submetida"/> e
    /// grava um <see cref="ComprovanteInscricao"/> novo, sem tocar nos anteriores. A validação
    /// completa (obrigatórios, restrições, impedimentos, termos, Geo) é da issue #1859; aqui ficam o
    /// ciclo de vida do agregado e a modalidade em que o candidato concorre.
    /// </summary>
    /// <remarks>
    /// A inscrição enviada guarda só o que se aplica a ela. A resposta a um item que a
    /// <paramref name="avaliacao"/> dá como não aplicável — a seção ou o item não aparece diante das
    /// demais respostas, como a condição de atendimento depois de o candidato dizer que não precisa
    /// de atendimento — é descartada, junto com a do subitem oculto e a do grupo oculto inteiro:
    /// ela não vale para o processo e seria dado pessoal guardado sem finalidade. O descarte não
    /// recusa o envio. O rascunho (<see cref="Salvar"/>) continua guardando a resposta oculta, para o
    /// candidato poder voltar atrás.
    /// <para>
    /// Quando a versão fixada deriva a modalidade, o envio só sai se o candidato concorre a alguma das
    /// ofertadas (UNI-REQ-0076). A modalidade derivada é gravada com os derivados, e o comprovante a
    /// registra com o nome congelado na publicação.
    /// </para>
    /// </remarks>
    /// <param name="avaliacao">A avaliação da inscrição sobre o estado final dela, com todas as etapas da inscrição concluídas.</param>
    /// <param name="derivados">Substituem os da submissão anterior; o candidato nunca os informa.</param>
    public Result<ComprovanteInscricao> Submeter(
        string numeroGeradoSeNecessario,
        Guid? ofertaCursoId,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlyList<AceiteDeTermo> aceites,
        RespostasAdmitidas admitidas,
        ResultadoDaAvaliacaoDaInscricao avaliacao,
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
        ArgumentNullException.ThrowIfNull(avaliacao);
        ArgumentNullException.ThrowIfNull(definicoesDosDerivados);

        if (ConferirPrecondicao(precondicao) is { } erroPrecondicao)
        {
            return Result<ComprovanteInscricao>.Failure(erroPrecondicao);
        }

        IReadOnlyList<FieldError> recusas =
        [
            .. ConferirRespostas(respostas, grupos, admitidas),
            .. ConferirValores(derivados, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), definicoesDosDerivados, new Dictionary<string, IReadOnlySet<string>>()),
            .. RecusasDoCpf(respostas),
            .. RecusasDaOferta(ofertaCursoId, admitidas.OfertasDeCurso, exigida: admitidas.OfertasDeCurso.Count > 0),
            .. RecusasDaModalidade(avaliacao.Modalidades),
            .. RecusasDosAceites(aceites, admitidas.Termos),
        ];
        if (recusas.Count > 0)
        {
            return Result<ComprovanteInscricao>.ValidationFailure(recusas);
        }

        ModalidadesDeConcorrencia modalidades = avaliacao.Modalidades;
        bool primeiraSubmissao = NumeroInscricao is null;
        NumeroInscricao ??= numeroGeradoSeNecessario;

        OfertaCursoId = ofertaCursoId;
        Dictionary<string, JsonElement> derivadosDoEnvio = MesclarPreservandoAusentes(new Dictionary<string, JsonElement>(StringComparer.Ordinal), derivados);
        Dictionary<string, DefinicaoDaResposta> definicoesDoEnvio = new(definicoesDosDerivados, StringComparer.Ordinal);
        if (modalidades.Derivada)
        {
            derivadosDoEnvio[AvaliacaoDaInscricao.FatoDaModalidade] = JsonSerializer.SerializeToElement(modalidades.Itens.Select(static m => m.Codigo));
            definicoesDoEnvio[AvaliacaoDaInscricao.FatoDaModalidade] = AvaliacaoDaInscricao.DefinicaoDaModalidade;
        }

        Derivados = derivadosDoEnvio;
        Respostas = SoAsQueSeAplicam(NaFormaCanonica(MesclarPreservandoAusentes(Respostas, respostas), admitidas.Definicoes), avaliacao.Formulario.Fatos)
            .Where(r => !Derivados.ContainsKey(r.Key))
            .ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal);
        Grupos = GruposQueSeAplicam(NaFormaCanonicaNosGrupos(SemValoresNulos(GruposAposMesclar(grupos)), admitidas.Definicoes), avaliacao.Formulario.Grupos);
        Aceites = aceites;
        DefinicoesDasRespostas = admitidas.Definicoes.Concat(definicoesDoEnvio)
            .GroupBy(static f => f.Key, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.Last().Value, StringComparer.Ordinal);
        Situacao = SituacaoInscricao.Submetida;
        Revisao++;

        // A modalidade vai ao comprovante na seção própria, com o nome; nos derivados, ficaria só o código.
        ComprovanteInscricao comprovante = ComprovanteInscricao.Emitir(
            Id, NumeroInscricao, instante, OfertaCursoId,
            SemNumerosDeDocumento(Respostas),
            SemNumerosDeDocumento(Derivados).Where(static d => d.Key != AvaliacaoDaInscricao.FatoDaModalidade)
                .ToDictionary(static d => d.Key, static d => d.Value, StringComparer.Ordinal),
            SemNumerosDeDocumentoNosGrupos(Grupos), Aceites,
            modalidades.Derivada ? modalidades.Itens : null);
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

    /// <summary>Cada resposta como é gravada, na forma canônica do formato do campo (<see cref="DefinicaoDaResposta.NaFormaCanonica"/>).</summary>
    private static Dictionary<string, JsonElement> NaFormaCanonica(
        IReadOnlyDictionary<string, JsonElement> respostas, IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes) =>
        respostas.ToDictionary(
            static r => r.Key,
            r => definicoes.TryGetValue(r.Key, out DefinicaoDaResposta? definicao) ? definicao.NaFormaCanonica(r.Value) : r.Value,
            StringComparer.Ordinal);

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> NaFormaCanonicaNosGrupos(
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos, IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes) =>
        grupos.ToDictionary(
            static g => g.Key,
            g => (IReadOnlyList<OcorrenciaDeGrupoInscricao>)
                [.. g.Value.Select(o => o with { Respostas = NaFormaCanonica(o.Respostas, definicoes) })],
            StringComparer.Ordinal);

    /// <summary>As respostas cujo fato a avaliação não dá como não aplicável; a de fato que ela não conhece fica.</summary>
    private static Dictionary<string, JsonElement> SoAsQueSeAplicam(
        IReadOnlyDictionary<string, JsonElement> respostas, IReadOnlyDictionary<string, FatoResolvido> fatos) =>
        respostas
            .Where(r => !(fatos.TryGetValue(r.Key, out FatoResolvido? fato) && fato.Estado == EstadoFato.NaoAplicavel))
            .ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal);

    /// <summary>
    /// Os grupos que se aplicam, cada ocorrência só com as respostas dos subitens que aparecem nela. Os
    /// ids das ocorrências já foram conferidos como únicos por <see cref="ConferirValores"/>.
    /// </summary>
    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> GruposQueSeAplicam(
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos, IReadOnlyList<AvaliacaoGrupo> avaliados)
    {
        Dictionary<string, AvaliacaoGrupo> avaliadoPorCodigo = avaliados.ToDictionary(static g => g.Codigo, StringComparer.Ordinal);
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> aplicaveis = new(StringComparer.Ordinal);
        foreach ((string codigo, IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias) in grupos)
        {
            if (!avaliadoPorCodigo.TryGetValue(codigo, out AvaliacaoGrupo? avaliado))
            {
                aplicaveis[codigo] = ocorrencias;
                continue;
            }

            if (avaliado.Estado == EstadoFato.NaoAplicavel)
            {
                continue;
            }

            Dictionary<string, AvaliacaoOcorrencia> avaliadaPorId = avaliado.Ocorrencias.ToDictionary(static o => o.Id, StringComparer.Ordinal);
            aplicaveis[codigo] = [.. ocorrencias.Select(o => avaliadaPorId.TryGetValue(o.Id, out AvaliacaoOcorrencia? avaliada)
                ? o with { Respostas = SoAsQueSeAplicam(o.Respostas, avaliada.Fatos) }
                : o)];
        }

        return aplicaveis;
    }

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> SemValoresNulos(
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos) =>
        grupos.ToDictionary(
            static g => g.Key,
            static g => (IReadOnlyList<OcorrenciaDeGrupoInscricao>)
                [.. g.Value.Select(static o => o with { Respostas = MesclarPreservandoAusentes(new Dictionary<string, JsonElement>(StringComparer.Ordinal), o.Respostas) })],
            StringComparer.Ordinal);

    /// <summary>
    /// As respostas do candidato: as de nível superior só podem ser de fato que a inscrição coleta fora
    /// de grupo, e as de grupo, de subitem do grupo. O fato que existe só como subitem de grupo não é
    /// admitido no nível superior, porque ali nenhuma ocorrência o reclama e ele seria gravado, e
    /// entraria no comprovante, fora do escopo a que pertence. O que o formulário não conhece segue
    /// recusado por <see cref="ConferirValores"/>.
    /// </summary>
    private static List<FieldError> ConferirRespostas(
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        RespostasAdmitidas admitidas)
    {
        bool SoDeGrupo(string fato) => admitidas.Definicoes.ContainsKey(fato) && !admitidas.DoNivelSuperior.Contains(fato);

        List<FieldError> recusas = [.. respostas.Keys
            .Where(SoDeGrupo)
            .Select(fato => new FieldError(fato, new DomainError(
                InscricaoErrorCodes.FatoSoDeGrupo,
                $"'{fato}' é um campo de grupo e só se responde dentro de uma ocorrência do grupo. Remova-o das respostas de nível superior e informe-o na ocorrência.")))];

        recusas.AddRange(ConferirValores(
            respostas.Where(r => !SoDeGrupo(r.Key)).ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal),
            grupos,
            admitidas.Definicoes,
            admitidas.SubitensPorGrupo));
        return recusas;
    }

    /// <summary>
    /// Todo valor tem de ter definição e caber no tipo dela. A ocorrência de grupo tem de ser de um grupo
    /// do formulário, com um id único dentro do grupo, porque é por ele que a ocorrência é gravada, e só
    /// responde os subitens desse grupo.
    /// </summary>
    private static List<FieldError> ConferirValores(
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes,
        IReadOnlyDictionary<string, IReadOnlySet<string>> subitensPorGrupo)
    {
        List<FieldError> recusas = [.. RecusasDeValor(respostas, definicoes, prefixo: string.Empty)];

        foreach ((string grupoCodigo, IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias) in grupos)
        {
            if (!subitensPorGrupo.TryGetValue(grupoCodigo, out IReadOnlySet<string>? subitens))
            {
                recusas.Add(new FieldError(grupoCodigo, new DomainError(
                    InscricaoErrorCodes.GrupoForaDoFormulario,
                    $"'{grupoCodigo}' não é um grupo do formulário desta inscrição. Remova-o das respostas.")));
                continue;
            }

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

                string prefixo = $"{grupoCodigo}.{ocorrencia.Id}.";
                recusas.AddRange(ocorrencia.Respostas.Keys
                    .Where(fato => !subitens.Contains(fato))
                    .Select(fato => new FieldError(prefixo + fato, new DomainError(
                        InscricaoErrorCodes.FatoForaDoGrupo,
                        $"'{fato}' não é um campo do grupo '{grupoCodigo}'. Remova-o desta ocorrência e responda-o fora do grupo, se o formulário o pedir."))));
                recusas.AddRange(RecusasDeValor(
                    ocorrencia.Respostas.Where(r => subitens.Contains(r.Key)).ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal),
                    definicoes,
                    prefixo));
            }
        }

        return recusas;
    }

    /// <summary>
    /// Com a modalidade derivada, o envio exige que ela esteja decidida e não vazia. A recusa sai em
    /// cada campo de que a modalidade depende, para o candidato conferir as respostas; a modalidade que
    /// falta decidir sai nos campos que faltam responder.
    /// </summary>
    private static IEnumerable<FieldError> RecusasDaModalidade(ModalidadesDeConcorrencia modalidades)
    {
        if (!modalidades.Derivada || modalidades.Situacao == SituacaoDasModalidades.Concorre)
        {
            return [];
        }

        if (modalidades.Situacao == SituacaoDasModalidades.NaoConcorre)
        {
            DomainError nenhuma = new(
                InscricaoErrorCodes.NenhumaModalidade,
                "Com estas respostas, você não concorre a nenhuma das modalidades ofertadas neste processo seletivo. "
                + "Confira as respostas indicadas. Se as respostas estão corretas, você não atende às condições de nenhuma "
                + "modalidade deste processo; consulte o edital.");
            return CamposOuAModalidade([.. modalidades.Dependencias.Fatos.Where(static f => !f.Contains('.', StringComparison.Ordinal)), .. modalidades.Dependencias.Grupos])
                .Select(campo => new FieldError(campo, nenhuma));
        }

        DomainError naoDecidida = new(
            InscricaoErrorCodes.ModalidadeNaoDecidida,
            "Ainda não é possível saber em quais modalidades você concorre. Responda os campos indicados e envie de novo.");
        return CamposOuAModalidade([.. modalidades.Pendencias.Select(static p => p.FatoCodigo)])
            .Select(campo => new FieldError(campo, naoDecidida));
    }

    private static IReadOnlyList<string> CamposOuAModalidade(IReadOnlyList<string> campos) =>
        campos.Count > 0 ? campos : [AvaliacaoDaInscricao.FatoDaModalidade];

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

    /// <summary>
    /// O comprovante registra os aceites como chegam, então cada um tem de ser de um termo que o
    /// formulário exige, na versão e no hash dele, e aparecer uma vez só. A lista pode ficar
    /// incompleta no rascunho; quem decide se um termo obrigatório foi aceito é o veredito de
    /// submissão, não este método.
    /// </summary>
    private static IEnumerable<FieldError> RecusasDosAceites(
        IReadOnlyList<AceiteDeTermo> aceites, IReadOnlyDictionary<string, TermoAdmitido> termos)
    {
        HashSet<string> vistos = new(StringComparer.Ordinal);
        for (int i = 0; i < aceites.Count; i++)
        {
            AceiteDeTermo aceite = aceites[i];
            string campo = $"aceites[{i}]";

            if (!vistos.Add(aceite.Codigo))
            {
                yield return new FieldError(campo, new DomainError(
                    InscricaoErrorCodes.AceiteRepetido,
                    $"O termo '{aceite.Codigo}' foi aceito mais de uma vez. Envie um único aceite para cada termo."));
            }
            else if (!termos.TryGetValue(aceite.Codigo, out TermoAdmitido? termo))
            {
                yield return new FieldError(campo, new DomainError(
                    InscricaoErrorCodes.AceiteForaDoFormulario,
                    $"O termo '{aceite.Codigo}' não faz parte do formulário. Recarregue a página e aceite os termos exibidos."));
            }
            else if (aceite.VersaoId != termo.VersaoId || !string.Equals(aceite.HashVersao, termo.HashVersao, StringComparison.Ordinal))
            {
                yield return new FieldError(campo, new DomainError(
                    InscricaoErrorCodes.AceiteDeOutraVersao,
                    $"O aceite do termo '{aceite.Codigo}' não é da versão que o formulário apresenta. Recarregue a página e aceite o termo exibido."));
            }
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

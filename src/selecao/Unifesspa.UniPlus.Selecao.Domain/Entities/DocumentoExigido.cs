namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Linq;
using System.Text.Json;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Exigência documental de um <see cref="ProcessoSeletivo"/> (Story #554): declara qual
/// documento é exigido, em que fase, com que aplicabilidade e sob qual gatilho DNF
/// (<see cref="Condicoes"/>, PR #896). <c>EntityBase</c> puro (sem soft-delete) — o regime
/// append-only/forense pertence a <see cref="VersaoConfiguracao"/>, não à configuração
/// viva; a coleção do agregado é substituível por inteiro
/// (<see cref="ProcessoSeletivo.DefinirDocumentosExigidos"/>), mesmo padrão de
/// <see cref="FaseCronograma"/>/<see cref="ModalidadeSelecionada"/>.
/// </summary>
/// <remarks>
/// A idade máxima de emissão/formato/tamanho é entregue por task-irmã (PR #900) — não
/// modelada aqui. A base legal 1:N (<see cref="BasesLegais"/>) chegou na PR #898 (issue #549).
/// </remarks>
public sealed class DocumentoExigido : EntityBase
{
    private static readonly string[] ConsequenciasValidas =
    [
        "ELIMINA",
        "RECLASSIFICA_AC",
        "REMOVE_VANTAGEM",
        "PENDENCIA_REENVIO",
    ];

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Fase do cronograma do mesmo processo em que o documento é exigido — NOT NULL (Story #554).</summary>
    public Guid ExigidoNaFaseId { get; private set; }

    /// <summary>Id do <c>TipoDocumento</c> vivo no momento da configuração — snapshot-copy (ADR-0061), sem FK cross-módulo.</summary>
    public Guid TipoDocumentoOrigemId { get; private set; }

    /// <summary>Código classificatório congelado — snapshot-copy.</summary>
    public string TipoDocumentoCodigo { get; private set; } = string.Empty;

    /// <summary>Rótulo legível congelado — snapshot-copy.</summary>
    public string TipoDocumentoNome { get; private set; } = string.Empty;

    /// <summary>Categoria classificatória congelada — snapshot-copy.</summary>
    public string TipoDocumentoCategoria { get; private set; } = string.Empty;

    public Aplicabilidade Aplicabilidade { get; private set; }

    public bool Obrigatorio { get; private set; }

    /// <summary>
    /// ∈ {ELIMINA, RECLASSIFICA_AC, REMOVE_VANTAGEM, PENDENCIA_REENVIO} quando presente
    /// (ADR-0074). Campo de transporte — a coerência com <c>ModalidadeSelecionada.AcaoQuandoIndeferido</c>
    /// é validada na PR #903 (CA-05), sem duplicar campo.
    /// </summary>
    public string? ConsequenciaIndeferimento { get; private set; }

    /// <summary>
    /// Escopo processo+fase — RESIDUAL (Story #920): a árvore de satisfação
    /// (<see cref="NoExigencia"/>) substitui o grupo plano para toda exigência criada a
    /// partir desta Story (<see cref="Criar"/> não aceita mais este campo — sempre
    /// <see langword="null"/> em exigência nova). A propriedade e a coluna permanecem só
    /// para <see cref="Reidratar"/> reconstruir com fidelidade um envelope publicado ANTES
    /// da Story #920 (codecs 1.0–1.3, congelados) — nunca lida pelo resolvedor novo
    /// (<see cref="Services.ResolvedorArvoreSatisfacao"/>).
    /// </summary>
    public Guid? GrupoSatisfacaoId { get; private set; }

    /// <summary>
    /// Idade máxima de emissão do ARQUIVO apresentado (Story #554, PR #900) — aviso, não
    /// bloqueio de presença; a avaliação em runtime é fora de escopo desta Story.
    /// </summary>
    public IdadeMaximaEmissao? IdadeMaximaEmissao { get; private set; }

    /// <summary>
    /// Formatos aceitos para a apresentação (Story #918) — congelado na exigência, não no
    /// <c>TipoDocumento</c>. Substitui o campo singular <c>FormatoPermitido?</c> (PR #900):
    /// agora OBRIGATÓRIO — o token <c>QUALQUER</c> substitui o antigo <see langword="null"/>
    /// "sem restrição".
    /// </summary>
    public FormatosPermitidos FormatosPermitidos { get; private set; } = null!;

    /// <summary>Tamanho máximo em bytes do arquivo apresentado (Story #554, PR #900) — congelado na exigência.</summary>
    public int? TamanhoMaximoBytes { get; private set; }

    private readonly List<CondicaoGatilho> _condicoes = [];

    /// <summary>Gatilho DNF (Story #554, PR #896) — vazia significa "sem gatilho": GERAL é sempre exigida, CONDICIONAL vazia é exigida de ninguém.</summary>
    public IReadOnlyCollection<CondicaoGatilho> Condicoes => _condicoes.AsReadOnly();

    private readonly List<DocumentoExigidoBaseLegal> _basesLegais = [];

    /// <summary>Base legal 1:N (Story #554, PR #898, ADR-0074) — embasamento rastreável e auditável da exigência; validada no gate de publicação, não na escrita.</summary>
    public IReadOnlyCollection<DocumentoExigidoBaseLegal> BasesLegais => _basesLegais.AsReadOnly();

    private DocumentoExigido() { }

    public static Result<DocumentoExigido> Criar(
        Guid exigidoNaFaseId,
        Guid tipoDocumentoOrigemId,
        string tipoDocumentoCodigo,
        string tipoDocumentoNome,
        string tipoDocumentoCategoria,
        Aplicabilidade aplicabilidade,
        bool obrigatorio,
        string? consequenciaIndeferimento,
        IReadOnlyList<CondicaoGatilho> condicoes,
        IReadOnlyList<DocumentoExigidoBaseLegal> basesLegais,
        IdadeMaximaEmissao? idadeMaximaEmissao,
        FormatosPermitidos formatosPermitidos,
        int? tamanhoMaximoBytes)
    {
        ArgumentNullException.ThrowIfNull(condicoes);
        ArgumentNullException.ThrowIfNull(basesLegais);
        ArgumentNullException.ThrowIfNull(formatosPermitidos);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDocumentoCodigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDocumentoNome);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDocumentoCategoria);

        if (exigidoNaFaseId == Guid.Empty)
        {
            throw new ArgumentException("A fase em que o documento é exigido é obrigatória.", nameof(exigidoNaFaseId));
        }

        if (tipoDocumentoOrigemId == Guid.Empty)
        {
            throw new ArgumentException("O id de origem do tipo de documento é obrigatório.", nameof(tipoDocumentoOrigemId));
        }

        if (aplicabilidade == Aplicabilidade.Nenhuma)
        {
            return Result<DocumentoExigido>.Failure(new DomainError(
                "DocumentoExigido.AplicabilidadeObrigatoria",
                "A aplicabilidade da exigência documental é obrigatória e deve ser GERAL ou CONDICIONAL."));
        }

        // Normaliza espaços-em-branco para null ANTES de validar — sem isso, " " escapa
        // da checagem de domínio (não é "presente" o bastante para reprovar, nem null o
        // bastante para DeterminaResultado() ignorar) e é persistido cru.
        string? consequenciaNormalizada = string.IsNullOrWhiteSpace(consequenciaIndeferimento)
            ? null
            : consequenciaIndeferimento.Trim();

        if (consequenciaNormalizada is not null
            && !ConsequenciasValidas.Contains(consequenciaNormalizada, StringComparer.Ordinal))
        {
            return Result<DocumentoExigido>.Failure(new DomainError(
                "DocumentoExigido.ConsequenciaIndeferimentoInvalida",
                $"Consequência de indeferimento '{consequenciaNormalizada}' inválida — esperado um de: {string.Join(", ", ConsequenciasValidas)}."));
        }

        if (tamanhoMaximoBytes is <= 0)
        {
            return Result<DocumentoExigido>.Failure(new DomainError(
                "DocumentoExigido.TamanhoMaximoBytesInvalido",
                "O tamanho máximo em bytes, quando presente, deve ser maior que zero."));
        }

        DocumentoExigido documento = new()
        {
            ExigidoNaFaseId = exigidoNaFaseId,
            TipoDocumentoOrigemId = tipoDocumentoOrigemId,
            TipoDocumentoCodigo = tipoDocumentoCodigo.Trim(),
            TipoDocumentoNome = tipoDocumentoNome.Trim(),
            TipoDocumentoCategoria = tipoDocumentoCategoria.Trim(),
            Aplicabilidade = aplicabilidade,
            Obrigatorio = obrigatorio,
            ConsequenciaIndeferimento = consequenciaNormalizada,
            IdadeMaximaEmissao = idadeMaximaEmissao,
            FormatosPermitidos = formatosPermitidos,
            TamanhoMaximoBytes = tamanhoMaximoBytes,
        };

        // CA-01 (Story #554, issue #547/#892): GERAL nunca convive com condição viva —
        // agora conectado à coleção REAL (PR #896), não mais ao parâmetro sintético da PR #895.
        DomainError? coerencia = documento.GarantirCoerenciaAplicabilidade(condicoes.Count > 0);
        if (coerencia is not null)
        {
            return Result<DocumentoExigido>.Failure(coerencia);
        }

        foreach (CondicaoGatilho condicao in condicoes)
        {
            condicao.VincularDocumentoExigido(documento.Id);
            documento._condicoes.Add(condicao);
        }

        foreach (DocumentoExigidoBaseLegal baseLegal in basesLegais)
        {
            baseLegal.VincularDocumentoExigido(documento.Id);
            documento._basesLegais.Add(baseLegal);
        }

        return Result<DocumentoExigido>.Success(documento);
    }

    /// <summary>
    /// Reidrata uma exigência a partir de uma <see cref="VersaoConfiguracao"/> congelada,
    /// <b>preservando o <see cref="EntityBase.Id"/></b> — que <see cref="Criar"/> não
    /// aceita, por decisão (Story #554, PR #903, CA-09). Diferente das demais entidades-filhas
    /// do agregado (ADR-0110 D2 — só <see cref="EtapaProcesso.Id"/> era preservado, porque
    /// só ele era referenciado por outro bloco do envelope), <c>DocumentoExigido.Id</c>
    /// passa a ser o segundo: é o <c>exigenciaId</c> que o resolvedor de exigências
    /// documentais usa para correlacionar uma apresentação à exigência certa — sem
    /// preservá-lo, cada retificação trocaria silenciosamente a identidade de exigências
    /// inalteradas, e apresentações já vinculadas ao <c>exigenciaId</c> antigo deixariam de
    /// resolver.
    /// </summary>
    /// <remarks>
    /// Reidratar não é criar: os dados vêm de um documento com peso jurídico, já validado
    /// quando foi congelado — as guardas aqui são a última linha contra erro de
    /// programação, não revalidação de negócio (essa já rodou em <see cref="Criar"/>,
    /// quando a exigência foi escrita pela primeira vez).
    /// </remarks>
    public static DocumentoExigido Reidratar(
        Guid id,
        Guid exigidoNaFaseId,
        Guid tipoDocumentoOrigemId,
        string tipoDocumentoCodigo,
        string tipoDocumentoNome,
        string tipoDocumentoCategoria,
        Aplicabilidade aplicabilidade,
        bool obrigatorio,
        string? consequenciaIndeferimento,
        Guid? grupoSatisfacaoId,
        IReadOnlyList<CondicaoGatilho> condicoes,
        IReadOnlyList<DocumentoExigidoBaseLegal> basesLegais,
        IdadeMaximaEmissao? idadeMaximaEmissao,
        FormatosPermitidos formatosPermitidos,
        int? tamanhoMaximoBytes)
    {
        ArgumentNullException.ThrowIfNull(condicoes);
        ArgumentNullException.ThrowIfNull(basesLegais);
        ArgumentNullException.ThrowIfNull(formatosPermitidos);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDocumentoCodigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDocumentoNome);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDocumentoCategoria);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A exigência reidratada deve declarar o Id congelado no envelope (CA-09).", nameof(id));
        }

        DocumentoExigido documento = new()
        {
            Id = id,
            ExigidoNaFaseId = exigidoNaFaseId,
            TipoDocumentoOrigemId = tipoDocumentoOrigemId,
            TipoDocumentoCodigo = tipoDocumentoCodigo.Trim(),
            TipoDocumentoNome = tipoDocumentoNome.Trim(),
            TipoDocumentoCategoria = tipoDocumentoCategoria.Trim(),
            Aplicabilidade = aplicabilidade,
            Obrigatorio = obrigatorio,
            ConsequenciaIndeferimento = consequenciaIndeferimento,
            GrupoSatisfacaoId = grupoSatisfacaoId,
            IdadeMaximaEmissao = idadeMaximaEmissao,
            FormatosPermitidos = formatosPermitidos,
            TamanhoMaximoBytes = tamanhoMaximoBytes,
        };

        foreach (CondicaoGatilho condicao in condicoes)
        {
            condicao.VincularDocumentoExigido(documento.Id);
            documento._condicoes.Add(condicao);
        }

        foreach (DocumentoExigidoBaseLegal baseLegal in basesLegais)
        {
            baseLegal.VincularDocumentoExigido(documento.Id);
            documento._basesLegais.Add(baseLegal);
        }

        return documento;
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// Corrige o ponteiro para a fase quando a reconciliação de <c>AplicarGrafo</c>
    /// (restauração da configuração congelada) reusa a instância VIVA de
    /// <see cref="Entities.FaseCronograma"/> em vez da decodificada (Ordem, não Id — ver
    /// <see cref="Entities.FaseCronograma.AtualizarSnapshot"/>). Sem este remapeamento, um
    /// documento congelado com <see cref="ExigidoNaFaseId"/> apontando para o Id FROZEN da
    /// fase (agora trocado pelo Id da instância viva) ficaria referenciando uma fase
    /// ausente de <c>CronogramaFases</c> após a restauração (Story #554, PR #903, achado
    /// de revisão).
    /// </summary>
    internal void RemapearFase(Guid exigidoNaFaseId) =>
        ExigidoNaFaseId = exigidoNaFaseId;

    /// <summary>
    /// Determina se a exigência "conta" para efeito de resultado — obrigatória ou com
    /// qualquer consequência de indeferimento declarada. Usado pela trava CA-01 (Story
    /// #554, issue #547: <c>CONDICIONAL</c> vazia que determina resultado bloqueia
    /// publicação) e, futuramente, pelo gate de base legal (PR #898/#549).
    /// </summary>
    public bool DeterminaResultado() => Obrigatorio || ConsequenciaIndeferimento is not null;

    /// <summary>
    /// Projeção "somente <see cref="StatusBaseLegal.Resolvido"/>" (Story #554, PR #898, CA-06)
    /// — o que a PR #903 (#548) materializa no bloco congelado; bases
    /// <see cref="StatusBaseLegal.Pendente"/> são rascunho e nunca aparecem aqui.
    /// </summary>
    public IEnumerable<DocumentoExigidoBaseLegal> BasesLegaisResolvidas() =>
        _basesLegais.Where(static b => b.Status == StatusBaseLegal.Resolvido);

    /// <summary>
    /// A exigência se aplica a um candidato com estes fatos? GERAL sempre é
    /// <see cref="Ternario.Verdadeiro"/> — o gatilho nunca é avaliado. CONDICIONAL depende
    /// do predicado DNF ternário (PR #896, Story #916): zero cláusulas vivas nunca casa com
    /// ninguém (<see cref="Ternario.Falso"/>, estado estrutural — CA-01 "exigida de
    /// ninguém"), e um fato citado que não está resolvido para este candidato produz
    /// <see cref="Ternario.Indeterminado"/>, nunca <see cref="Ternario.Falso"/> (fail-closed).
    /// Usado tanto pelo resolvedor de exigências documentais (PR #903, por candidato real)
    /// quanto pelo gate de conformidade legal (PR #903, <see cref="Services.AvaliadorConformidadeLegal"/>
    /// — por um fato sintético fixo, ex. só <c>MODALIDADE</c>, para provar cobertura
    /// incondicional de uma modalidade inteira).
    /// </summary>
    public Ternario AplicavelPara(IReadOnlyDictionary<string, JsonElement> fatosResolvidos)
    {
        ArgumentNullException.ThrowIfNull(fatosResolvidos);

        if (Aplicabilidade == Aplicabilidade.Geral)
        {
            return Ternario.Verdadeiro;
        }

        if (_condicoes.Count == 0)
        {
            return Ternario.Falso;
        }

        // O dado já foi validado quando a exigência foi escrita (CondicaoGatilho.Criar) —
        // mesmo raciocínio de CondicaoGatilho.ParaCondicaoDnf(), que esta chamada usa por
        // baixo: reidratar/reavaliar não revalida.
        PredicadoDnf predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [.. _condicoes.Select(static c => (c.Clausula, c.ParaCondicaoDnf()))]).Value!;

        return predicado.Avaliar(fatosResolvidos);
    }

    /// <summary>
    /// A exigência PODE alcançar candidatos desta modalidade — verificação ESTRUTURAL do
    /// gatilho, não factual (Story #554, PR #903, achado de revisão P2). Usada pelo gate
    /// CA-05 (<see cref="Entities.ProcessoSeletivo"/>, coerência entre
    /// <see cref="ConsequenciaIndeferimento"/> e <see cref="ModalidadeSelecionada.AcaoQuandoIndeferido"/>)
    /// no lugar de <see cref="AplicavelPara"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="AplicavelPara"/> avalia contra um dicionário de fatos completo — qualquer
    /// fato ausente vira <see langword="false"/> (<c>PredicadoDnf.Avaliar</c>). No momento da
    /// publicação, só o fato sintético <c>MODALIDADE</c> é conhecido (não há candidato real
    /// ainda); um gatilho misto como <c>FAIXA_ETARIA &gt;= 18 E MODALIDADE = PCD</c> teria a
    /// condição de <c>FAIXA_ETARIA</c> avaliada como falsa por <see cref="AplicavelPara"/>,
    /// reprovando a cláusula inteira e escondendo a modalidade PCD do gate — e, pior, um
    /// gatilho <b>só</b> sobre <c>FAIXA_ETARIA</c> (sem nenhuma condição de
    /// <c>MODALIDADE</c>) nunca alcançaria NENHUMA modalidade, isentando silenciosamente
    /// todo gatilho não-modal do CA-05. Aqui as condições sobre outros fatos são
    /// IGNORADAS (não tratadas como falsas) — só as condições sobre <c>MODALIDADE</c>
    /// decidem, por cláusula; uma cláusula sem nenhuma condição de <c>MODALIDADE</c> é
    /// modalidade-agnóstica e alcança qualquer uma.
    /// </remarks>
    public bool PodeAlcancarModalidade(string modalidadeCodigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modalidadeCodigo);

        if (Aplicabilidade == Aplicabilidade.Geral)
        {
            return true;
        }

        if (_condicoes.Count == 0)
        {
            return false;
        }

        return _condicoes
            .GroupBy(static c => c.Clausula)
            .Any(clausula => clausula
                .Where(static c => string.Equals(c.Fato, "MODALIDADE", StringComparison.Ordinal))
                .All(c => CondicaoDeModalidadeSatisfeitaPor(c.ParaCondicaoDnf(), modalidadeCodigo)));
    }

    /// <summary>
    /// Story #916: <see cref="Operador.Diferente"/>/<see cref="Operador.NaoEm"/> espelham a
    /// negação de <see cref="Operador.Igual"/>/<see cref="Operador.Em"/> — sem os dois braços
    /// novos, um gatilho <c>MODALIDADE DIFERENTE X</c>/<c>NAO_EM [...]</c> seria aceito pelo
    /// validador (matriz operador × domínio) mas escaparia silenciosamente do gate estrutural
    /// CA-05 (esta checagem tem switch PRÓPRIO, à parte de <see cref="PredicadoDnf.Avaliar"/> —
    /// não migra sozinho quando um operador novo é adicionado).
    /// </summary>
    private static bool CondicaoDeModalidadeSatisfeitaPor(CondicaoDnf condicao, string modalidadeCodigo) =>
        condicao.Operador switch
        {
            Operador.Igual => condicao.Valor.ValueKind == JsonValueKind.String
                && string.Equals(condicao.Valor.GetString(), modalidadeCodigo, StringComparison.Ordinal),
            Operador.Em => condicao.Valor.ValueKind == JsonValueKind.Array
                && condicao.Valor.EnumerateArray().Any(item =>
                    item.ValueKind == JsonValueKind.String
                    && string.Equals(item.GetString(), modalidadeCodigo, StringComparison.Ordinal)),
            Operador.Diferente => condicao.Valor.ValueKind == JsonValueKind.String
                && !string.Equals(condicao.Valor.GetString(), modalidadeCodigo, StringComparison.Ordinal),
            Operador.NaoEm => condicao.Valor.ValueKind == JsonValueKind.Array
                && !condicao.Valor.EnumerateArray().Any(item =>
                    item.ValueKind == JsonValueKind.String
                    && string.Equals(item.GetString(), modalidadeCodigo, StringComparison.Ordinal)),
            _ => false,
        };

    /// <summary>
    /// Coerência entre <see cref="Aplicabilidade"/> e a existência de condição de gatilho
    /// viva (CA-01, Story #554, ADR-0071): <c>GERAL</c> nunca convive com condição viva.
    /// Chamado por <see cref="Criar"/> contra a coleção real de <see cref="Condicoes"/>.
    /// </summary>
    public DomainError? GarantirCoerenciaAplicabilidade(bool possuiCondicaoViva)
    {
        if (Aplicabilidade == Aplicabilidade.Geral && possuiCondicaoViva)
        {
            return new DomainError(
                "DocumentoExigido.GeralComCondicao",
                $"A exigência '{TipoDocumentoCodigo}' é GERAL e não pode conviver com condição de gatilho viva.");
        }

        return null;
    }
}

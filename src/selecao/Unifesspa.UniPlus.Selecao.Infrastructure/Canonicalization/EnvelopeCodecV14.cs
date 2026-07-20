namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Codec da versão <c>1.4</c> do envelope (Story #923, snapshot conjunto final da change
/// <c>documentos-exigidos-composicao</c>, ADR-0109 D1): acrescenta o 14º bloco de topo,
/// <c>arvoreSatisfacao</c> — a TOPOLOGIA da árvore de satisfação (<see cref="NoExigencia"/>,
/// Stories #920/#921/#922: grupos E/OU, cardinalidade qualificada de folha, repetição por
/// entidade), até aqui fail-closed na publicação por não ter onde congelar
/// (<c>ProcessoSeletivo.PendenciaDaArvoreDeSatisfacaoAindaNaoPublicavel</c>, removido nesta
/// Story). <see cref="SnapshotPublicacaoCanonicalizer"/> é o encoder — "o canonicalizador de
/// hoje" — enquanto for também o corrente; no dia de um bump para 1.5, este codec congela sua
/// própria cópia do encoder, exatamente como <see cref="EnvelopeCodecV13"/> fez para a 1.3.
/// </summary>
/// <remarks>
/// O decoder reaproveita <see cref="EnvelopeCodecV11"/> (via <see cref="EnvelopeCodecV12"/>)
/// para os 11 blocos cuja FORMA não mudou desde a 1.1, e <see cref="EnvelopeCodecV13.LerDocumentosExigidos"/>
/// para <c>documentosExigidos</c> (forma inalterada entre a 1.3 e a 1.4 — <c>arvoreSatisfacao</c>
/// é bloco de topo IRMÃO, não uma mudança daquele). <c>LerArvoreSatisfacao</c>/<c>LerNo</c> são
/// os únicos leitores novos: cada folha referencia sua exigência pelo <c>exigenciaId</c> já
/// decodificado por <c>LerDocumentosExigidos</c>, resolvido contra um dicionário por Id — nunca
/// duplicando a exigência dentro da árvore.
/// </remarks>
public sealed class EnvelopeCodecV14 : IEnvelopeCodec
{
    private static readonly string[] Stubs =
    [
        "formulario",
        "cascataRemanejamento",
        "divulgacao",
        "identidadesUnidade",
    ];

    private static readonly string[] BlocosReais =
    [
        "periodo",
        "etapas",
        "distribuicao",
        "modalidades",
        "ofertas",
        "atendimento",
        "bonusRegional",
        "criteriosDesempate",
        "classificacao",
        "hashesEdital",
        "cronogramaFases",
        "documentosExigidos",
        "vagas",
        "arvoreSatisfacao",
    ];

    private readonly SnapshotPublicacaoCanonicalizer _encoder = new();

    public string SchemaVersion => "1.4";

    public string AlgoritmoHash => "canonical-json/sha256@v1";

    public bool TemEncoder => true;

    public bool TemDecoder => true;

    public string? MotivoDaRecusa => null;

    /// <summary>
    /// O encoder <c>1.4</c> é o canonicalizador de hoje — e enquanto ele for também o
    /// corrente, a delegação basta. Mesma guarda de <see cref="EnvelopeCodecV13"/> antes do
    /// bump para 1.4: se a emissão de produção mudar de versão sem este codec acompanhar, a
    /// guarda estoura em vez de deixar o round-trip das versões 1.4 já publicadas ficar
    /// não-verificável em silêncio (ADR-0110 D1, fitness test CA-08).
    /// </summary>
    public SnapshotCanonico Codificar(EntradaCanonicalizacao entrada)
    {
        SnapshotCanonico snapshot = _encoder.Canonicalizar(entrada);

        if (snapshot.SchemaVersion != SchemaVersion || snapshot.AlgoritmoHash != AlgoritmoHash)
        {
            throw new InvalidOperationException(
                $"O codec {SchemaVersion} delegou a um canonicalizador que emite {snapshot.SchemaVersion}/{snapshot.AlgoritmoHash}. " +
                $"A emissão corrente mudou: o encoder {SchemaVersion} precisa ser congelado neste codec para que o round-trip " +
                "das versões já publicadas continue verificável (ADR-0110 D1).");
        }

        return snapshot;
    }

    public Result<EnvelopeReidratado> Decodificar(VersaoConfiguracao versao)
    {
        ArgumentNullException.ThrowIfNull(versao);

        Result<JsonObject> parse = EnvelopeCodecV11.Parsear(versao.ConfiguracaoCongeladaCanonica);
        if (parse.IsFailure)
        {
            return Result<EnvelopeReidratado>.Failure(parse.Error!);
        }

        JsonObject payload = parse.Value!;
        LeitorEnvelope leitor = new();

        bool temRetificacao = payload.ContainsKey("retificacao");
        string[] chavesEsperadas = temRetificacao
            ? [.. BlocosReais, .. Stubs, "retificacao"]
            : [.. BlocosReais, .. Stubs];

        leitor.ExigirChaves(payload, "$", chavesEsperadas);
        foreach (string stub in Stubs)
        {
            leitor.ExigirStub(payload, stub);
        }

        DadosEdital? dados = EnvelopeCodecV11.LerDadosEdital(leitor, payload, out string hashDocumento);
        IReadOnlyList<EtapaProcesso> etapas = EnvelopeCodecV11.LerEtapas(leitor, payload);
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicao = EnvelopeCodecV11.LerDistribuicao(leitor, payload);
        OfertaAtendimentoEspecializado? atendimento = EnvelopeCodecV11.LerAtendimento(leitor, payload);
        ConfiguracaoBonusRegional? bonus = EnvelopeCodecV11.LerBonusRegional(leitor, payload);
        IReadOnlyList<CriterioDesempate> desempate = EnvelopeCodecV11.LerCriteriosDesempate(leitor, payload);
        ConfiguracaoClassificacao? classificacao = EnvelopeCodecV11.LerClassificacao(leitor, payload);
        IReadOnlyList<FaseCronograma> cronogramaFases = EnvelopeCodecV11.LerCronogramaFases(leitor, payload, comId: true);
        (ResultadoConformidade? conformidade, IReadOnlyList<DocumentoExigido> documentosExigidos, ReferenciaTemporalFatos? referenciaTemporalFatos,
            IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatosCongelados) = EnvelopeCodecV13.LerDocumentosExigidos(leitor, payload);
        RetificacaoInfo? retificacao = temRetificacao ? EnvelopeCodecV11.LerRetificacao(leitor, payload) : null;

        if (leitor.Falhou)
        {
            return leitor.Falha<EnvelopeReidratado>();
        }

        Dictionary<Guid, DocumentoExigido> exigenciasPorId = IndexarExigenciasPorId(leitor, documentosExigidos);
        if (leitor.Falhou)
        {
            return leitor.Falha<EnvelopeReidratado>();
        }

        IReadOnlyList<NoExigencia> raizes = LerArvoreSatisfacao(leitor, payload, exigenciasPorId);
        if (leitor.Falhou)
        {
            return leitor.Falha<EnvelopeReidratado>();
        }

        if (EnvelopeCodecV11.VerificarCoerenciaComAVersao(versao, hashDocumento, retificacao) is { } incoerencia)
        {
            return Result<EnvelopeReidratado>.Failure(incoerencia);
        }

        IReadOnlyList<NoExigencia> todosOsNos = [.. raizes.SelectMany(static raiz => raiz.AchatarComDescendentes())];
        GrafoConfiguracao grafo = new(
            etapas, atendimento!, distribuicao, bonus, desempate, classificacao!, cronogramaFases,
            documentosExigidos, todosOsNos, referenciaTemporalFatos);
        return Result<EnvelopeReidratado>.Success(
            new EnvelopeReidratado(grafo, dados!, hashDocumento, retificacao, conformidade, metadadosFatosCongelados));
    }

    /// <summary>
    /// <c>documentosExigidos.exigencias</c> nunca tem <c>exigenciaId</c> duplicado quando
    /// produzido por um encoder real — mas um envelope adulterado poderia ter. Sem esta
    /// checagem, <c>ToDictionary</c> lançaria <see cref="ArgumentException"/> (500 não
    /// tratado) em vez de recusar como envelope malformado, mesma disciplina do restante do
    /// decoder.
    /// </summary>
    private static Dictionary<Guid, DocumentoExigido> IndexarExigenciasPorId(
        LeitorEnvelope leitor, IReadOnlyList<DocumentoExigido> documentosExigidos)
    {
        List<Guid> duplicados = [.. documentosExigidos
            .GroupBy(static d => d.Id)
            .Where(static grupo => grupo.Count() > 1)
            .Select(static grupo => grupo.Key)];

        if (duplicados.Count > 0)
        {
            return leitor.Propagar<Dictionary<Guid, DocumentoExigido>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'documentosExigidos.exigencias': o exigenciaId '{duplicados[0]}' aparece mais de uma vez.")) ?? [];
        }

        return documentosExigidos.ToDictionary(static d => d.Id);
    }

    /// <summary>
    /// Story #923 — a única chave nova. Cada item é uma raiz; a recursão desce por
    /// <c>filhos</c>, mesmo formato de <c>SnapshotPublicacaoCanonicalizer.SerializarNo</c>.
    /// </summary>
    private static List<NoExigencia> LerArvoreSatisfacao(
        LeitorEnvelope leitor, JsonObject payload, IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId)
    {
        JsonArray array = leitor.Array(payload, "arvoreSatisfacao", "$");
        if (leitor.Falhou)
        {
            return [];
        }

        List<NoExigencia> raizes = [];
        for (int i = 0; i < array.Count; i++)
        {
            JsonObject item = leitor.ItemObjeto(array, i, "arvoreSatisfacao");
            NoExigencia? no = LerNo(leitor, item, $"arvoreSatisfacao[{i}]", exigenciasPorId);
            if (leitor.Falhou)
            {
                return [];
            }

            raizes.Add(no!);
        }

        return raizes;
    }

    /// <summary>
    /// Um nó, recursivamente. <c>tipo</c>/<c>chaveDistincao</c>/<c>repetePorEntidade</c> usam
    /// o mesmo <c>FromCodigo</c> das demais leituras (RN08: token não reconhecido é envelope
    /// malformado, nunca coerção silenciosa a um sentinela). <c>exigenciaId</c> resolve contra
    /// <paramref name="exigenciasPorId"/> — presente sse <c>tipo</c> é <c>FOLHA</c> (checagem
    /// simétrica à de <see cref="NoExigencia.CriarFolha"/>/<see cref="NoExigencia.CriarGrupo"/>,
    /// aqui como forma, não semântica: <see cref="NoExigencia.Reidratar"/> não revalida).
    /// </summary>
    private static NoExigencia? LerNo(
        LeitorEnvelope leitor, JsonObject item, string path, IReadOnlyDictionary<Guid, DocumentoExigido> exigenciasPorId)
    {
        leitor.ExigirChaves(
            item, path,
            "id", "ordem", "tipo", "exigenciaId", "quantidadeMinima", "consequencia", "basesLegais",
            "chaveDistincao", "dataReferencia", "ocorrenciasEsperadas", "repetePorEntidade", "filhos");

        Guid id = leitor.Identificador(item, "id", path);
        int ordem = leitor.Inteiro(item, "ordem", path);
        string tipoCodigo = leitor.TextoNaoVazio(item, "tipo", path);
        Guid? exigenciaId = leitor.IdentificadorOpcional(item, "exigenciaId", path);
        int? quantidadeMinima = leitor.InteiroOpcional(item, "quantidadeMinima", path);
        string? consequencia = leitor.TextoOpcional(item, "consequencia", path, LimitesDoEnvelope.Token);
        string? chaveDistincaoCodigo = leitor.TextoOpcional(item, "chaveDistincao", path);
        DateOnly? dataReferencia = leitor.DataOpcional(item, "dataReferencia", path);
        string? repetePorEntidadeCodigo = leitor.TextoOpcional(item, "repetePorEntidade", path);
        if (leitor.Falhou)
        {
            return null;
        }

        IReadOnlyList<string>? ocorrenciasEsperadas = LerOcorrenciasEsperadasDeNo(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        IReadOnlyList<NoExigenciaBaseLegal> basesLegais = LerBasesLegaisDeNo(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        TipoNo tipo = TipoNoCodigo.FromCodigo(tipoCodigo);
        if (tipo == TipoNo.Nenhum)
        {
            return leitor.Propagar<NoExigencia>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.tipo' não reconhecido: '{tipoCodigo}'."));
        }

        ChaveDistincao? chaveDistincao = null;
        if (chaveDistincaoCodigo is not null)
        {
            chaveDistincao = ChaveDistincaoCodigo.FromCodigo(chaveDistincaoCodigo);
            if (chaveDistincao == Domain.Enums.ChaveDistincao.Nenhuma)
            {
                return leitor.Propagar<NoExigencia>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.chaveDistincao' não reconhecida: '{chaveDistincaoCodigo}'."));
            }
        }

        TipoEntidade? repetePorEntidade = null;
        if (repetePorEntidadeCodigo is not null)
        {
            repetePorEntidade = TipoEntidadeCodigo.FromCodigo(repetePorEntidadeCodigo);
            if (repetePorEntidade == TipoEntidade.Nenhuma)
            {
                return leitor.Propagar<NoExigencia>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.repetePorEntidade' não reconhecida: '{repetePorEntidadeCodigo}'."));
            }
        }

        DocumentoExigido? documentoExigido = null;
        if (tipo == TipoNo.Folha)
        {
            if (exigenciaId is not { } idDaExigencia)
            {
                return leitor.Propagar<NoExigencia>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}': nó FOLHA sem 'exigenciaId'."));
            }

            if (!exigenciasPorId.TryGetValue(idDaExigencia, out documentoExigido))
            {
                return leitor.Propagar<NoExigencia>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}.exigenciaId' ({idDaExigencia}) não corresponde a nenhuma exigência em 'documentosExigidos.exigencias'."));
            }
        }
        else if (exigenciaId is not null)
        {
            return leitor.Propagar<NoExigencia>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}': nó '{tipoCodigo}' não pode ter 'exigenciaId'."));
        }

        JsonArray filhosArray = leitor.Array(item, "filhos", path);
        if (leitor.Falhou)
        {
            return null;
        }

        List<NoExigencia> filhos = [];
        for (int i = 0; i < filhosArray.Count; i++)
        {
            JsonObject filhoItem = leitor.ItemObjeto(filhosArray, i, $"{path}.filhos");
            NoExigencia? filho = LerNo(leitor, filhoItem, $"{path}.filhos[{i}]", exigenciasPorId);
            if (leitor.Falhou)
            {
                return null;
            }

            filhos.Add(filho!);
        }

        return NoExigencia.Reidratar(
            id, tipo, ordem, exigenciaId, documentoExigido, quantidadeMinima, consequencia,
            chaveDistincao, dataReferencia, ocorrenciasEsperadas, repetePorEntidade, basesLegais, filhos);
    }

    /// <summary>Mesma técnica de <c>EnvelopeCodecV13.LerValoresDominio</c> para o campo nulo-ou-array.</summary>
    private static IReadOnlyList<string>? LerOcorrenciasEsperadasDeNo(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        string path = $"{pathPai}.ocorrenciasEsperadas";
        if (item["ocorrenciasEsperadas"] is not JsonNode node)
        {
            return null;
        }

        if (node is not JsonArray)
        {
            return leitor.Propagar<IReadOnlyList<string>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}' deveria ser um array de textos ou null."));
        }

        return leitor.Textos(item, "ocorrenciasEsperadas", pathPai);
    }

    /// <summary>
    /// Base legal PRÓPRIA de um grupo — mesmo formato de <c>EnvelopeCodecV12.LerBasesLegais</c>
    /// (a de <see cref="DocumentoExigido"/>), tipo diferente (<see cref="NoExigenciaBaseLegal"/>).
    /// Só <c>RESOLVIDO</c> é congelado — mesma razão de <c>LerBasesLegais</c>.
    /// </summary>
    private static IReadOnlyList<NoExigenciaBaseLegal> LerBasesLegaisDeNo(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        JsonArray array = leitor.Array(item, "basesLegais", pathPai);
        if (leitor.Falhou)
        {
            return [];
        }

        List<NoExigenciaBaseLegal> basesLegais = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"{pathPai}.basesLegais[{i}]";
            JsonObject baseItem = leitor.ItemObjeto(array, i, $"{pathPai}.basesLegais");
            leitor.ExigirChaves(baseItem, path, "referencia", "abrangencia", "status", "observacao");

            string referencia = leitor.TextoNaoVazio(baseItem, "referencia", path, LimitesDoEnvelope.BaseLegal);
            string abrangenciaCodigo = leitor.TextoNaoVazio(baseItem, "abrangencia", path);
            string statusCodigo = leitor.TextoNaoVazio(baseItem, "status", path);
            string? observacao = leitor.TextoOpcional(baseItem, "observacao", path, LimitesDoEnvelope.ObservacaoBaseLegal);
            if (leitor.Falhou)
            {
                return [];
            }

            StatusBaseLegal status = StatusBaseLegalCodigo.FromCodigo(statusCodigo);
            if (status != StatusBaseLegal.Resolvido)
            {
                return leitor.Propagar<IReadOnlyList<NoExigenciaBaseLegal>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}.status' deveria ser sempre RESOLVIDO — encontrado '{statusCodigo}'.")) ?? [];
            }

            Result<NoExigenciaBaseLegal> baseLegalResult = NoExigenciaBaseLegal.Criar(
                referencia, TipoAbrangenciaCodigo.FromCodigo(abrangenciaCodigo), status, observacao);
            if (baseLegalResult.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<NoExigenciaBaseLegal>>(baseLegalResult.Error!) ?? [];
            }

            basesLegais.Add(baseLegalResult.Value!);
        }

        return basesLegais;
    }
}

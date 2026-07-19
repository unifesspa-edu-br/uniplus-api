namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Codec da versão <c>1.2</c> do envelope (Story #554, PR #903, ADR-0109 D1): a forma nova
/// que substitui o stub de <c>documentosExigidos.exigencias</c> por um bloco rico
/// (CA-09), e acrescenta <c>referenciaTemporalFatos</c>/<c>dataReferenciaFatos</c>
/// (B-03). <see cref="SnapshotPublicacaoCanonicalizer"/> é o encoder — "o
/// canonicalizador de hoje" — enquanto for também o corrente; no dia de um bump para
/// 1.3, este codec congela sua própria cópia do encoder, exatamente como
/// <see cref="EnvelopeCodecV11"/> fez para a 1.1.
/// </summary>
/// <remarks>
/// O decoder reaproveita os métodos <c>internal</c> de <see cref="EnvelopeCodecV11"/>
/// para os 11 blocos cuja FORMA não mudou entre 1.1 e 1.2 (etapas, distribuição,
/// modalidades, atendimento, bônus, desempate, classificação, hashesEdital, período,
/// ofertas, vagas, retificação) — ao contrário do encoder (ADR-0109 D1, nunca evolui no
/// lugar), decodificar bytes de um bloco cuja forma NÃO mudou não corre o mesmo risco: é
/// interpretar bytes fixos, não produzir novos, e um bug corrigido no leitor
/// compartilhado corrige os dois codecs ao mesmo tempo, nunca diverge.
/// <c>documentosExigidos</c> (o bloco cuja forma muda nesta versão) ganha um leitor
/// próprio aqui; <c>cronogramaFases</c> também muda de forma (a chave <c>id</c> nova,
/// achado de revisão — Story #554, PR #903), mas continua reaproveitando
/// <see cref="EnvelopeCodecV11.LerCronogramaFases"/> via o parâmetro <c>comId</c>, sem
/// duplicar o leitor inteiro.
/// </remarks>
public sealed class EnvelopeCodecV12 : IEnvelopeCodec
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
    ];

    private readonly SnapshotPublicacaoCanonicalizer _encoder = new();

    public string SchemaVersion => "1.2";

    public string AlgoritmoHash => "canonical-json/sha256@v1";

    public bool TemEncoder => true;

    public bool TemDecoder => true;

    public string? MotivoDaRecusa => null;

    /// <summary>
    /// O encoder <c>1.2</c> é o canonicalizador de hoje — e enquanto ele for também o
    /// corrente, a delegação basta. Mesma guarda de <see cref="EnvelopeCodecV11"/> antes
    /// do bump para 1.2: se a emissão de produção mudar de versão sem este codec
    /// acompanhar, a guarda estoura em vez de deixar o round-trip das versões 1.2 já
    /// publicadas ficar não-verificável em silêncio.
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
        (ResultadoConformidade? conformidade, IReadOnlyList<DocumentoExigido> documentosExigidos, ReferenciaTemporalFatos? referenciaTemporalFatos) =
            LerDocumentosExigidos(leitor, payload);
        RetificacaoInfo? retificacao = temRetificacao ? EnvelopeCodecV11.LerRetificacao(leitor, payload) : null;

        if (leitor.Falhou)
        {
            return leitor.Falha<EnvelopeReidratado>();
        }

        if (EnvelopeCodecV11.VerificarCoerenciaComAVersao(versao, hashDocumento, retificacao) is { } incoerencia)
        {
            return Result<EnvelopeReidratado>.Failure(incoerencia);
        }

        GrafoConfiguracao grafo = new(
            etapas, atendimento!, distribuicao, bonus, desempate, classificacao!, cronogramaFases,
            documentosExigidos, referenciaTemporalFatos);
        return Result<EnvelopeReidratado>.Success(
            new EnvelopeReidratado(grafo, dados!, hashDocumento, retificacao, conformidade));
    }

    /// <summary>
    /// Story #554 (PR #903): a única leitura de bloco que difere de <see cref="EnvelopeCodecV11"/>.
    /// <c>exigencias[]</c> é real (não mais stub); <c>obrigatoriedades[]</c> mantém a MESMA
    /// forma da 1.1 (reaproveita <see cref="EnvelopeCodecV11.LerPredicadoObrigatoriedade"/>);
    /// <c>referenciaTemporalFatos</c>/<c>dataReferenciaFatos</c> são as duas chaves novas
    /// (B-03) — a política crua e o output resolvido, lidos aqui só para prova de
    /// round-trip (a data resolvida não é reposta em lugar nenhum do grafo: quem a repõe é
    /// <see cref="Entities.ProcessoSeletivo.ResolverDataReferenciaFatos"/>, recalculando a
    /// partir da política restaurada).
    /// </summary>
    private static (ResultadoConformidade? Conformidade, IReadOnlyList<DocumentoExigido> DocumentosExigidos, ReferenciaTemporalFatos? ReferenciaTemporalFatos)
        LerDocumentosExigidos(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "documentosExigidos", "$");
        if (leitor.Falhou)
        {
            return (null, [], null);
        }

        leitor.ExigirChaves(
            bloco, "documentosExigidos", "exigencias", "obrigatoriedades", "referenciaTemporalFatos", "dataReferenciaFatos");

        IReadOnlyList<DocumentoExigido> exigencias = LerExigencias(leitor, bloco);
        if (leitor.Falhou)
        {
            return (null, [], null);
        }

        ResultadoConformidade? conformidade = LerObrigatoriedades(leitor, bloco);
        if (leitor.Falhou)
        {
            return (null, [], null);
        }

        ReferenciaTemporalFatos? referenciaTemporalFatos = LerReferenciaTemporalFatosPolitica(leitor, bloco);
        if (leitor.Falhou)
        {
            return (null, [], null);
        }

        // A data resolvida só é lida para participar do payload fechado (ExigirChaves
        // acima já a exige); a prova de que ela bate com a política é o round-trip
        // reidratar→recanonicalizar, não uma comparação aqui — ver o comentário de classe.
        leitor.DataOpcional(bloco, "dataReferenciaFatos", "documentosExigidos");

        return leitor.Falhou ? (null, [], null) : (conformidade, exigencias, referenciaTemporalFatos);
    }

    /// <summary>
    /// Não valida <c>exigidoNaFaseId</c> contra as fases decodificadas nesta mesma
    /// passagem. Desde o achado de revisão que acrescentou <c>id</c> ao bloco
    /// <c>cronogramaFases</c> (<see cref="EnvelopeCodecV11.LerCronogramaFases"/>,
    /// parâmetro <c>comId</c>), <c>FaseCronograma.Id</c> É congelado no envelope 1.2 e a
    /// checagem SERIA possível — mas continua redundante: a mesma razão pela qual
    /// <see cref="ReferenciaTemporalFatos.FaseId"/> (<see cref="LerReferenciaTemporalFatosPolitica"/>)
    /// também não é validado aqui. A resolução real acontece no domínio
    /// (<see cref="Entities.ProcessoSeletivo.RestaurarConfiguracaoCongelada"/>/<c>ResolverDataReferenciaFatos</c>),
    /// que agora enxerga o MESMO Id que a exigência/política referenciam — não um Id
    /// regenerado a cada decodificação.
    /// </summary>
    private static IReadOnlyList<DocumentoExigido> LerExigencias(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonArray array = leitor.Array(bloco, "exigencias", "documentosExigidos");
        if (leitor.Falhou)
        {
            return [];
        }

        List<DocumentoExigido> exigencias = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"documentosExigidos.exigencias[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "documentosExigidos.exigencias");
            leitor.ExigirChaves(
                item, path,
                "exigenciaId", "tipoDocumentoOrigemId", "tipoDocumentoCodigo", "tipoDocumentoNome",
                "tipoDocumentoCategoria", "exigidoNaFaseId", "aplicabilidade", "obrigatorio",
                "consequenciaIndeferimento", "grupoSatisfacaoId", "condicaoGatilho", "basesLegais",
                "idadeMaximaEmissao", "formatosPermitidos", "tamanhoMaximoBytes");

            Guid exigenciaId = leitor.Identificador(item, "exigenciaId", path);
            Guid tipoDocumentoOrigemId = leitor.Identificador(item, "tipoDocumentoOrigemId", path);
            string tipoDocumentoCodigo = leitor.TextoNaoVazio(item, "tipoDocumentoCodigo", path, LimitesDoEnvelope.TipoDocumentoCodigo);
            string tipoDocumentoNome = leitor.TextoNaoVazio(item, "tipoDocumentoNome", path, LimitesDoEnvelope.TipoDocumentoNome);
            string tipoDocumentoCategoria = leitor.TextoNaoVazio(item, "tipoDocumentoCategoria", path, LimitesDoEnvelope.TipoDocumentoCategoria);
            Guid exigidoNaFaseId = leitor.Identificador(item, "exigidoNaFaseId", path);
            Aplicabilidade aplicabilidade = leitor.Enumeracao<Aplicabilidade>(item, "aplicabilidade", path);
            bool obrigatorio = leitor.Booleano(item, "obrigatorio", path);
            string? consequenciaIndeferimento = leitor.TextoOpcional(item, "consequenciaIndeferimento", path, LimitesDoEnvelope.Token);
            Guid? grupoSatisfacaoId = leitor.IdentificadorOpcional(item, "grupoSatisfacaoId", path);
            int? tamanhoMaximoBytes = leitor.InteiroOpcional(item, "tamanhoMaximoBytes", path);

            if (leitor.Falhou)
            {
                return [];
            }

            IReadOnlyList<CondicaoGatilho> condicoes = LerCondicaoGatilho(leitor, item, path);
            if (leitor.Falhou)
            {
                return [];
            }

            IReadOnlyList<DocumentoExigidoBaseLegal> basesLegais = LerBasesLegais(leitor, item, path);
            if (leitor.Falhou)
            {
                return [];
            }

            IdadeMaximaEmissao? idadeMaximaEmissao = LerIdadeMaximaEmissao(leitor, item, path);
            if (leitor.Falhou)
            {
                return [];
            }

            FormatosPermitidos? formatosPermitidos = LerFormatosPermitidos(leitor, item, path);
            if (leitor.Falhou)
            {
                return [];
            }

            if (tamanhoMaximoBytes is <= 0)
            {
                return leitor.Propagar<IReadOnlyList<DocumentoExigido>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}.tamanhoMaximoBytes' deve ser maior que zero quando presente.")) ?? [];
            }

            exigencias.Add(DocumentoExigido.Reidratar(
                exigenciaId,
                exigidoNaFaseId,
                tipoDocumentoOrigemId,
                tipoDocumentoCodigo,
                tipoDocumentoNome,
                tipoDocumentoCategoria,
                aplicabilidade,
                obrigatorio,
                consequenciaIndeferimento,
                grupoSatisfacaoId,
                condicoes,
                basesLegais,
                idadeMaximaEmissao,
                formatosPermitidos!,
                tamanhoMaximoBytes));
        }

        return exigencias;
    }

    /// <summary>
    /// <see cref="FormatosPermitidos"/> (Story #918) substitui o campo singular
    /// <c>formatoPermitido</c> — objeto SEMPRE presente (o VO é obrigatório em
    /// <see cref="DocumentoExigido"/>), com <c>lista</c> nula ⟺ <c>qualquer</c> verdadeiro.
    /// A validação de forma/coerência (formato reconhecido, sem duplicata, teto por formato
    /// positivo, QUALQUER exclusivo) é <see cref="Domain.ValueObjects.FormatosPermitidos.Criar"/> —
    /// mesma fronteira de responsabilidade que o restante deste decoder (RN08: o congelado
    /// não revalida semântica, só forma).
    /// </summary>
    private static FormatosPermitidos? LerFormatosPermitidos(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        JsonObject formatosJson = leitor.Objeto(item, "formatosPermitidos", pathPai);
        if (leitor.Falhou)
        {
            return null;
        }

        string path = $"{pathPai}.formatosPermitidos";
        leitor.ExigirChaves(formatosJson, path, "qualquer", "lista");

        bool qualquer = leitor.Booleano(formatosJson, "qualquer", path);
        if (leitor.Falhou)
        {
            return null;
        }

        JsonNode? listaNode = formatosJson["lista"];
        if (listaNode is null)
        {
            Result<FormatosPermitidos> resultadoSemLista = FormatosPermitidos.Criar(qualquer, entradas: null);
            return resultadoSemLista.IsFailure
                ? leitor.Propagar<FormatosPermitidos>(resultadoSemLista.Error!)
                : resultadoSemLista.Value;
        }

        if (listaNode is not JsonArray listaArray)
        {
            return leitor.Propagar<FormatosPermitidos>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.lista' deveria ser um array ou null."));
        }

        List<(string Formato, int? TamanhoMaximoBytesMax)> entradas = [];
        for (int i = 0; i < listaArray.Count; i++)
        {
            string itemPath = $"{path}.lista[{i}]";
            JsonObject entradaJson = leitor.ItemObjeto(listaArray, i, $"{path}.lista");
            leitor.ExigirChaves(entradaJson, itemPath, "formato", "tamanhoMaximoBytesMax");

            string formatoCodigo = leitor.TextoNaoVazio(entradaJson, "formato", itemPath);
            int? tamanhoMaximoBytesMax = leitor.InteiroOpcional(entradaJson, "tamanhoMaximoBytesMax", itemPath);
            if (leitor.Falhou)
            {
                return null;
            }

            entradas.Add((formatoCodigo, tamanhoMaximoBytesMax));
        }

        Result<FormatosPermitidos> resultado = FormatosPermitidos.Criar(qualquer, entradas);
        return resultado.IsFailure ? leitor.Propagar<FormatosPermitidos>(resultado.Error!) : resultado.Value;
    }

    /// <summary>
    /// O predicado DNF (PR #896): <see langword="null"/> na chave é "sem gatilho" (0
    /// cláusulas); do contrário, um array de cláusulas (OU), cada uma um array de
    /// condições (E) — a forma espelha exatamente <c>SnapshotPublicacaoCanonicalizer.SerializarCondicaoGatilho</c>.
    /// A validação de forma de cada condição é <see cref="CondicaoGatilho.Criar"/>, a
    /// mesma factory que o caminho de comando usa — o decoder não revalida semântica
    /// (RN08: um predicado congelado não é reinterpretado contra um vocabulário que pode
    /// ter mudado).
    /// </summary>
    private static IReadOnlyList<CondicaoGatilho> LerCondicaoGatilho(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        string chave = "condicaoGatilho";
        if (item[chave] is not JsonNode raiz)
        {
            return [];
        }

        if (raiz is not JsonArray clausulas)
        {
            return leitor.Propagar<IReadOnlyList<CondicaoGatilho>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{pathPai}.{chave}' deveria ser um array de cláusulas ou null.")) ?? [];
        }

        List<CondicaoGatilho> condicoes = [];
        for (int c = 0; c < clausulas.Count; c++)
        {
            string clausulaPath = $"{pathPai}.{chave}[{c}]";
            if (clausulas[c] is not JsonArray condicoesDaClausula)
            {
                return leitor.Propagar<IReadOnlyList<CondicaoGatilho>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{clausulaPath}' deveria ser um array de condições.")) ?? [];
            }

            for (int i = 0; i < condicoesDaClausula.Count; i++)
            {
                string condicaoPath = $"{clausulaPath}[{i}]";
                JsonObject condicaoItem = leitor.ItemObjeto(condicoesDaClausula, i, clausulaPath);
                leitor.ExigirChaves(condicaoItem, condicaoPath, "fato", "operador", "valor");

                string fato = leitor.TextoNaoVazio(condicaoItem, "fato", condicaoPath, LimitesDoEnvelope.Fato);
                string operadorCodigo = leitor.TextoNaoVazio(condicaoItem, "operador", condicaoPath);
                JsonElement valor = leitor.Valor(condicaoItem, "valor", condicaoPath);
                if (leitor.Falhou)
                {
                    return [];
                }

                Operador operador = OperadorCodigo.FromCodigo(operadorCodigo);
                Result<CondicaoGatilho> condicaoResult = CondicaoGatilho.Criar(c, fato, operador, valor);
                if (condicaoResult.IsFailure)
                {
                    return leitor.Propagar<IReadOnlyList<CondicaoGatilho>>(condicaoResult.Error!) ?? [];
                }

                condicoes.Add(condicaoResult.Value!);
            }
        }

        return condicoes;
    }

    /// <summary>Só <c>RESOLVIDO</c> é congelado (PR #898) — todo item lido aqui reidrata como tal.</summary>
    private static IReadOnlyList<DocumentoExigidoBaseLegal> LerBasesLegais(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        JsonArray array = leitor.Array(item, "basesLegais", pathPai);
        if (leitor.Falhou)
        {
            return [];
        }

        List<DocumentoExigidoBaseLegal> basesLegais = [];
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

            // FromCodigo mapeia um token não reconhecido para o sentinela Nenhuma — e Criar
            // já o rejeita (DocumentoExigidoBaseLegal.AbrangenciaObrigatoria/StatusObrigatorio).
            // Um status congelado diferente de RESOLVIDO é envelope adulterado — só bases
            // resolvidas são materializadas (PR #898), e Reidratar não teria como saber disso
            // sozinho; a checagem é aqui, na fronteira de leitura.
            StatusBaseLegal status = StatusBaseLegalCodigo.FromCodigo(statusCodigo);
            if (status != StatusBaseLegal.Resolvido)
            {
                return leitor.Propagar<IReadOnlyList<DocumentoExigidoBaseLegal>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}.status' deveria ser sempre RESOLVIDO — encontrado '{statusCodigo}'.")) ?? [];
            }

            Result<DocumentoExigidoBaseLegal> baseLegalResult = DocumentoExigidoBaseLegal.Criar(
                referencia, TipoAbrangenciaCodigo.FromCodigo(abrangenciaCodigo), status, observacao);
            if (baseLegalResult.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<DocumentoExigidoBaseLegal>>(baseLegalResult.Error!) ?? [];
            }

            basesLegais.Add(baseLegalResult.Value!);
        }

        return basesLegais;
    }

    private static IdadeMaximaEmissao? LerIdadeMaximaEmissao(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        JsonObject? idadeJson = leitor.ObjetoOpcional(item, "idadeMaximaEmissao", pathPai);
        if (leitor.Falhou || idadeJson is null)
        {
            return null;
        }

        string path = $"{pathPai}.idadeMaximaEmissao";
        leitor.ExigirChaves(idadeJson, path, "valor", "unidade", "referenciaTipo", "data", "referenciaFaseId");

        int valor = leitor.Inteiro(idadeJson, "valor", path);
        string unidadeCodigo = leitor.TextoNaoVazio(idadeJson, "unidade", path);
        string referenciaTipoCodigo = leitor.TextoNaoVazio(idadeJson, "referenciaTipo", path);
        DateOnly? data = leitor.DataOpcional(idadeJson, "data", path);
        Guid? referenciaFaseId = leitor.IdentificadorOpcional(idadeJson, "referenciaFaseId", path);
        if (leitor.Falhou)
        {
            return null;
        }

        if (UnidadeIdadeCodigo.FromCodigo(unidadeCodigo) is not { } unidade)
        {
            return leitor.Propagar<IdadeMaximaEmissao>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.unidade' não reconhecida: '{unidadeCodigo}'."));
        }

        if (ReferenciaTipoIdadeEmissaoCodigo.FromCodigo(referenciaTipoCodigo) is not { } referenciaTipo)
        {
            return leitor.Propagar<IdadeMaximaEmissao>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.referenciaTipo' não reconhecida: '{referenciaTipoCodigo}'."));
        }

        Result<IdadeMaximaEmissao?> idadeResult = IdadeMaximaEmissao.Criar(valor, unidade, referenciaTipo, data, referenciaFaseId);
        return idadeResult.IsFailure ? leitor.Propagar<IdadeMaximaEmissao>(idadeResult.Error!) : idadeResult.Value;
    }

    /// <summary>Mesma forma de <c>obrigatoriedades[]</c> da 1.1 — reaproveita o leitor de predicado.</summary>
    private static ResultadoConformidade? LerObrigatoriedades(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonArray array = leitor.Array(bloco, "obrigatoriedades", "documentosExigidos");
        if (leitor.Falhou)
        {
            return null;
        }

        List<RegraAvaliada> regras = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"documentosExigidos.obrigatoriedades[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "documentosExigidos.obrigatoriedades");
            leitor.ExigirChaves(
                item, path,
                "regraId", "regraCodigo", "categoria", "tipoProcessoCodigoAvaliado", "predicado",
                "aprovada", "baseLegal", "atoNormativoUrl", "portariaInterna", "descricaoHumana",
                "vigenciaInicio", "vigenciaFim", "hash");

            Guid regraId = leitor.Identificador(item, "regraId", path);
            string regraCodigo = leitor.TextoNaoVazio(item, "regraCodigo", path);
            CategoriaObrigatoriedade categoria = leitor.Enumeracao<CategoriaObrigatoriedade>(item, "categoria", path);
            string tipoProcessoCodigoAvaliado = leitor.TextoNaoVazio(item, "tipoProcessoCodigoAvaliado", path);
            JsonObject predicadoJson = leitor.Objeto(item, "predicado", path);
            if (leitor.Falhou)
            {
                return null;
            }

            PredicadoObrigatoriedade? predicado = EnvelopeCodecV11.LerPredicadoObrigatoriedade(leitor, predicadoJson, $"{path}.predicado");
            if (leitor.Falhou)
            {
                return null;
            }

            bool aprovada = leitor.Booleano(item, "aprovada", path);
            string baseLegal = leitor.TextoNaoVazio(item, "baseLegal", path);
            string? atoNormativoUrl = leitor.TextoOpcional(item, "atoNormativoUrl", path);
            string? portariaInterna = leitor.TextoOpcional(item, "portariaInterna", path);
            string descricaoHumana = leitor.TextoNaoVazio(item, "descricaoHumana", path);
            DateOnly vigenciaInicio = leitor.Data(item, "vigenciaInicio", path);
            DateOnly? vigenciaFim = leitor.DataOpcional(item, "vigenciaFim", path);
            string hash = leitor.TextoNaoVazio(item, "hash", path);
            if (leitor.Falhou)
            {
                return null;
            }

            regras.Add(new RegraAvaliada(
                regraId, regraCodigo, categoria, tipoProcessoCodigoAvaliado, predicado!, aprovada, null,
                baseLegal, atoNormativoUrl, portariaInterna, descricaoHumana, vigenciaInicio, vigenciaFim, hash));
        }

        return regras.Count == 0 ? null : new ResultadoConformidade(regras, []);
    }

    /// <summary>A POLÍTICA crua (B-03) — o insumo de <see cref="Entities.ProcessoSeletivo.ResolverDataReferenciaFatos"/>.</summary>
    private static ReferenciaTemporalFatos? LerReferenciaTemporalFatosPolitica(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonObject? json = leitor.ObjetoOpcional(bloco, "referenciaTemporalFatos", "documentosExigidos");
        if (leitor.Falhou || json is null)
        {
            return null;
        }

        const string path = "documentosExigidos.referenciaTemporalFatos";
        leitor.ExigirChaves(json, path, "tipo", "data", "faseId");

        string tipoCodigo = leitor.TextoNaoVazio(json, "tipo", path);
        DateOnly? data = leitor.DataOpcional(json, "data", path);
        Guid? faseId = leitor.IdentificadorOpcional(json, "faseId", path);
        if (leitor.Falhou)
        {
            return null;
        }

        // FromCodigo mapeia um token não reconhecido para o sentinela Nenhuma — e Criar já
        // o rejeita com um DomainError nomeado (ReferenciaTemporalFatos.TipoObrigatorio),
        // sem precisar de uma checagem de "código desconhecido" própria aqui.
        Result<ReferenciaTemporalFatos> resultado = ReferenciaTemporalFatos.Criar(
            ReferenciaTipoCodigo.FromCodigo(tipoCodigo), data, faseId);
        return resultado.IsFailure ? leitor.Propagar<ReferenciaTemporalFatos>(resultado.Error!) : resultado.Value;
    }
}

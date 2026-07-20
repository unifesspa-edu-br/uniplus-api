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
/// (B-03). <see cref="SnapshotPublicacaoCanonicalizer"/> foi o encoder — "o
/// canonicalizador de hoje" — só enquanto a 1.2 foi também a corrente.
/// </summary>
/// <remarks>
/// <para>
/// <b>Encoder congelado (Story #919, bump para 1.3 — ADR-0109 D1):</b> até aqui, este
/// método delegava a <see cref="SnapshotPublicacaoCanonicalizer"/>, que era "o
/// canonicalizador de hoje". Com o bump para 1.3 (acréscimo do bloco
/// <c>documentosExigidos.metadadosFatos</c>), <see cref="SnapshotPublicacaoCanonicalizer"/>
/// passou a emitir 1.3 — é ele quem os handlers de escrita injetam como o encoder vivo.
/// Este método é agora a ÚNICA fonte de verdade de como um envelope 1.2 é produzido: uma
/// cópia autossuficiente do que o canonicalizador emitia neste instante, para que o
/// round-trip das versões 1.2 já publicadas continue verificável para sempre, imune a
/// qualquer refactor futuro do canonicalizador vivo — exatamente o que aconteceu com
/// <see cref="EnvelopeCodecV11"/> no bump anterior (1.1 → 1.2).
/// </para>
/// <para>
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
/// duplicar o leitor inteiro. O decoder da 1.2 NÃO ganha <c>metadadosFatos</c> — a 1.2
/// nunca teve essa chave, e um envelope histórico "1.2" não a tem nos bytes.
/// </para>
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

    public string SchemaVersion => "1.2";

    public string AlgoritmoHash => "canonical-json/sha256@v1";

    public bool TemEncoder => true;

    public bool TemDecoder => true;

    public string? MotivoDaRecusa => null;

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
            documentosExigidos, [], referenciaTemporalFatos);
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
    /// <summary>
    /// <c>internal</c> (não <c>private</c>): a forma de <c>exigencias[]</c> não muda entre a
    /// 1.2 e a 1.3 (Story #919) — <see cref="EnvelopeCodecV13"/> reaproveita este leitor tal
    /// qual, mesma técnica de <see cref="EnvelopeCodecV11"/> para os blocos que sobrevivem
    /// ao bump 1.1→1.2.
    /// </summary>
    internal static IReadOnlyList<DocumentoExigido> LerExigencias(LeitorEnvelope leitor, JsonObject bloco)
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

    /// <summary>
    /// Mesma forma de <c>obrigatoriedades[]</c> da 1.1 — reaproveita o leitor de predicado.
    /// <c>internal</c>: a forma não muda na 1.3 (Story #919) — <see cref="EnvelopeCodecV13"/>
    /// reaproveita este leitor tal qual.
    /// </summary>
    internal static ResultadoConformidade? LerObrigatoriedades(LeitorEnvelope leitor, JsonObject bloco)
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

    /// <summary>
    /// A POLÍTICA crua (B-03) — o insumo de <see cref="Entities.ProcessoSeletivo.ResolverDataReferenciaFatos"/>.
    /// <c>internal</c>: a forma não muda na 1.3 (Story #919) — <see cref="EnvelopeCodecV13"/>
    /// reaproveita este leitor tal qual.
    /// </summary>
    internal static ReferenciaTemporalFatos? LerReferenciaTemporalFatosPolitica(LeitorEnvelope leitor, JsonObject bloco)
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

    // ────────────────────────────────────────────────────────────────────────────
    // Encoder congelado 1.2 (ver o comentário de classe acima) — cópia
    // autossuficiente do que SnapshotPublicacaoCanonicalizer emitia antes do bump
    // para 1.3. Sufixo "V12" nos nomes: intencional, mesma convenção do sufixo
    // "V11" em EnvelopeCodecV11 — nunca confundir com a lógica viva (que evolui).
    // ────────────────────────────────────────────────────────────────────────────

    private const int EscalaPadraoV12 = 4;
    private const int EscalaPercentualV12 = 2;

    private static readonly JsonObject NaoConstruidoV12 = new() { ["status"] = "nao_construido" };

    /// <summary>
    /// O encoder 1.2, congelado. Até o bump para 1.3, este método delegava a
    /// <see cref="SnapshotPublicacaoCanonicalizer"/> — ver o comentário de classe.
    /// </summary>
    public SnapshotCanonico Codificar(EntradaCanonicalizacao entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        ProcessoSeletivo processo = entrada.Processo;
        DadosEdital dados = entrada.Dados;
        RetificacaoInfo? retificacao = entrada.Retificacao;

        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentException.ThrowIfNullOrWhiteSpace(entrada.HashDocumento);

        JsonObject payload = new()
        {
            ["periodo"] = SerializarPeriodoV12(dados),
            ["etapas"] = SerializarEtapasV12(processo),
            ["vagas"] = SerializarVagasV12(processo),
            ["distribuicao"] = SerializarDistribuicaoV12(processo),
            ["modalidades"] = SerializarModalidadesV12(processo),
            ["ofertas"] = SerializarOfertasV12(processo),
            ["atendimento"] = SerializarAtendimentoV12(processo),
            ["bonusRegional"] = SerializarBonusRegionalV12(processo),
            ["criteriosDesempate"] = SerializarCriteriosDesempateV12(processo),
            ["classificacao"] = SerializarClassificacaoV12(processo),
            ["hashesEdital"] = SerializarHashesEditalV12(dados, entrada.HashDocumento),
            ["documentosExigidos"] = SerializarDocumentosExigidosV12(processo, entrada.Conformidade),
            ["formulario"] = NaoConstruidoV12.DeepClone(),
            ["cascataRemanejamento"] = NaoConstruidoV12.DeepClone(),
            ["divulgacao"] = NaoConstruidoV12.DeepClone(),
            ["cronogramaFases"] = SerializarCronogramaFasesV12(processo),
            ["identidadesUnidade"] = NaoConstruidoV12.DeepClone(),
        };

        // ADR-0101: a retificação ACRESCENTA um 18º bloco preservando os 17 anteriores.
        if (retificacao is not null)
        {
            payload["retificacao"] = new JsonObject
            {
                ["editalRetificadoId"] = retificacao.EditalRetificadoId,
                ["motivo"] = HashCanonicalComputer.NormalizeNfc(retificacao.Motivo),
            };
        }

        byte[] bytes = HashCanonicalComputer.ComputeSnapshotBytes(payload);
        return new SnapshotCanonico(bytes, SchemaVersion, AlgoritmoHash);
    }

    private static JsonObject SerializarPeriodoV12(DadosEdital dados) => new()
    {
        ["numero"] = dados.Numero is { } numero ? HashCanonicalComputer.NormalizeNfc(numero) : null,
        ["inicio"] = dados.PeriodoInscricaoInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["fim"] = dados.PeriodoInscricaoFim.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    private static JsonArray SerializarEtapasV12(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        IOrderedEnumerable<EtapaProcesso> ordenadas = processo.Etapas
            .OrderBy(static e => e.Ordem ?? int.MaxValue)
            .ThenBy(static e => e.Id);
        foreach (EtapaProcesso etapa in ordenadas)
        {
            array.Add(new JsonObject
            {
                ["id"] = etapa.Id,
                ["nome"] = HashCanonicalComputer.NormalizeNfc(etapa.Nome),
                ["carater"] = etapa.Carater.ToString(),
                ["peso"] = etapa.Peso is { } peso ? HashCanonicalComputer.SerializeDecimalCanonical(peso, EscalaPadraoV12) : null,
                ["notaMinima"] = etapa.NotaMinima is { } notaMinima ? HashCanonicalComputer.SerializeDecimalCanonical(notaMinima, EscalaPadraoV12) : null,
                ["ordem"] = etapa.Ordem,
            });
        }

        return array;
    }

    private static JsonArray SerializarDistribuicaoV12(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDistribuicaoVagas configuracao in OrdenarPorOfertaCursoOrigemIdV12(processo.DistribuicaoVagas))
        {
            array.Add(new JsonObject
            {
                ["ofertaCursoOrigemId"] = configuracao.OfertaCursoOrigemId,
                ["voBase"] = configuracao.VoBase,
                ["pr"] = HashCanonicalComputer.SerializeDecimalCanonical(configuracao.Pr, EscalaPadraoV12),
                ["regraDistribuicao"] = SerializarReferenciaRegraV12(configuracao.RegraDistribuicao),
                ["regraAjuste"] = configuracao.RegraAjuste is { } regraAjuste ? SerializarReferenciaRegraV12(regraAjuste) : null,
                ["referenciaDemografica"] = configuracao.ReferenciaDemografica is { } referencia
                    ? SerializarReferenciaDemograficaV12(referencia)
                    : null,
            });
        }

        return array;
    }

    private static JsonArray SerializarVagasV12(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDistribuicaoVagas configuracao in OrdenarPorOfertaCursoOrigemIdV12(processo.DistribuicaoVagas))
        {
            JsonArray quadro = [];
            foreach (VagaOfertada vaga in configuracao.VagasOfertadas.OrderBy(static v => v.ModalidadeCodigo, StringComparer.Ordinal))
            {
                quadro.Add(new JsonObject
                {
                    ["modalidadeCodigo"] = HashCanonicalComputer.NormalizeNfc(vaga.ModalidadeCodigo),
                    ["quantidade"] = vaga.Quantidade,
                });
            }

            array.Add(new JsonObject
            {
                ["ofertaCursoOrigemId"] = configuracao.OfertaCursoOrigemId,
                ["quadro"] = quadro,
                ["vrNominal"] = configuracao.VrNominal,
                ["vrFinal"] = configuracao.VrFinal,
                ["estouro"] = configuracao.Estouro,
                ["capadoEmVo"] = configuracao.CapadoEmVo,
                ["totalPublicado"] = configuracao.TotalPublicado,
            });
        }

        return array;
    }

    private static JsonObject SerializarReferenciaDemograficaV12(ReferenciaReservaDemograficaSnapshot referencia) => new()
    {
        ["origemId"] = referencia.OrigemId,
        ["censoReferencia"] = HashCanonicalComputer.NormalizeNfc(referencia.CensoReferencia),
        ["ppiPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.PpiPercentual, EscalaPercentualV12),
        ["quilombolaPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.QuilombolaPercentual, EscalaPercentualV12),
        ["pcdPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.PcdPercentual, EscalaPercentualV12),
        ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(referencia.BaseLegal),
    };

    private static JsonArray SerializarModalidadesV12(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDistribuicaoVagas configuracao in OrdenarPorOfertaCursoOrigemIdV12(processo.DistribuicaoVagas))
        {
            IOrderedEnumerable<ModalidadeSelecionada> modalidadesOrdenadas = configuracao.Modalidades
                .OrderBy(static m => m.Codigo, StringComparer.Ordinal);
            foreach (ModalidadeSelecionada modalidade in modalidadesOrdenadas)
            {
                array.Add(new JsonObject
                {
                    ["ofertaCursoOrigemId"] = configuracao.OfertaCursoOrigemId,
                    ["modalidadeOrigemId"] = modalidade.ModalidadeOrigemId,
                    ["codigo"] = HashCanonicalComputer.NormalizeNfc(modalidade.Codigo),
                    ["descricao"] = modalidade.Descricao is { } descricao ? HashCanonicalComputer.NormalizeNfc(descricao) : null,
                    ["naturezaLegal"] = modalidade.NaturezaLegal.ToString(),
                    ["composicaoVagas"] = modalidade.ComposicaoVagas.ToString(),
                    ["composicaoOrigemCodigo"] = modalidade.ComposicaoOrigemCodigo,
                    ["regraRemanejamento"] = modalidade.RegraRemanejamento.ToString(),
                    ["remanejamentoDestino"] = modalidade.RemanejamentoDestino,
                    ["remanejamentoPar"] = modalidade.RemanejamentoPar,
                    ["remanejamentoFallback"] = modalidade.RemanejamentoFallback,
                    ["criteriosCumulativos"] = new JsonArray([.. modalidade.CriteriosCumulativos.Select(static c => (JsonNode?)JsonValue.Create(c))]),
                    ["acaoQuandoIndeferido"] = modalidade.AcaoQuandoIndeferido,
                    ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(modalidade.BaseLegal),
                    ["quantidadeDeclarada"] = modalidade.QuantidadeDeclarada,
                });
            }
        }

        return array;
    }

    private static IOrderedEnumerable<ConfiguracaoDistribuicaoVagas> OrdenarPorOfertaCursoOrigemIdV12(
        IEnumerable<ConfiguracaoDistribuicaoVagas> distribuicoes) =>
        distribuicoes.OrderBy(static d => d.OfertaCursoOrigemId);

    private static JsonArray SerializarOfertasV12(ProcessoSeletivo processo)
    {
        IEnumerable<string> ofertaIds = processo.DistribuicaoVagas
            .Select(static d => d.OfertaCursoOrigemId.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal);

        return new JsonArray([.. ofertaIds.Select(static id => (JsonNode?)JsonValue.Create(id))]);
    }

    private static JsonObject SerializarAtendimentoV12(ProcessoSeletivo processo)
    {
        if (processo.OfertaAtendimento is not { } oferta)
        {
            throw new InvalidOperationException(
                "Canonicalização de processo sem oferta de atendimento especializado — o gate de conformidade deveria ter recusado a transição antes deste ponto.");
        }

        return new JsonObject
        {
            ["condicoes"] = new JsonArray([.. oferta.Condicoes
                .OrderBy(static c => c.CondicaoOrigemId)
                .Select(static c => (JsonNode)new JsonObject
                {
                    ["condicaoOrigemId"] = c.CondicaoOrigemId,
                    ["condicaoCodigo"] = HashCanonicalComputer.NormalizeNfc(c.CondicaoCodigo),
                    ["condicaoNome"] = HashCanonicalComputer.NormalizeNfc(c.CondicaoNome),
                })]),
            ["recursos"] = new JsonArray([.. oferta.Recursos
                .OrderBy(static r => r.RecursoOrigemId)
                .Select(static r => (JsonNode)new JsonObject
                {
                    ["recursoOrigemId"] = r.RecursoOrigemId,
                    ["recursoNome"] = HashCanonicalComputer.NormalizeNfc(r.RecursoNome),
                })]),
            ["tiposDeficiencia"] = new JsonArray([.. oferta.TiposDeficiencia
                .OrderBy(static t => t.TipoDeficienciaOrigemId)
                .Select(static t => (JsonNode)new JsonObject
                {
                    ["tipoDeficienciaOrigemId"] = t.TipoDeficienciaOrigemId,
                    ["tipoDeficienciaNome"] = HashCanonicalComputer.NormalizeNfc(t.TipoDeficienciaNome),
                })]),
        };
    }

    private static JsonObject SerializarBonusRegionalV12(ProcessoSeletivo processo)
    {
        if (processo.BonusRegional is not { } bonus)
        {
            return new JsonObject { ["presente"] = false };
        }

        return new JsonObject
        {
            ["presente"] = true,
            ["regra"] = SerializarReferenciaRegraV12(bonus.Regra),
            ["fator"] = HashCanonicalComputer.SerializeDecimalCanonical(bonus.Fator, EscalaPadraoV12),
            ["teto"] = bonus.Teto is { } teto ? HashCanonicalComputer.SerializeDecimalCanonical(teto, EscalaPadraoV12) : null,
            ["municipioConvenio"] = bonus.MunicipioConvenio is { } municipio ? HashCanonicalComputer.NormalizeNfc(municipio) : null,
            ["baseLegal"] = bonus.BaseLegal is { } baseLegal ? HashCanonicalComputer.NormalizeNfc(baseLegal) : null,
        };
    }

    private static JsonArray SerializarCriteriosDesempateV12(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (CriterioDesempate criterio in processo.CriteriosDesempate.OrderBy(static c => c.Ordem))
        {
            array.Add(new JsonObject
            {
                ["ordem"] = criterio.Ordem,
                ["regra"] = SerializarReferenciaRegraV12(criterio.Regra),
                ["args"] = SerializarArgsCriterioDesempateV12(criterio.Args),
            });
        }

        return array;
    }

    private static JsonObject SerializarArgsCriterioDesempateV12(ArgsCriterioDesempate args) => args switch
    {
        ArgsDesempateMaiorNotaEtapa maiorNotaEtapa => new JsonObject { ["etapaRef"] = maiorNotaEtapa.EtapaRef },
        ArgsDesempateMaiorIdade => [],
        ArgsDesempateIdoso idoso => new JsonObject { ["idadeMinima"] = idoso.IdadeMinima },
        ArgsDesempatePredicadoFato predicadoFato => new JsonObject
        {
            ["fato"] = HashCanonicalComputer.NormalizeNfc(predicadoFato.Condicao.Fato),
            ["operador"] = predicadoFato.Condicao.Operador.ToCodigo(),
            ["valor"] = JsonNode.Parse(predicadoFato.Condicao.Valor.GetRawText()),
        },
        _ => throw new InvalidOperationException($"Variante de {nameof(ArgsCriterioDesempate)} não reconhecida: {args.GetType()}."),
    };

    private static JsonObject SerializarClassificacaoV12(ProcessoSeletivo processo)
    {
        if (processo.Classificacao is not { } classificacao)
        {
            throw new InvalidOperationException(
                "Canonicalização de processo sem configuração de classificação — o gate de conformidade deveria ter recusado a transição antes deste ponto.");
        }

        return new JsonObject
        {
            ["regraCalculo"] = SerializarReferenciaRegraV12(classificacao.RegraCalculo),
            ["regraArredondamento"] = classificacao.RegraArredondamento is { } arredondamento
                ? SerializarReferenciaRegraV12(arredondamento)
                : null,
            ["casasArredondamento"] = classificacao.CasasArredondamento,
            ["regraOrdemAlocacao"] = SerializarReferenciaRegraV12(classificacao.RegraOrdemAlocacao),
            ["nOpcoesAlocacao"] = classificacao.NOpcoesAlocacao,
            ["regrasEliminacao"] = OrdenarPorConteudoV12(classificacao.RegrasEliminacao
                .Select(static r => new JsonObject
                {
                    ["regra"] = SerializarReferenciaRegraV12(r.Regra),
                    ["args"] = SerializarArgsRegraEliminacaoV12(r.Args),
                })),
        };
    }

    private static JsonObject SerializarArgsRegraEliminacaoV12(ArgsRegraEliminacao args) => args switch
    {
        ArgsElimNotaMinimaEtapa notaMinima => new JsonObject
        {
            ["etapaRef"] = notaMinima.EtapaRef,
            ["notaMinima"] = HashCanonicalComputer.SerializeDecimalCanonical(notaMinima.NotaMinima, EscalaPadraoV12),
        },
        ArgsElimCorteRedacao corteRedacao => new JsonObject
        {
            ["minimo"] = HashCanonicalComputer.SerializeDecimalCanonical(corteRedacao.Minimo, EscalaPadraoV12),
        },
        ArgsElimZeroEmArea => [],
        _ => throw new InvalidOperationException($"Variante de {nameof(ArgsRegraEliminacao)} não reconhecida: {args.GetType()}."),
    };

    private static JsonObject SerializarHashesEditalV12(DadosEdital dados, string hashEdital) => new()
    {
        ["documentoEditalId"] = dados.DocumentoEditalId,
        ["hashSha256"] = hashEdital,
    };

    /// <summary>
    /// Forma 1.2 do bloco: <c>exigencias</c>/<c>obrigatoriedades</c>/
    /// <c>referenciaTemporalFatos</c>/<c>dataReferenciaFatos</c> — SEM <c>metadadosFatos</c>
    /// (Story #919, chave nova só a partir da 1.3).
    /// </summary>
    private static JsonObject SerializarDocumentosExigidosV12(ProcessoSeletivo processo, ResultadoConformidade? conformidade) => new()
    {
        ["exigencias"] = SerializarExigenciasV12(processo.DocumentosExigidos),
        ["obrigatoriedades"] = SerializarObrigatoriedadesV12(conformidade),
        ["referenciaTemporalFatos"] = processo.ReferenciaTemporalFatos is { } referencia ? new JsonObject
        {
            ["tipo"] = referencia.Tipo.ToCodigo(),
            ["data"] = referencia.Data?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["faseId"] = referencia.FaseId,
        }
            : null,
        ["dataReferenciaFatos"] = processo.ResolverDataReferenciaFatos() is { } data
            ? data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null,
    };

    private static JsonArray SerializarExigenciasV12(IReadOnlyCollection<DocumentoExigido> exigencias)
    {
        IOrderedEnumerable<DocumentoExigido> ordenadas = exigencias
            .OrderBy(static e => e.ExigidoNaFaseId)
            .ThenBy(static e => e.TipoDocumentoOrigemId)
            .ThenBy(static e => System.Text.Encoding.UTF8.GetString(
                HashCanonicalComputer.ComputeSnapshotBytes(SerializarExigenciaSemIdentidadeV12(e))),
                StringComparer.Ordinal)
            .ThenBy(static e => e.Id);

        return new JsonArray([.. ordenadas.Select(static e =>
        {
            JsonObject item = SerializarExigenciaSemIdentidadeV12(e);
            item.Insert(0, "exigenciaId", JsonValue.Create(e.Id));
            return (JsonNode)item;
        })]);
    }

    private static JsonObject SerializarExigenciaSemIdentidadeV12(DocumentoExigido exigencia) => new()
    {
        ["tipoDocumentoOrigemId"] = exigencia.TipoDocumentoOrigemId,
        ["tipoDocumentoCodigo"] = HashCanonicalComputer.NormalizeNfc(exigencia.TipoDocumentoCodigo),
        ["tipoDocumentoNome"] = HashCanonicalComputer.NormalizeNfc(exigencia.TipoDocumentoNome),
        ["tipoDocumentoCategoria"] = HashCanonicalComputer.NormalizeNfc(exigencia.TipoDocumentoCategoria),
        ["exigidoNaFaseId"] = exigencia.ExigidoNaFaseId,
        ["aplicabilidade"] = exigencia.Aplicabilidade.ToString(),
        ["obrigatorio"] = exigencia.Obrigatorio,
        ["consequenciaIndeferimento"] = exigencia.ConsequenciaIndeferimento is { } consequencia
            ? HashCanonicalComputer.NormalizeNfc(consequencia)
            : null,
        ["grupoSatisfacaoId"] = exigencia.GrupoSatisfacaoId,
        ["condicaoGatilho"] = SerializarCondicaoGatilhoV12(exigencia.Condicoes),
        ["basesLegais"] = SerializarBasesLegaisV12(exigencia.BasesLegaisResolvidas()),
        ["idadeMaximaEmissao"] = exigencia.IdadeMaximaEmissao is { } idade ? SerializarIdadeMaximaEmissaoV12(idade) : null,
        ["formatosPermitidos"] = SerializarFormatosPermitidosV12(exigencia.FormatosPermitidos),
        ["tamanhoMaximoBytes"] = exigencia.TamanhoMaximoBytes,
    };

    private static JsonObject SerializarFormatosPermitidosV12(FormatosPermitidos formatosPermitidos) => new()
    {
        ["qualquer"] = formatosPermitidos.Qualquer,
        ["lista"] = formatosPermitidos.Lista is { } lista
            ? new JsonArray([.. lista.Select(static e => new JsonObject
            {
                ["formato"] = e.Formato.ToCodigo(),
                ["tamanhoMaximoBytesMax"] = e.TamanhoMaximoBytesMax,
            })])
            : null,
    };

    private static JsonArray? SerializarCondicaoGatilhoV12(IReadOnlyCollection<CondicaoGatilho> condicoes)
    {
        if (condicoes.Count == 0)
        {
            return null;
        }

        JsonArray clausulas = [];
        foreach (IGrouping<int, CondicaoGatilho> clausula in condicoes.GroupBy(static c => c.Clausula).OrderBy(static g => g.Key))
        {
            clausulas.Add(OrdenarPorConteudoV12(clausula.Select(static c => new JsonObject
            {
                ["fato"] = HashCanonicalComputer.NormalizeNfc(c.Fato),
                ["operador"] = c.Operador.ToCodigo(),
                ["valor"] = JsonNode.Parse(c.Valor.GetRawText()),
            })));
        }

        return clausulas;
    }

    private static JsonArray SerializarBasesLegaisV12(IEnumerable<DocumentoExigidoBaseLegal> basesLegais) =>
        OrdenarPorConteudoV12(basesLegais.Select(static b => new JsonObject
        {
            ["referencia"] = HashCanonicalComputer.NormalizeNfc(b.Referencia),
            ["abrangencia"] = b.Abrangencia.ToCodigo(),
            ["status"] = b.Status.ToCodigo(),
            ["observacao"] = b.Observacao is { } observacao ? HashCanonicalComputer.NormalizeNfc(observacao) : null,
        }));

    private static JsonObject SerializarIdadeMaximaEmissaoV12(IdadeMaximaEmissao idade) => new()
    {
        ["valor"] = idade.Valor,
        ["unidade"] = idade.Unidade.ToCodigo(),
        ["referenciaTipo"] = idade.ReferenciaTipo.ToCodigo(),
        ["data"] = idade.Data?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["referenciaFaseId"] = idade.ReferenciaFaseId,
    };

    private static JsonArray SerializarObrigatoriedadesV12(ResultadoConformidade? conformidade)
    {
        JsonArray array = [];
        if (conformidade is null)
        {
            return array;
        }

        foreach (RegraAvaliada regra in conformidade.Regras.OrderBy(static r => r.RegraId))
        {
            array.Add(new JsonObject
            {
                ["regraId"] = regra.RegraId,
                ["regraCodigo"] = HashCanonicalComputer.NormalizeNfc(regra.RegraCodigo),
                ["categoria"] = regra.Categoria.ToString(),
                ["tipoProcessoCodigoAvaliado"] = HashCanonicalComputer.NormalizeNfc(regra.TipoProcessoCodigoAvaliado),
                ["predicado"] = SerializarPredicadoObrigatoriedadeV12(regra.Predicado),
                ["aprovada"] = regra.Aprovada,
                ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(regra.BaseLegal),
                ["atoNormativoUrl"] = regra.AtoNormativoUrl is { } url ? HashCanonicalComputer.NormalizeNfc(url) : null,
                ["portariaInterna"] = regra.PortariaInterna is { } portaria ? HashCanonicalComputer.NormalizeNfc(portaria) : null,
                ["descricaoHumana"] = HashCanonicalComputer.NormalizeNfc(regra.DescricaoHumana),
                ["vigenciaInicio"] = regra.VigenciaInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["vigenciaFim"] = regra.VigenciaFim?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["hash"] = regra.Hash,
            });
        }

        return array;
    }

    private static JsonObject SerializarPredicadoObrigatoriedadeV12(PredicadoObrigatoriedade predicado)
    {
        (string tipo, JsonObject args) = predicado switch
        {
            EtapaObrigatoria p => ("etapaObrigatoria", new JsonObject
            {
                ["tipoEtapaCodigo"] = HashCanonicalComputer.NormalizeNfc(p.TipoEtapaCodigo),
            }),
            ModalidadesMinimas p => ("modalidadesMinimas", new JsonObject
            {
                ["codigos"] = new JsonArray([.. p.Codigos.Select(static c => JsonValue.Create(HashCanonicalComputer.NormalizeNfc(c)))]),
            }),
            DesempateDeveIncluir p => ("desempateDeveIncluir", new JsonObject
            {
                ["criterio"] = HashCanonicalComputer.NormalizeNfc(p.Criterio),
            }),
            DocumentoObrigatorioParaModalidade p => ("documentoObrigatorioParaModalidade", new JsonObject
            {
                ["modalidade"] = HashCanonicalComputer.NormalizeNfc(p.Modalidade),
                ["tipoDocumento"] = HashCanonicalComputer.NormalizeNfc(p.TipoDocumento),
            }),
            AtendimentoDisponivel p => ("atendimentoDisponivel", new JsonObject
            {
                ["necessidades"] = new JsonArray([.. p.Necessidades.Select(static n => JsonValue.Create(HashCanonicalComputer.NormalizeNfc(n)))]),
            }),
            ConcorrenciaDuplaObrigatoria => ("concorrenciaDuplaObrigatoria", []),
            Customizado p => ("customizado", new JsonObject
            {
                ["parametros"] = JsonNode.Parse(p.Parametros.GetRawText()),
            }),
            _ => throw new InvalidOperationException(
                $"Variante de {nameof(PredicadoObrigatoriedade)} não reconhecida: {predicado.GetType()}."),
        };

        return new JsonObject
        {
            ["tipo"] = tipo,
            ["args"] = args,
        };
    }

    private static JsonObject SerializarCronogramaFasesV12(ProcessoSeletivo processo) => new()
    {
        ["origemCandidatos"] = processo.OrigemCandidatos.ToString(),
        ["fases"] = SerializarFasesCronogramaV12(processo),
    };

    private static JsonArray SerializarFasesCronogramaV12(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        IOrderedEnumerable<FaseCronograma> ordenadas = processo.CronogramaFases
            .OrderBy(static f => f.Ordem)
            .ThenBy(static f => f.Id);
        foreach (FaseCronograma fase in ordenadas)
        {
            array.Add(new JsonObject
            {
                ["id"] = fase.Id,
                ["ordem"] = fase.Ordem,
                ["faseCanonicaOrigemId"] = fase.FaseCanonicaOrigemId,
                ["codigo"] = HashCanonicalComputer.NormalizeNfc(fase.Codigo),
                ["donoInstitucional"] = HashCanonicalComputer.NormalizeNfc(fase.DonoInstitucional),
                ["origemData"] = fase.OrigemData.ToString(),
                ["agrupaEtapas"] = fase.AgrupaEtapas,
                ["permiteComplementacao"] = fase.PermiteComplementacao,
                ["produzResultado"] = fase.ProduzResultado,
                ["resultadoDefinitivo"] = fase.ResultadoDefinitivo,
                ["coletaInscricao"] = fase.ColetaInscricao,
                ["inicio"] = fase.Inicio is { } inicio ? HashCanonicalComputer.SerializeInstantCanonical(inicio) : null,
                ["fim"] = fase.Fim is { } fim ? HashCanonicalComputer.SerializeInstantCanonical(fim) : null,
                ["atoProduzidoCodigo"] = fase.AtoProduzidoCodigo is { } atoCodigo ? HashCanonicalComputer.NormalizeNfc(atoCodigo) : null,
                ["atoProduzidoEfeitoIrreversivel"] = fase.AtoProduzidoEfeitoIrreversivel,
                ["bancasRequeridas"] = SerializarBancasRequeridasV12(fase),
                ["regraRecurso"] = fase.RegraRecurso is { } regraRecurso ? SerializarRegraRecursoFaseV12(regraRecurso) : null,
            });
        }

        return array;
    }

    private static JsonArray SerializarBancasRequeridasV12(FaseCronograma fase)
    {
        IOrderedEnumerable<BancaRequerida> ordenadas = fase.BancasRequeridas
            .OrderBy(static b => b.TipoBancaOrigemId)
            .ThenBy(static b => b.Codigo, StringComparer.Ordinal);

        return new JsonArray([.. ordenadas.Select(static b => (JsonNode)new JsonObject
        {
            ["tipoBancaOrigemId"] = b.TipoBancaOrigemId,
            ["codigo"] = HashCanonicalComputer.NormalizeNfc(b.Codigo),
        })]);
    }

    private static JsonObject SerializarRegraRecursoFaseV12(RegraRecursoFase regraRecurso) => new()
    {
        ["regra"] = SerializarReferenciaRegraV12(regraRecurso.Regra),
        ["args"] = SerializarArgsRegraPrazoRecursoV12(regraRecurso.Args),
    };

    private static JsonObject SerializarArgsRegraPrazoRecursoV12(ArgsRegraPrazoRecurso args) => new()
    {
        ["prazoValor"] = HashCanonicalComputer.SerializeDecimalCanonical(args.PrazoValor, EscalaPadraoV12),
        ["prazoUnidade"] = args.PrazoUnidade.ToString(),
        ["atoAncoraCodigo"] = HashCanonicalComputer.NormalizeNfc(args.AtoAncoraCodigo),
        ["suspensividadePrimeiraInstanciaValor"] = args.SuspensividadePrimeiraInstanciaValor is { } v1
            ? HashCanonicalComputer.SerializeDecimalCanonical(v1, EscalaPadraoV12)
            : null,
        ["suspensividadePrimeiraInstanciaUnidade"] = args.SuspensividadePrimeiraInstanciaUnidade?.ToString(),
        ["suspensividadeSegundaInstanciaValor"] = args.SuspensividadeSegundaInstanciaValor is { } v2
            ? HashCanonicalComputer.SerializeDecimalCanonical(v2, EscalaPadraoV12)
            : null,
        ["suspensividadeSegundaInstanciaUnidade"] = args.SuspensividadeSegundaInstanciaUnidade?.ToString(),
    };

    private static JsonArray OrdenarPorConteudoV12(IEnumerable<JsonObject> itens)
    {
        IOrderedEnumerable<JsonObject> ordenados = itens.OrderBy(
            static item => System.Text.Encoding.UTF8.GetString(HashCanonicalComputer.ComputeSnapshotBytes(item)),
            StringComparer.Ordinal);

        return new JsonArray([.. ordenados.Select(static item => (JsonNode)item)]);
    }

    private static JsonObject SerializarReferenciaRegraV12(ReferenciaRegra regra) => new()
    {
        ["codigo"] = regra.Codigo,
        ["versao"] = regra.Versao,
        ["hash"] = regra.Hash,
    };
}

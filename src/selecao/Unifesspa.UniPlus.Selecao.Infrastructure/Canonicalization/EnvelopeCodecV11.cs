namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Codec da versão <c>1.1</c> do envelope (ADR-0109, ADR-0110 D1): o
/// <see cref="SnapshotPublicacaoCanonicalizer"/> como <b>encoder</b>, e o decoder que
/// reconstrói o grafo de volta em entidades.
/// </summary>
/// <remarks>
/// <para>
/// <b>O decoder é a peça de risco do módulo.</b> Ele é o inverso de uma projeção que
/// produz a evidência jurídica do certame: um campo que ele perca não aparece em lugar
/// nenhum — o descarte de uma sessão editorial repõe a configuração <b>sem</b> aquele
/// campo, e o certame publicado passa a divergir do documento que o publicou. Por isso
/// toda leitura é fechada (<see cref="LeitorEnvelope"/>), o vocabulário de regras é
/// fechado, e a fidelidade é <b>provada</b> por round-trip byte-a-byte, não presumida.
/// </para>
/// <para>
/// <b>A variante de <c>args</c> vem do código da regra, não do JSON.</b>
/// <c>DESEMPATE-MAIOR-IDADE</c> e <c>ELIM-ZERO-EM-AREA</c> serializam como <c>{}</c> —
/// não há discriminador dentro do envelope. É o <c>regra.codigo</c> que decide a
/// variante, e é por isso que um código fora do rol tem de ser recusado em vez de
/// ignorado.
/// </para>
/// </remarks>
public sealed class EnvelopeCodecV11 : IEnvelopeCodec
{
    /// <summary>
    /// Os 6 blocos sem dono (ADR-0109 D8). Um bloco <b>real</b> nunca emite
    /// <c>nao_construido</c>; um stub que virou objeto rico é forma nova, e forma nova é
    /// bump de versão.
    /// </summary>
    private static readonly string[] Stubs =
    [
        "vagas",
        "documentosExigidos",
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
        // Story #851 — cronogramaFases deixa de ser stub.
        "cronogramaFases",
    ];

    /// <summary>
    /// Chave duplicada é <b>erro</b>, não “a última ganha”. Um envelope com
    /// <c>"fator"</c> duas vezes tem duas leituras possíveis, e a que o hash cobre é
    /// indistinguível da que o parser escolheria.
    /// </summary>
    private static readonly JsonDocumentOptions OpcoesDocumento = new() { AllowDuplicateProperties = false };

    /// <summary>
    /// As ações ao indeferir que o <b>cadastro</b> admite — domínio fechado
    /// (<c>AcoesQuandoIndeferido</c>, módulo Configuração). O comando nunca produz outra
    /// coisa: ele <b>copia</b> o token da view do cadastro.
    /// </summary>
    /// <remarks>
    /// O envelope o congela por valor (ADR-0061), e o decoder não passa pelo cadastro — sem
    /// esta lista, um token inventado faria round-trip perfeito e restauraria uma
    /// <b>instrução que o motor de homologação não sabe executar</b>. A lista está aqui, e
    /// não numa referência ao módulo Configuração, porque ela é a da <b>forma 1.1</b>: um
    /// token novo no cadastro é uma forma nova do envelope, com o seu próprio codec.
    /// </remarks>
    private static readonly string[] AcoesQuandoIndeferido =
        ["RECLASSIFICAR_AC", "RECLASSIFICAR_REGRA_EDITAL"];

    /// <summary>
    /// O formato que o <c>CodigoModalidade</c> do cadastro impõe (<c>^[A-Z0-9_]+$</c>). Um
    /// código fora dele nunca sai do cadastro — mas é <b>chave</b> de composição e de
    /// remanejamento dentro da oferta, e um envelope adulterado o usaria como tal.
    /// </summary>
    private static readonly Regex FormatoCodigoModalidade =
        new("^[A-Z0-9_]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// O formato que o <c>CodigoCondicao</c> do cadastro impõe
    /// (<c>^[A-Z][A-Z0-9_]{1,49}$</c>). O código da condição é <b>chave natural</b> — é por
    /// ele que a invariante ADR-0067 reconhece a condição PcD (<c>CodigoCondicaoPcd</c>) —,
    /// e um <c>"pcd"</c> minúsculo restaurado do envelope seria uma condição que o cadastro
    /// não produz e que o resto do código trata como chave.
    /// </summary>
    private static readonly Regex FormatoCodigoCondicao =
        new("^[A-Z][A-Z0-9_]{1,49}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private const int EscalaPadrao = 4;
    private const int EscalaPercentual = 2;

    private readonly SnapshotPublicacaoCanonicalizer _encoder = new();

    public string SchemaVersion => "1.1";

    public string AlgoritmoHash => "canonical-json/sha256@v1";

    public bool TemEncoder => true;

    public bool TemDecoder => true;

    public string? MotivoDaRecusa => null;

    /// <summary>
    /// O encoder <c>1.1</c> é o canonicalizador de hoje — e enquanto ele for também o
    /// corrente, a delegação basta. No dia do bump para <c>1.2</c>, esta guarda dispara:
    /// o encoder <c>1.1</c> tem de virar código <b>congelado</b> aqui dentro, em vez de
    /// a <c>1.1</c> passar a emitir <c>1.2</c> em silêncio — que tornaria não
    /// verificável o descarte de todo certame congelado antes do bump.
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

        Result<JsonObject> parse = Parsear(versao.ConfiguracaoCongeladaCanonica);
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

        DadosEdital? dados = LerDadosEdital(leitor, payload, out string hashDocumento);
        IReadOnlyList<EtapaProcesso> etapas = LerEtapas(leitor, payload);
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicao = LerDistribuicao(leitor, payload);
        OfertaAtendimentoEspecializado? atendimento = LerAtendimento(leitor, payload);
        ConfiguracaoBonusRegional? bonus = LerBonusRegional(leitor, payload);
        IReadOnlyList<CriterioDesempate> desempate = LerCriteriosDesempate(leitor, payload);
        ConfiguracaoClassificacao? classificacao = LerClassificacao(leitor, payload);
        IReadOnlyList<FaseCronograma> cronogramaFases = LerCronogramaFases(leitor, payload);
        RetificacaoInfo? retificacao = temRetificacao ? LerRetificacao(leitor, payload) : null;

        if (leitor.Falhou)
        {
            return leitor.Falha<EnvelopeReidratado>();
        }

        if (VerificarCoerenciaComAVersao(versao, hashDocumento, retificacao) is { } incoerencia)
        {
            return Result<EnvelopeReidratado>.Failure(incoerencia);
        }

        GrafoConfiguracao grafo = new(etapas, atendimento!, distribuicao, bonus, desempate, classificacao!, cronogramaFases);
        return Result<EnvelopeReidratado>.Success(new EnvelopeReidratado(grafo, dados!, hashDocumento, retificacao));
    }

    /// <summary>
    /// Parse com duas guardas que o hash sozinho não dá: <b>sem chave duplicada</b> e
    /// <b>canônico</b>. Reserializar o que foi lido tem de reproduzir os bytes — um
    /// payload com chaves fora de ordem, com espaços, ou com uma chave repetida (de que
    /// o parser escolheria uma) não é um envelope <c>1.1</c>, é outra coisa com o mesmo
    /// hash recomputado por quem o adulterou.
    /// </summary>
    private static Result<JsonObject> Parsear(byte[] bytes)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(bytes, nodeOptions: null, OpcoesDocumento);
        }
        catch (JsonException excecao)
        {
            return Result<JsonObject>.Failure(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"Os bytes congelados não são um JSON válido e sem chaves duplicadas: {excecao.Message}"));
        }

        if (node is not JsonObject payload)
        {
            return Result<JsonObject>.Failure(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "Os bytes congelados não são um objeto JSON."));
        }

        byte[] recanonicalizado = HashCanonicalComputer.ComputeSnapshotBytes(payload);
        if (!recanonicalizado.AsSpan().SequenceEqual(bytes))
        {
            return Result<JsonObject>.Failure(new DomainError(
                ErrosCodecEnvelope.IntegridadeViolada,
                "Os bytes congelados não estão na forma canônica (ADR-0100) — reserializá-los produz bytes distintos."));
        }

        return Result<JsonObject>.Success(payload);
    }

    /// <summary>
    /// O envelope não pode contradizer a linha que o guarda. O hash do ato aparece nos
    /// dois lugares, e a cadeia de retificação também: a versão 1 <b>não</b> tem o 18º
    /// bloco, e toda versão <c>N &gt; 1</c> tem — apontando para o mesmo ato que a
    /// coluna aponta. Divergir aqui significa que uma das duas evidências está errada, e
    /// não há como saber qual.
    /// </summary>
    private static DomainError? VerificarCoerenciaComAVersao(
        VersaoConfiguracao versao,
        string hashDocumento,
        RetificacaoInfo? retificacao)
    {
        if (!string.Equals(hashDocumento, versao.AtoCriadorHash, StringComparison.Ordinal))
        {
            return new DomainError(
                ErrosCodecEnvelope.EnvelopeIncoerenteComAVersao,
                "O hash do documento congelado no bloco 'hashesEdital' não é o do ato criador da versão.");
        }

        if (versao.NumeroVersao == 1)
        {
            return retificacao is null
                ? null
                : new DomainError(
                    ErrosCodecEnvelope.EnvelopeIncoerenteComAVersao,
                    "A versão 1 abre a cadeia e não retifica ato algum — o bloco 'retificacao' não pode existir nela.");
        }

        if (retificacao is null)
        {
            return new DomainError(
                ErrosCodecEnvelope.EnvelopeIncoerenteComAVersao,
                $"A versão {versao.NumeroVersao} sucede outra e tem de carregar o bloco 'retificacao'.");
        }

        return retificacao.EditalRetificadoId == versao.AtoCriadorRetificaId
            ? null
            : new DomainError(
                ErrosCodecEnvelope.EnvelopeIncoerenteComAVersao,
                "O ato retificado declarado no bloco 'retificacao' não é o que a versão registra ter emendado.");
    }

    private static DadosEdital? LerDadosEdital(LeitorEnvelope leitor, JsonObject payload, out string hashDocumento)
    {
        hashDocumento = string.Empty;

        JsonObject periodo = leitor.Objeto(payload, "periodo", "$");
        leitor.ExigirChaves(periodo, "periodo", "numero", "inicio", "fim");
        string? numero = leitor.TextoOpcional(periodo, "numero", "periodo", LimitesDoEnvelope.NumeroDoAto);
        DateOnly inicio = leitor.Data(periodo, "inicio", "periodo");
        DateOnly fim = leitor.Data(periodo, "fim", "periodo");

        JsonObject hashes = leitor.Objeto(payload, "hashesEdital", "$");
        leitor.ExigirChaves(hashes, "hashesEdital", "documentoEditalId", "hashSha256");
        Guid documentoId = leitor.Identificador(hashes, "documentoEditalId", "hashesEdital");
        string hash = leitor.Texto(hashes, "hashSha256", "hashesEdital");

        if (leitor.Falhou)
        {
            return null;
        }

        hashDocumento = hash;

        Result<DadosEdital> dados = DadosEdital.Criar(numero, inicio, fim, documentoId);
        return dados.IsFailure ? leitor.Propagar<DadosEdital>(dados.Error!) : dados.Value;
    }

    private static IReadOnlyList<EtapaProcesso> LerEtapas(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray array = leitor.Array(payload, "etapas", "$");
        if (leitor.Falhou)
        {
            return [];
        }

        List<EtapaProcesso> etapas = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"etapas[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "etapas");
            leitor.ExigirChaves(item, path, "id", "nome", "carater", "peso", "notaMinima", "ordem");

            Guid id = leitor.Identificador(item, "id", path);
            string nome = leitor.TextoNaoVazio(item, "nome", path, LimitesDoEnvelope.EtapaNome);
            CaraterEtapa carater = leitor.Enumeracao<CaraterEtapa>(item, "carater", path);
            decimal? peso = leitor.DecimalOpcional(item, "peso", EscalaPadrao, path, LimitesDoEnvelope.PrecisaoEtapa);
            decimal? notaMinima = leitor.DecimalOpcional(item, "notaMinima", EscalaPadrao, path, LimitesDoEnvelope.PrecisaoEtapa);
            int? ordem = leitor.InteiroOpcional(item, "ordem", path);

            if (leitor.Falhou)
            {
                return [];
            }

            // Limites que hoje só o FluentValidation do comando impõe — a entidade não os
            // conhece. Um envelope legítimo já os satisfaz (passou pelo validator quando
            // foi criado); recusá-los aqui custa nada e fecha a porta a um envelope
            // adulterado que reidrataria num agregado impossível de persistir.
            if (carater == CaraterEtapa.Nenhum
                || peso is <= 0
                || notaMinima is < 0
                || ordem is <= 0)
            {
                return leitor.Propagar<IReadOnlyList<EtapaProcesso>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"Envelope malformado em '{path}': peso e ordem devem ser maiores que zero e a nota mínima não negativa.")) ?? [];
            }

            etapas.Add(EtapaProcesso.Reidratar(id, nome, carater, peso, notaMinima, ordem));
        }

        return etapas;
    }

    /// <summary>
    /// Recombina os <b>três blocos derivados</b> da mesma coleção — <c>distribuicao</c>,
    /// <c>modalidades</c> e <c>ofertas</c> (ADR-0110 D8). A exigência é <b>igualdade
    /// exata de conjuntos</b> de <c>ofertaCursoOrigemId</c>, não mera inclusão: recombinar
    /// em silêncio um envelope incoerente reconstruiria um agregado que <b>nunca
    /// existiu</b> — uma modalidade sem oferta, uma oferta sem vagas.
    /// </summary>
    private static IReadOnlyList<ConfiguracaoDistribuicaoVagas> LerDistribuicao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray arrayDistribuicao = leitor.Array(payload, "distribuicao", "$");
        JsonArray arrayModalidades = leitor.Array(payload, "modalidades", "$");
        IReadOnlyList<string> ofertasDeclaradas = leitor.Textos(payload, "ofertas", "$");
        if (leitor.Falhou)
        {
            return [];
        }

        Dictionary<Guid, List<ModalidadeSelecionada>> modalidadesPorOferta = [];
        List<Guid> ofertasEmModalidades = [];
        for (int i = 0; i < arrayModalidades.Count; i++)
        {
            string path = $"modalidades[{i}]";
            JsonObject item = leitor.ItemObjeto(arrayModalidades, i, "modalidades");
            Guid ofertaId = leitor.Identificador(item, "ofertaCursoOrigemId", path);
            ModalidadeSelecionada? modalidade = LerModalidade(leitor, item, path);
            if (leitor.Falhou)
            {
                return [];
            }

            ofertasEmModalidades.Add(ofertaId);
            if (!modalidadesPorOferta.TryGetValue(ofertaId, out List<ModalidadeSelecionada>? lista))
            {
                lista = [];
                modalidadesPorOferta[ofertaId] = lista;
            }

            lista.Add(modalidade!);
        }

        List<Guid> ofertasEmDistribuicao = [];
        List<(Guid Oferta, int VoBase, decimal Pr, ReferenciaRegra Regra, ReferenciaReservaDemograficaSnapshot? Demografica)> inputs = [];
        for (int i = 0; i < arrayDistribuicao.Count; i++)
        {
            string path = $"distribuicao[{i}]";
            JsonObject item = leitor.ItemObjeto(arrayDistribuicao, i, "distribuicao");
            leitor.ExigirChaves(item, path, "ofertaCursoOrigemId", "voBase", "pr", "regraDistribuicao", "referenciaDemografica");

            Guid ofertaId = leitor.Identificador(item, "ofertaCursoOrigemId", path);
            int voBase = leitor.Inteiro(item, "voBase", path);
            decimal pr = leitor.Decimal(item, "pr", EscalaPadrao, path, LimitesDoEnvelope.PrecisaoPr);
            ReferenciaRegra regra = leitor.Regra(
                item,
                "regraDistribuicao",
                path,
                RegraDistribuicaoVagasCodigo.Lei12711,
                RegraDistribuicaoVagasCodigo.Institucional);
            ReferenciaReservaDemograficaSnapshot? demografica = LerReferenciaDemografica(leitor, item, path);

            if (leitor.Falhou)
            {
                return [];
            }

            ofertasEmDistribuicao.Add(ofertaId);
            inputs.Add((ofertaId, voBase, pr, regra, demografica));
        }

        if (VerificarBlocosDerivados(ofertasEmDistribuicao, ofertasEmModalidades, ofertasDeclaradas) is { } incoerencia)
        {
            return leitor.Propagar<IReadOnlyList<ConfiguracaoDistribuicaoVagas>>(incoerencia) ?? [];
        }

        List<ConfiguracaoDistribuicaoVagas> distribuicao = [];
        foreach ((Guid oferta, int voBase, decimal pr, ReferenciaRegra regra, ReferenciaReservaDemograficaSnapshot? demografica) in inputs)
        {
            Result<ConfiguracaoDistribuicaoVagas> configuracao = ConfiguracaoDistribuicaoVagas.Criar(
                oferta, voBase, pr, regra, demografica, modalidadesPorOferta[oferta]);
            if (configuracao.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<ConfiguracaoDistribuicaoVagas>>(configuracao.Error!) ?? [];
            }

            distribuicao.Add(configuracao.Value!);
        }

        return distribuicao;
    }

    private static DomainError? VerificarBlocosDerivados(
        List<Guid> emDistribuicao,
        List<Guid> emModalidades,
        IReadOnlyList<string> emOfertas)
    {
        if (emDistribuicao.Distinct().Count() != emDistribuicao.Count)
        {
            return new DomainError(
                ErrosCodecEnvelope.BlocosDerivadosIncoerentes,
                "O bloco 'distribuicao' repete uma oferta de curso — cada oferta tem no máximo uma distribuição.");
        }

        List<Guid> ofertas = [];
        foreach (string texto in emOfertas)
        {
            if (!Guid.TryParseExact(texto, "D", out Guid oferta)
                || !string.Equals(oferta.ToString(), texto, StringComparison.Ordinal)
                || oferta == Guid.Empty)
            {
                return new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"Envelope malformado em 'ofertas': '{texto}' não é um Guid canônico não vazio.");
            }

            ofertas.Add(oferta);
        }

        if (ofertas.Distinct().Count() != ofertas.Count)
        {
            return new DomainError(
                ErrosCodecEnvelope.BlocosDerivadosIncoerentes,
                "O bloco 'ofertas' repete uma oferta de curso.");
        }

        HashSet<Guid> conjuntoDistribuicao = [.. emDistribuicao];
        HashSet<Guid> conjuntoOfertas = [.. ofertas];
        HashSet<Guid> conjuntoModalidades = [.. emModalidades];

        if (!conjuntoDistribuicao.SetEquals(conjuntoOfertas) || !conjuntoDistribuicao.SetEquals(conjuntoModalidades))
        {
            return new DomainError(
                ErrosCodecEnvelope.BlocosDerivadosIncoerentes,
                "Os blocos 'distribuicao', 'modalidades' e 'ofertas' derivam da mesma coleção e não declaram o mesmo conjunto de ofertas de curso (ADR-0110 D8).");
        }

        return null;
    }

    private static ReferenciaReservaDemograficaSnapshot? LerReferenciaDemografica(
        LeitorEnvelope leitor,
        JsonObject distribuicao,
        string pathPai)
    {
        JsonObject? item = leitor.ObjetoOpcional(distribuicao, "referenciaDemografica", pathPai);
        if (leitor.Falhou || item is null)
        {
            return null;
        }

        string path = $"{pathPai}.referenciaDemografica";
        leitor.ExigirChaves(
            item,
            path,
            "origemId",
            "censoReferencia",
            "ppiPercentual",
            "quilombolaPercentual",
            "pcdPercentual",
            "baseLegal");

        Guid origemId = leitor.Identificador(item, "origemId", path);
        string censo = leitor.TextoNaoVazio(item, "censoReferencia", path, LimitesDoEnvelope.CensoReferencia);
        decimal ppi = leitor.Decimal(item, "ppiPercentual", EscalaPercentual, path, LimitesDoEnvelope.PrecisaoPercentual);
        decimal quilombola = leitor.Decimal(item, "quilombolaPercentual", EscalaPercentual, path, LimitesDoEnvelope.PrecisaoPercentual);
        decimal pcd = leitor.Decimal(item, "pcdPercentual", EscalaPercentual, path, LimitesDoEnvelope.PrecisaoPercentual);
        string baseLegal = leitor.TextoNaoVazio(item, "baseLegal", path, LimitesDoEnvelope.BaseLegal);

        if (leitor.Falhou)
        {
            return null;
        }

        Result<ReferenciaReservaDemograficaSnapshot> referencia =
            ReferenciaReservaDemograficaSnapshot.Criar(origemId, censo, ppi, quilombola, pcd, baseLegal);

        return referencia.IsFailure
            ? leitor.Propagar<ReferenciaReservaDemograficaSnapshot>(referencia.Error!)
            : referencia.Value;
    }

    private static ModalidadeSelecionada? LerModalidade(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(
            item,
            path,
            "ofertaCursoOrigemId",
            "modalidadeOrigemId",
            "codigo",
            "descricao",
            "naturezaLegal",
            "composicaoVagas",
            "composicaoOrigemCodigo",
            "regraRemanejamento",
            "remanejamentoDestino",
            "remanejamentoPar",
            "remanejamentoFallback",
            "criteriosCumulativos",
            "acaoQuandoIndeferido",
            "baseLegal");

        Guid modalidadeOrigemId = leitor.Identificador(item, "modalidadeOrigemId", path);
        string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.ModalidadeCodigo);
        string? descricao = leitor.TextoOpcional(item, "descricao", path, LimitesDoEnvelope.ModalidadeDescricao);
        NaturezaLegalModalidade natureza = leitor.Enumeracao<NaturezaLegalModalidade>(item, "naturezaLegal", path);
        ComposicaoVagasModalidade composicao = leitor.Enumeracao<ComposicaoVagasModalidade>(item, "composicaoVagas", path);
        string? composicaoOrigem = leitor.TextoOpcional(item, "composicaoOrigemCodigo", path, LimitesDoEnvelope.ModalidadeCodigo);
        RegraRemanejamentoModalidade remanejamento = leitor.Enumeracao<RegraRemanejamentoModalidade>(item, "regraRemanejamento", path);
        string? destino = leitor.TextoOpcional(item, "remanejamentoDestino", path, LimitesDoEnvelope.ModalidadeCodigo);
        string? par = leitor.TextoOpcional(item, "remanejamentoPar", path, LimitesDoEnvelope.ModalidadeCodigo);
        string? fallback = leitor.TextoOpcional(item, "remanejamentoFallback", path, LimitesDoEnvelope.ModalidadeCodigo);

        // A ordem de `criteriosCumulativos` é a de ENTRADA — o encoder não a reordena
        // (é array de escalares, sem chave de conteúdo). Reordenar aqui produziria bytes
        // distintos dos congelados, e o round-trip acusaria.
        IReadOnlyList<string> criterios = leitor.Textos(item, "criteriosCumulativos", path);
        string? acaoQuandoIndeferido = leitor.TextoOpcional(item, "acaoQuandoIndeferido", path, LimitesDoEnvelope.Token);
        string baseLegal = leitor.TextoNaoVazio(item, "baseLegal", path, LimitesDoEnvelope.BaseLegal);

        if (leitor.Falhou)
        {
            return null;
        }

        if (VocabularioDaModalidade(codigo, acaoQuandoIndeferido, composicaoOrigem, destino, par, fallback) is { } vocabulario)
        {
            return leitor.Propagar<ModalidadeSelecionada>(vocabulario);
        }

        if (CoerenciaNaturezaRemanejamento(codigo, natureza, remanejamento) is { } incoerencia)
        {
            return leitor.Propagar<ModalidadeSelecionada>(incoerencia);
        }

        Result<ModalidadeSelecionada> modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId,
            codigo,
            descricao,
            natureza,
            composicao,
            composicaoOrigem,
            remanejamento,
            destino,
            par,
            fallback,
            criterios,
            acaoQuandoIndeferido,
            baseLegal);

        return modalidade.IsFailure ? leitor.Propagar<ModalidadeSelecionada>(modalidade.Error!) : modalidade.Value;
    }

    /// <summary>
    /// O <b>vocabulário</b> da modalidade — os tokens que só o cadastro produz.
    /// </summary>
    /// <remarks>
    /// O código é <b>chave</b>: a composição (<c>RETIRA_DE</c>) e o remanejamento
    /// (<c>DESTINO_UNICO</c>, <c>CRUZADO</c>) apontam para códigos de outras modalidades da
    /// mesma oferta. Um código fora do formato do cadastro (<c>^[A-Z0-9_]+$</c>) nunca sai
    /// de lá — mas um envelope adulterado o usaria como chave, e o motor de vagas do certame
    /// receberia um grafo de remanejamento cujos nós não existem no cadastro.
    /// </remarks>
    private static DomainError? VocabularioDaModalidade(
        string codigo,
        string? acaoQuandoIndeferido,
        string? composicaoOrigem,
        string? destino,
        string? par,
        string? fallback)
    {
        IEnumerable<string> foraDoFormato = new[] { codigo, composicaoOrigem, destino, par, fallback }
            .Where(static c => c is not null)
            .Select(static c => c!)
            .Where(c => !FormatoCodigoModalidade.IsMatch(c));

        foreach (string cruzamento in foraDoFormato)
        {
            return new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"Envelope malformado em 'modalidades': o código '{cruzamento}' não tem o formato do cadastro " +
                "(A-Z, 0-9 e underscore) — e códigos são chave de composição e de remanejamento dentro da oferta.");
        }

        if (acaoQuandoIndeferido is not null && !AcoesQuandoIndeferido.Contains(acaoQuandoIndeferido, StringComparer.Ordinal))
        {
            return new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"Envelope malformado em 'modalidades': a ação ao indeferir '{acaoQuandoIndeferido}' não pertence ao " +
                $"domínio fechado do cadastro ({string.Join(", ", AcoesQuandoIndeferido)}) — restaurá-la daria ao " +
                "motor de homologação uma instrução que ele não sabe executar.");
        }

        return null;
    }

    /// <summary>
    /// A coerência entre a <b>natureza legal</b> e a <b>regra de remanejamento</b>, como ela
    /// valia na forma <c>1.1</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A regra é do <b>cadastro</b> de modalidades
    /// (<c>Modalidade.ValidarCoerenciaNaturezaRemanejamento</c>, módulo Configuração), e é
    /// por isso que o caminho de comando <b>nunca</b> a viola: o handler não lê estes campos
    /// do payload — <b>copia-os da view do cadastro</b>. Mas o snapshot-copy (ADR-0061)
    /// congela os três <b>por valor</b>, e quem reconstrói a modalidade a partir dos bytes
    /// não passa pelo cadastro. <c>ModalidadeSelecionada.Criar</c> só replica <b>uma</b> das
    /// três (a INV-12).
    /// </para>
    /// <para>
    /// Sem as outras duas, um envelope adulterado com
    /// <c>{naturezaLegal: "Ampla", regraRemanejamento: "DestinoUnico"}</c> restaura uma
    /// <b>ampla concorrência que remaneja as próprias vagas ociosas</b> — configuração que o
    /// caminho de escrita jamais produz, que faz round-trip <b>perfeito</b> (o encoder
    /// reemite os enums verbatim) e que é <b>input do motor de vagas</b> do certame.
    /// </para>
    /// <para>
    /// <b>Por que aqui, e não em <c>ModalidadeSelecionada.Criar</c>.</b> A entidade é a cópia
    /// congelada de uma modalidade — não a modalidade viva. Se o cadastro mudar a tabela de
    /// coerência amanhã, um snapshot <b>legítimo</b> de hoje passaria a ser irreidratável, e
    /// o certame publicado ficaria sem descarte: exatamente o que o congelamento existe para
    /// impedir. No codec, a tabela é a da forma <c>1.1</c> — e uma regra nova é <b>bump de
    /// versão</b>, com o seu próprio codec.
    /// </para>
    /// </remarks>
    private static DomainError? CoerenciaNaturezaRemanejamento(
        string codigo,
        NaturezaLegalModalidade natureza,
        RegraRemanejamentoModalidade remanejamento) => natureza switch
        {
            // A INV-12 já é da entidade; repeti-la aqui seria redundância — ela cai na
            // factory logo abaixo. As duas que faltam:
            NaturezaLegalModalidade.Ampla when remanejamento != RegraRemanejamentoModalidade.Nenhuma =>
                new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"Envelope malformado em 'modalidades': {codigo} é de ampla concorrência e não admite regra de " +
                    "remanejamento — as vagas ociosas dela não vão para lugar nenhum."),

            NaturezaLegalModalidade.Suplementar or NaturezaLegalModalidade.OutraModalidade
                when remanejamento is not (RegraRemanejamentoModalidade.DestinoUnico or RegraRemanejamentoModalidade.Cruzado) =>
                new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"Envelope malformado em 'modalidades': {codigo} é suplementar ou de outra natureza e exige " +
                    "regra de remanejamento DESTINO_UNICO ou CRUZADO."),

            _ => null,
        };

    private static OfertaAtendimentoEspecializado? LerAtendimento(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "atendimento", "$");
        leitor.ExigirChaves(bloco, "atendimento", "condicoes", "recursos", "tiposDeficiencia");

        JsonArray arrayCondicoes = leitor.Array(bloco, "condicoes", "atendimento");
        JsonArray arrayRecursos = leitor.Array(bloco, "recursos", "atendimento");
        JsonArray arrayTipos = leitor.Array(bloco, "tiposDeficiencia", "atendimento");
        if (leitor.Falhou)
        {
            return null;
        }

        List<OfertaCondicao> condicoes = [];
        for (int i = 0; i < arrayCondicoes.Count; i++)
        {
            string path = $"atendimento.condicoes[{i}]";
            JsonObject item = leitor.ItemObjeto(arrayCondicoes, i, "atendimento.condicoes");
            leitor.ExigirChaves(item, path, "condicaoOrigemId", "condicaoCodigo", "condicaoNome");

            Guid origemId = leitor.Identificador(item, "condicaoOrigemId", path);
            string codigo = leitor.TextoNaoVazio(item, "condicaoCodigo", path, LimitesDoEnvelope.CondicaoCodigo);
            if (!leitor.Falhou && !FormatoCodigoCondicao.IsMatch(codigo))
            {
                // O código da condição é CHAVE NATURAL: é por ele que a invariante ADR-0067
                // reconhece a condição PcD (OfertaAtendimentoEspecializado.CodigoCondicaoPcd).
                // Um "pcd" minúsculo, restaurado de um envelope adulterado, seria uma condição
                // que o cadastro nunca produz — e que o resto do código trata como chave.
                return leitor.Propagar<OfertaAtendimentoEspecializado>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"Envelope malformado em '{path}': o código de condição '{codigo}' não tem o formato do " +
                    "cadastro (maiúscula inicial, depois maiúsculas, dígitos e underscore)."));
            }

            string nome = leitor.TextoNaoVazio(item, "condicaoNome", path, LimitesDoEnvelope.NomeDeCadastro);
            if (leitor.Falhou)
            {
                return null;
            }

            condicoes.Add(OfertaCondicao.Criar(origemId, codigo, nome));
        }

        List<OfertaRecurso> recursos = [];
        for (int i = 0; i < arrayRecursos.Count; i++)
        {
            string path = $"atendimento.recursos[{i}]";
            JsonObject item = leitor.ItemObjeto(arrayRecursos, i, "atendimento.recursos");
            leitor.ExigirChaves(item, path, "recursoOrigemId", "recursoNome");

            Guid origemId = leitor.Identificador(item, "recursoOrigemId", path);
            string nome = leitor.TextoNaoVazio(item, "recursoNome", path, LimitesDoEnvelope.NomeDeCadastro);
            if (leitor.Falhou)
            {
                return null;
            }

            recursos.Add(OfertaRecurso.Criar(origemId, nome));
        }

        List<OfertaTipoDeficiencia> tipos = [];
        for (int i = 0; i < arrayTipos.Count; i++)
        {
            string path = $"atendimento.tiposDeficiencia[{i}]";
            JsonObject item = leitor.ItemObjeto(arrayTipos, i, "atendimento.tiposDeficiencia");
            leitor.ExigirChaves(item, path, "tipoDeficienciaOrigemId", "tipoDeficienciaNome");

            Guid origemId = leitor.Identificador(item, "tipoDeficienciaOrigemId", path);
            string nome = leitor.TextoNaoVazio(item, "tipoDeficienciaNome", path, LimitesDoEnvelope.NomeDeCadastro);
            if (leitor.Falhou)
            {
                return null;
            }

            tipos.Add(OfertaTipoDeficiencia.Criar(origemId, nome));
        }

        Result<OfertaAtendimentoEspecializado> oferta = OfertaAtendimentoEspecializado.Criar(condicoes, recursos, tipos);
        return oferta.IsFailure ? leitor.Propagar<OfertaAtendimentoEspecializado>(oferta.Error!) : oferta.Value;
    }

    /// <summary>
    /// O bônus tem <b>duas formas fechadas</b>: <c>{"presente": false}</c> e a completa.
    /// Fechar as duas é o que impede o pior modo de falha desta leitura — um
    /// <c>{"presente":false,"fator":"1.2000",…}</c> lido como “sem bônus”
    /// <b>descartaria o bônus regional do certame</b> (RN05) sem deixar rastro.
    /// </summary>
    private static ConfiguracaoBonusRegional? LerBonusRegional(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "bonusRegional", "$");
        if (leitor.Falhou)
        {
            return null;
        }

        bool presente = leitor.Booleano(bloco, "presente", "bonusRegional");
        if (leitor.Falhou)
        {
            return null;
        }

        if (!presente)
        {
            leitor.ExigirChaves(bloco, "bonusRegional", "presente");
            return null;
        }

        leitor.ExigirChaves(bloco, "bonusRegional", "presente", "regra", "fator", "teto", "municipioConvenio", "baseLegal");

        ReferenciaRegra regra = leitor.Regra(bloco, "regra", "bonusRegional", RegraBonusCodigo.Multiplicativo);
        decimal fator = leitor.Decimal(bloco, "fator", EscalaPadrao, "bonusRegional", LimitesDoEnvelope.PrecisaoBonus);
        decimal? teto = leitor.DecimalOpcional(bloco, "teto", EscalaPadrao, "bonusRegional", LimitesDoEnvelope.PrecisaoBonus);
        string? municipio = leitor.TextoOpcional(bloco, "municipioConvenio", "bonusRegional", LimitesDoEnvelope.MunicipioConvenio);
        string? baseLegal = leitor.TextoOpcional(bloco, "baseLegal", "bonusRegional", LimitesDoEnvelope.BaseLegal);

        if (leitor.Falhou)
        {
            return null;
        }

        Result<ConfiguracaoBonusRegional> bonus = ConfiguracaoBonusRegional.Criar(regra, fator, teto, municipio, baseLegal);
        return bonus.IsFailure ? leitor.Propagar<ConfiguracaoBonusRegional>(bonus.Error!) : bonus.Value;
    }

    private static IReadOnlyList<CriterioDesempate> LerCriteriosDesempate(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray array = leitor.Array(payload, "criteriosDesempate", "$");
        if (leitor.Falhou)
        {
            return [];
        }

        List<CriterioDesempate> criterios = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"criteriosDesempate[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "criteriosDesempate");
            leitor.ExigirChaves(item, path, "ordem", "regra", "args");

            int ordem = leitor.Inteiro(item, "ordem", path);
            ReferenciaRegra regra = leitor.Regra(
                item,
                "regra",
                path,
                CriterioDesempateCodigo.MaiorNotaEtapa,
                CriterioDesempateCodigo.MaiorIdade,
                CriterioDesempateCodigo.Idoso,
                CriterioDesempateCodigo.PredicadoFato);
            JsonObject args = leitor.Objeto(item, "args", path);
            if (leitor.Falhou)
            {
                return [];
            }

            ArgsCriterioDesempate? argumentos = LerArgsDesempate(leitor, regra.Codigo, args, $"{path}.args");
            if (leitor.Falhou)
            {
                return [];
            }

            Result<CriterioDesempate> criterio = CriterioDesempate.Criar(ordem, regra, argumentos!);
            if (criterio.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<CriterioDesempate>>(criterio.Error!) ?? [];
            }

            criterios.Add(criterio.Value!);
        }

        return criterios;
    }

    /// <summary>
    /// A variante vem do <b>código da regra</b> — o envelope não carrega discriminador, e
    /// duas variantes distintas (<c>MAIOR-IDADE</c>, <c>ZERO-EM-AREA</c>) serializam ambas
    /// como <c>{}</c>. Exigir o objeto vazio <b>exatamente</b> é o que impede que args de
    /// uma variante entrem em outra sem que ninguém veja.
    /// </summary>
    private static ArgsCriterioDesempate? LerArgsDesempate(
        LeitorEnvelope leitor,
        string codigo,
        JsonObject args,
        string path)
    {
        switch (codigo)
        {
            case CriterioDesempateCodigo.MaiorNotaEtapa:
                leitor.ExigirChaves(args, path, "etapaRef");
                Guid etapaRef = leitor.Identificador(args, "etapaRef", path);
                return leitor.Falhou ? null : new ArgsDesempateMaiorNotaEtapa(etapaRef);

            case CriterioDesempateCodigo.MaiorIdade:
                leitor.ExigirChaves(args, path);
                return leitor.Falhou ? null : new ArgsDesempateMaiorIdade();

            case CriterioDesempateCodigo.Idoso:
                leitor.ExigirChaves(args, path, "idadeMinima");
                int idadeMinima = leitor.Inteiro(args, "idadeMinima", path);
                return leitor.Falhou ? null : new ArgsDesempateIdoso(idadeMinima);

            case CriterioDesempateCodigo.PredicadoFato:
                leitor.ExigirChaves(args, path, "fato", "operador", "valor");
                // A validação de FORMA (fato não-vazio, operador reconhecido, valor coerente
                // com o operador) é feita por CondicaoDnf.Criar — a mesma factory que o
                // caminho de comando usa (DefinirCriteriosDesempateCommandHandler). O decoder
                // não revalida SEMÂNTICA (fato pertence ao vocabulário vivo): RN08 proíbe
                // reinterpretar um predicado já congelado contra um catálogo que pode ter
                // mudado — só a forma é exigida aqui.
                string fato = leitor.TextoNaoVazio(args, "fato", path);
                string operadorCodigo = leitor.TextoNaoVazio(args, "operador", path);
                System.Text.Json.JsonElement valor = leitor.Valor(args, "valor", path);
                if (leitor.Falhou)
                {
                    return null;
                }

                Operador operador = OperadorCodigo.FromCodigo(operadorCodigo);
                Result<CondicaoDnf> condicaoResult = CondicaoDnf.Criar(fato, operador, valor);
                return condicaoResult.IsFailure
                    ? leitor.Propagar<ArgsCriterioDesempate>(condicaoResult.Error!)
                    : new ArgsDesempatePredicadoFato(condicaoResult.Value!);

            default:
                return leitor.Propagar<ArgsCriterioDesempate>(new DomainError(
                    ErrosCodecEnvelope.RegraDesconhecida,
                    $"Não há variante de args conhecida para o critério de desempate '{codigo}' em '{path}'."));
        }
    }

    private static ConfiguracaoClassificacao? LerClassificacao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "classificacao", "$");
        leitor.ExigirChaves(
            bloco,
            "classificacao",
            "regraCalculo",
            "regraArredondamento",
            "casasArredondamento",
            "regraOrdemAlocacao",
            "nOpcoesAlocacao",
            "regrasEliminacao");

        ReferenciaRegra regraCalculo = leitor.Regra(
            bloco,
            "regraCalculo",
            "classificacao",
            RegraCalculoCodigo.FormulaMediaPonderada,
            RegraCalculoCodigo.ClassificacaoImportada);
        ReferenciaRegra? regraArredondamento = leitor.RegraOpcional(
            bloco,
            "regraArredondamento",
            "classificacao",
            RegraArredondamentoCodigo.PrecisaoTruncar,
            RegraArredondamentoCodigo.PrecisaoArredondarCima);
        int? casas = leitor.InteiroOpcional(bloco, "casasArredondamento", "classificacao");
        ReferenciaRegra regraOrdem = leitor.Regra(
            bloco,
            "regraOrdemAlocacao",
            "classificacao",
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04);
        int nOpcoes = leitor.Inteiro(bloco, "nOpcoesAlocacao", "classificacao");

        JsonArray array = leitor.Array(bloco, "regrasEliminacao", "classificacao");
        if (leitor.Falhou)
        {
            return null;
        }

        List<RegraEliminacao> eliminacoes = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"classificacao.regrasEliminacao[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "classificacao.regrasEliminacao");
            leitor.ExigirChaves(item, path, "regra", "args");

            ReferenciaRegra regra = leitor.Regra(
                item,
                "regra",
                path,
                RegraEliminacaoCodigo.ElimNotaMinimaEtapa,
                RegraEliminacaoCodigo.ElimCorteRedacao,
                RegraEliminacaoCodigo.ElimZeroEmArea);
            JsonObject args = leitor.Objeto(item, "args", path);
            if (leitor.Falhou)
            {
                return null;
            }

            ArgsRegraEliminacao? argumentos = LerArgsEliminacao(leitor, regra.Codigo, args, $"{path}.args");
            if (leitor.Falhou)
            {
                return null;
            }

            Result<RegraEliminacao> eliminacao = RegraEliminacao.Criar(regra, argumentos!);
            if (eliminacao.IsFailure)
            {
                return leitor.Propagar<ConfiguracaoClassificacao>(eliminacao.Error!);
            }

            eliminacoes.Add(eliminacao.Value!);
        }

        Result<ConfiguracaoClassificacao> classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo, regraArredondamento, casas, regraOrdem, nOpcoes, eliminacoes);

        return classificacao.IsFailure
            ? leitor.Propagar<ConfiguracaoClassificacao>(classificacao.Error!)
            : classificacao.Value;
    }

    private static ArgsRegraEliminacao? LerArgsEliminacao(
        LeitorEnvelope leitor,
        string codigo,
        JsonObject args,
        string path)
    {
        switch (codigo)
        {
            case RegraEliminacaoCodigo.ElimNotaMinimaEtapa:
                leitor.ExigirChaves(args, path, "etapaRef", "notaMinima");
                Guid etapaRef = leitor.Identificador(args, "etapaRef", path);
                decimal notaMinima = leitor.Decimal(args, "notaMinima", EscalaPadrao, path);
                return leitor.Falhou ? null : new ArgsElimNotaMinimaEtapa(etapaRef, notaMinima);

            case RegraEliminacaoCodigo.ElimCorteRedacao:
                leitor.ExigirChaves(args, path, "minimo");
                decimal minimo = leitor.Decimal(args, "minimo", EscalaPadrao, path);
                return leitor.Falhou ? null : new ArgsElimCorteRedacao(minimo);

            case RegraEliminacaoCodigo.ElimZeroEmArea:
                leitor.ExigirChaves(args, path);
                return leitor.Falhou ? null : new ArgsElimZeroEmArea();

            default:
                return leitor.Propagar<ArgsRegraEliminacao>(new DomainError(
                    ErrosCodecEnvelope.RegraDesconhecida,
                    $"Não há variante de args conhecida para a regra de eliminação '{codigo}' em '{path}'."));
        }
    }

    private static RetificacaoInfo? LerRetificacao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "retificacao", "$");
        leitor.ExigirChaves(bloco, "retificacao", "editalRetificadoId", "motivo");

        Guid atoRetificado = leitor.Identificador(bloco, "editalRetificadoId", "retificacao");
        string motivo = leitor.TextoNaoVazio(bloco, "motivo", "retificacao");

        return leitor.Falhou ? null : new RetificacaoInfo(atoRetificado, motivo);
    }

    /// <summary>
    /// O cronograma de fases (Story #851): <c>origemCandidatos</c> é lido e validado para
    /// fechar a gramática, mas <b>não é propagado</b> — é atributo de raiz do agregado
    /// (<c>ProcessoSeletivo.OrigemCandidatos</c>), imutável após a criação (nenhum
    /// <c>Definir*</c> o altera), e por isso nada aqui precisa repô-lo.
    /// </summary>
    private static IReadOnlyList<FaseCronograma> LerCronogramaFases(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "cronogramaFases", "$");
        leitor.ExigirChaves(bloco, "cronogramaFases", "origemCandidatos", "fases");

        leitor.Enumeracao<OrigemCandidatos>(bloco, "origemCandidatos", "cronogramaFases");

        JsonArray array = leitor.Array(bloco, "fases", "cronogramaFases");
        if (leitor.Falhou)
        {
            return [];
        }

        List<FaseCronograma> fases = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"cronogramaFases.fases[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "cronogramaFases.fases");
            leitor.ExigirChaves(
                item, path,
                "ordem", "faseCanonicaOrigemId", "codigo", "donoInstitucional", "origemData",
                "agrupaEtapas", "permiteComplementacao", "produzResultado", "resultadoDefinitivo",
                "coletaInscricao", "inicio", "fim", "atoProduzidoCodigo", "atoProduzidoEfeitoIrreversivel",
                "bancasRequeridas", "regraRecurso");

            int ordem = leitor.Inteiro(item, "ordem", path);
            Guid faseCanonicaOrigemId = leitor.Identificador(item, "faseCanonicaOrigemId", path);
            string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.FaseCodigo);
            string donoInstitucional = leitor.TextoNaoVazio(item, "donoInstitucional", path, LimitesDoEnvelope.DonoInstitucional);
            OrigemDataFase origemData = leitor.Enumeracao<OrigemDataFase>(item, "origemData", path);
            bool agrupaEtapas = leitor.Booleano(item, "agrupaEtapas", path);
            bool permiteComplementacao = leitor.Booleano(item, "permiteComplementacao", path);
            bool produzResultado = leitor.Booleano(item, "produzResultado", path);
            bool resultadoDefinitivo = leitor.Booleano(item, "resultadoDefinitivo", path);
            bool coletaInscricao = leitor.Booleano(item, "coletaInscricao", path);
            DateTimeOffset? inicio = leitor.InstanteOpcional(item, "inicio", path);
            DateTimeOffset? fim = leitor.InstanteOpcional(item, "fim", path);
            string? atoProduzidoCodigo = leitor.TextoOpcional(item, "atoProduzidoCodigo", path, LimitesDoEnvelope.TipoAtoCodigo);
            bool atoProduzidoEfeitoIrreversivel = leitor.Booleano(item, "atoProduzidoEfeitoIrreversivel", path);

            if (leitor.Falhou)
            {
                return [];
            }

            IReadOnlyList<BancaRequerida> bancas = LerBancasRequeridas(leitor, item, path);
            if (leitor.Falhou)
            {
                return [];
            }

            RegraRecursoFase? regraRecurso = LerRegraRecursoFase(leitor, item, path);
            if (leitor.Falhou)
            {
                return [];
            }

            Result<FaseCronograma> fase = FaseCronograma.Criar(
                ordem, faseCanonicaOrigemId, codigo, donoInstitucional, origemData,
                agrupaEtapas, permiteComplementacao, produzResultado, resultadoDefinitivo, coletaInscricao,
                inicio, fim, atoProduzidoCodigo, atoProduzidoEfeitoIrreversivel, bancas, regraRecurso);
            if (fase.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<FaseCronograma>>(fase.Error!) ?? [];
            }

            fases.Add(fase.Value!);
        }

        return fases;
    }

    private static List<BancaRequerida> LerBancasRequeridas(LeitorEnvelope leitor, JsonObject faseItem, string pathPai)
    {
        JsonArray array = leitor.Array(faseItem, "bancasRequeridas", pathPai);
        if (leitor.Falhou)
        {
            return [];
        }

        List<BancaRequerida> bancas = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"{pathPai}.bancasRequeridas[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, $"{pathPai}.bancasRequeridas");
            leitor.ExigirChaves(item, path, "tipoBancaOrigemId", "codigo");

            Guid tipoBancaOrigemId = leitor.Identificador(item, "tipoBancaOrigemId", path);
            string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.TipoBancaCodigo);

            if (leitor.Falhou)
            {
                return [];
            }

            bancas.Add(BancaRequerida.Criar(tipoBancaOrigemId, codigo));
        }

        return bancas;
    }

    /// <summary>
    /// A regra de recurso da fase (0..1 — a PRESENÇA do bloco é o que faz a fase admitir
    /// recurso, §3.6). O vocabulário de <c>regra.codigo</c> é fechado a
    /// <see cref="RegraPrazoRecursoCodigo.AncoradoEmAto"/> — a única variante que
    /// <see cref="RegraRecursoFase"/> admite (CA-02).
    /// </summary>
    private static RegraRecursoFase? LerRegraRecursoFase(LeitorEnvelope leitor, JsonObject faseItem, string pathPai)
    {
        JsonObject? bloco = leitor.ObjetoOpcional(faseItem, "regraRecurso", pathPai);
        if (leitor.Falhou || bloco is null)
        {
            return null;
        }

        string path = $"{pathPai}.regraRecurso";
        leitor.ExigirChaves(bloco, path, "regra", "args");

        ReferenciaRegra regra = leitor.Regra(bloco, "regra", path, RegraPrazoRecursoCodigo.AncoradoEmAto);
        JsonObject argsObjeto = leitor.Objeto(bloco, "args", path);
        if (leitor.Falhou)
        {
            return null;
        }

        string argsPath = $"{path}.args";
        leitor.ExigirChaves(
            argsObjeto, argsPath,
            "prazoValor", "prazoUnidade", "atoAncoraCodigo",
            "suspensividadePrimeiraInstanciaValor", "suspensividadePrimeiraInstanciaUnidade",
            "suspensividadeSegundaInstanciaValor", "suspensividadeSegundaInstanciaUnidade");

        decimal prazoValor = leitor.Decimal(argsObjeto, "prazoValor", EscalaPadrao, argsPath, LimitesDoEnvelope.PrecisaoPrazo);
        UnidadePrazo prazoUnidade = leitor.Enumeracao<UnidadePrazo>(argsObjeto, "prazoUnidade", argsPath);
        string atoAncoraCodigo = leitor.TextoNaoVazio(argsObjeto, "atoAncoraCodigo", argsPath, LimitesDoEnvelope.TipoAtoCodigo);
        decimal? suspensividade1Valor = leitor.DecimalOpcional(
            argsObjeto, "suspensividadePrimeiraInstanciaValor", EscalaPadrao, argsPath, LimitesDoEnvelope.PrecisaoPrazo);
        UnidadePrazo? suspensividade1Unidade = leitor.EnumeracaoOpcional<UnidadePrazo>(
            argsObjeto, "suspensividadePrimeiraInstanciaUnidade", argsPath);
        decimal? suspensividade2Valor = leitor.DecimalOpcional(
            argsObjeto, "suspensividadeSegundaInstanciaValor", EscalaPadrao, argsPath, LimitesDoEnvelope.PrecisaoPrazo);
        UnidadePrazo? suspensividade2Unidade = leitor.EnumeracaoOpcional<UnidadePrazo>(
            argsObjeto, "suspensividadeSegundaInstanciaUnidade", argsPath);

        if (leitor.Falhou)
        {
            return null;
        }

        ArgsRegraPrazoRecurso args = new(
            prazoValor, prazoUnidade, atoAncoraCodigo,
            suspensividade1Valor, suspensividade1Unidade,
            suspensividade2Valor, suspensividade2Unidade);

        Result<RegraRecursoFase> regraRecurso = RegraRecursoFase.Criar(regra, args);
        return regraRecurso.IsFailure ? leitor.Propagar<RegraRecursoFase>(regraRecurso.Error!) : regraRecurso.Value;
    }
}

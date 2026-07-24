namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O codec do envelope de congelamento — <b>um só</b>. Enquanto não há produção nem certame
/// congelado, o sistema emite e lê uma forma canônica corrente (<c>0.0.1</c>) e a evolui
/// livremente: mudar a forma reescreve a fixture, não gera um encoder congelado ao lado. O
/// versionamento forense — um codec por <c>schema_version</c>, encoders aposentados só quando
/// deixam de ser correntes — volta a valer quando a primeira release de produção fixar a
/// versão <c>1.0.0</c>.
/// </summary>
/// <remarks>
/// <c>Codificar</c> delega ao <see cref="SnapshotPublicacaoCanonicalizer"/>, a projeção viva —
/// e, sendo o único codec, os dois nunca podem divergir. <c>Decodificar</c> reconstrói o
/// envelope pelos leitores de bloco (os métodos <c>Ler*</c> estáticos, hoje agrupados nos tipos
/// de leitura de bloco) e resolve <c>arvoreSatisfacao</c> contra as exigências já lidas, por Id,
/// sem duplicá-las.
/// </remarks>
public sealed class EnvelopeCodec : IEnvelopeCodec
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
        "fatosColetados",
        "regrasDerivacao",
        "grafoDependencia",
        "versaoInterpretador",
        "modalidadesOfertadas",
    ];

    private readonly SnapshotPublicacaoCanonicalizer _encoder = new();

    public string SchemaVersion => "0.0.2";

    public IPerfilCanonico Perfil => PerfilCanonicoV1.Instancia;

    public string AlgoritmoHash => Perfil.Algoritmo;

    public bool TemEncoder => true;

    public bool TemDecoder => true;

    public string? MotivoDaRecusa => null;

    /// <summary>
    /// Delega à projeção viva. A guarda confere que o codec e o canonicalizador declaram a
    /// mesma versão e o mesmo algoritmo — sendo um sistema só, eles têm de concordar; uma
    /// divergência aqui é erro de programação (o codec e a emissão saíram de sincronia), não
    /// um estado alcançável em runtime.
    /// </summary>
    public SnapshotCanonico Codificar(EntradaCanonicalizacao entrada)
    {
        SnapshotCanonico snapshot = _encoder.Canonicalizar(entrada);

        if (snapshot.SchemaVersion != SchemaVersion || snapshot.AlgoritmoHash != AlgoritmoHash)
        {
            throw new InvalidOperationException(
                $"O codec ({SchemaVersion}/{AlgoritmoHash}) e o canonicalizador ({snapshot.SchemaVersion}/{snapshot.AlgoritmoHash}) " +
                "declaram versões distintas — o codec e a emissão saíram de sincronia.");
        }

        return snapshot;
    }

    public Result<EnvelopeReidratado> Decodificar(VersaoConfiguracao versao)
    {
        ArgumentNullException.ThrowIfNull(versao);

        Result<JsonObject> parse = EnvelopeCodecV11.Parsear(Perfil, versao.ConfiguracaoCongeladaCanonica);
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

        IReadOnlyList<FatoColetado> fatosColetados = LerFatosColetados(leitor, payload);
        IReadOnlyList<ConfiguracaoDerivacaoFato> regrasDerivacao = LerRegrasDerivacao(leitor, payload);
        string versaoInterpretador = leitor.TextoNaoVazio(payload, "versaoInterpretador", "$");
        IReadOnlyList<string> modalidadesOfertadas = leitor.Textos(payload, "modalidadesOfertadas", "$");
        if (leitor.Falhou)
        {
            return leitor.Falha<EnvelopeReidratado>();
        }

        if (EnvelopeCodecV11.VerificarCoerenciaComAVersao(versao, hashDocumento, retificacao) is { } incoerencia)
        {
            return Result<EnvelopeReidratado>.Failure(incoerencia);
        }

        IReadOnlyList<NoExigencia> todosOsNos = [.. raizes.SelectMany(static raiz => raiz.AchatarComDescendentes())];

        // Fail-closed do bloco de coleta/derivação (RN08): um envelope adulterado que declare fato
        // duplicado, cite fato inexistente num gatilho/pré-condição/regra, contribua código fora do
        // domínio de modalidades, feche ciclo no grafo conjunto, ou cujo grafo/modalidades congelados
        // divirjam do recomputado, é recusado como malformado — nunca reidratado como se fosse íntegro.
        if (ValidarBlocoDeFatosEDerivacao(
            fatosColetados, regrasDerivacao, documentosExigidos,
            versaoInterpretador, modalidadesOfertadas, distribuicao, payload) is { } malformado)
        {
            return Result<EnvelopeReidratado>.Failure(malformado);
        }

        GrafoConfiguracao grafo = new(
            etapas, atendimento!, distribuicao, bonus, desempate, classificacao!, cronogramaFases,
            documentosExigidos, todosOsNos, referenciaTemporalFatos, fatosColetados, regrasDerivacao);
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

    /// <summary>
    /// Os fatos coletados (Story #928, §7.4) — reconstruídos por <see cref="FatoColetado.Criar"/>
    /// (que revalida a forma: código, ordem, auto-referência), Ids novos (não são congelados no
    /// envelope, diferente de <c>etapa.Id</c>). A pré-condição é a mesma forma DNF do gatilho.
    /// </summary>
    private static IReadOnlyList<FatoColetado> LerFatosColetados(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray array = leitor.Array(payload, "fatosColetados", "$");
        if (leitor.Falhou)
        {
            return [];
        }

        List<FatoColetado> fatos = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"fatosColetados[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "fatosColetados");
            leitor.ExigirChaves(item, path, "fatoCodigo", "ordem", "precondicao");

            string fatoCodigo = leitor.TextoNaoVazio(item, "fatoCodigo", path, LimitesDoEnvelope.Fato);
            int ordem = leitor.Inteiro(item, "ordem", path);
            if (leitor.Falhou)
            {
                return [];
            }

            IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> condicoes =
                LerDnf(leitor, item, "precondicao", path);
            if (leitor.Falhou)
            {
                return [];
            }

            List<CondicaoPrecondicaoFato> precondicoes = [];
            foreach ((int clausula, string fato, Operador operador, JsonElement valor) in condicoes)
            {
                Result<CondicaoPrecondicaoFato> condicao = CondicaoPrecondicaoFato.Criar(clausula, fato, operador, valor);
                if (condicao.IsFailure)
                {
                    return leitor.Propagar<IReadOnlyList<FatoColetado>>(condicao.Error!) ?? [];
                }

                precondicoes.Add(condicao.Value!);
            }

            Result<FatoColetado> fatoColetado = FatoColetado.Criar(fatoCodigo, ordem, precondicoes);
            if (fatoColetado.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<FatoColetado>>(fatoColetado.Error!) ?? [];
            }

            fatos.Add(fatoColetado.Value!);
        }

        return fatos;
    }

    /// <summary>
    /// As regras de derivação (Story #928, §7.4) — reconstruídas nos três níveis por
    /// <see cref="ConfiguracaoDerivacaoFato.Criar"/>, <see cref="RegraDerivacaoConfigurada.Criar"/> e
    /// <see cref="CondicaoRegraDerivacao.Criar"/> (forma revalidada; Ids novos). A regra âncora tem
    /// <c>quando</c> nulo.
    /// </summary>
    private static IReadOnlyList<ConfiguracaoDerivacaoFato> LerRegrasDerivacao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray array = leitor.Array(payload, "regrasDerivacao", "$");
        if (leitor.Falhou)
        {
            return [];
        }

        List<ConfiguracaoDerivacaoFato> configuracoes = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"regrasDerivacao[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "regrasDerivacao");
            leitor.ExigirChaves(item, path, "codigoFato", "regras");

            string codigoFato = leitor.TextoNaoVazio(item, "codigoFato", path, LimitesDoEnvelope.Fato);
            if (leitor.Falhou)
            {
                return [];
            }

            IReadOnlyList<RegraDerivacaoConfigurada> regras = LerRegrasDaDerivacao(leitor, item, path);
            if (leitor.Falhou)
            {
                return [];
            }

            Result<ConfiguracaoDerivacaoFato> config = ConfiguracaoDerivacaoFato.Criar(codigoFato, regras);
            if (config.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<ConfiguracaoDerivacaoFato>>(config.Error!) ?? [];
            }

            configuracoes.Add(config.Value!);
        }

        return configuracoes;
    }

    private static IReadOnlyList<RegraDerivacaoConfigurada> LerRegrasDaDerivacao(
        LeitorEnvelope leitor, JsonObject configItem, string pathPai)
    {
        JsonArray array = leitor.Array(configItem, "regras", pathPai);
        if (leitor.Falhou)
        {
            return [];
        }

        List<RegraDerivacaoConfigurada> regras = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"{pathPai}.regras[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, $"{pathPai}.regras");
            leitor.ExigirChaves(item, path, "ordem", "contribui", "quando");

            int ordem = leitor.Inteiro(item, "ordem", path);
            string contribui = leitor.TextoNaoVazio(item, "contribui", path, LimitesDoEnvelope.Fato);
            if (leitor.Falhou)
            {
                return [];
            }

            IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> condicoesTuplas =
                LerDnf(leitor, item, "quando", path);
            if (leitor.Falhou)
            {
                return [];
            }

            List<CondicaoRegraDerivacao> condicoes = [];
            foreach ((int clausula, string fato, Operador operador, JsonElement valor) in condicoesTuplas)
            {
                Result<CondicaoRegraDerivacao> condicao = CondicaoRegraDerivacao.Criar(clausula, fato, operador, valor);
                if (condicao.IsFailure)
                {
                    return leitor.Propagar<IReadOnlyList<RegraDerivacaoConfigurada>>(condicao.Error!) ?? [];
                }

                condicoes.Add(condicao.Value!);
            }

            Result<RegraDerivacaoConfigurada> regra = RegraDerivacaoConfigurada.Criar(ordem, contribui, condicoes);
            if (regra.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<RegraDerivacaoConfigurada>>(regra.Error!) ?? [];
            }

            regras.Add(regra.Value!);
        }

        return regras;
    }

    /// <summary>
    /// Um predicado DNF <c>{fato, operador, valor}</c> (pré-condição de fato ou <c>quando</c> de
    /// regra), na mesma forma do gatilho: array de cláusulas (OU), cada uma array de condições (E).
    /// Ausente/nulo = sem condição. Mesma disciplina de <c>EnvelopeCodecV12.LerCondicaoGatilho</c> —
    /// a factory revalida a forma; token de operador não reconhecido vira <c>Nenhuma</c>, que a
    /// factory rejeita como falha de domínio.
    /// </summary>
    private static IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> LerDnf(
        LeitorEnvelope leitor, JsonObject item, string chave, string pathPai)
    {
        if (item[chave] is not JsonNode raiz)
        {
            return [];
        }

        if (raiz is not JsonArray clausulas)
        {
            return leitor.Propagar<IReadOnlyList<(int, string, Operador, JsonElement)>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{pathPai}.{chave}' deveria ser um array de cláusulas ou null.")) ?? [];
        }

        List<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> condicoes = [];
        for (int c = 0; c < clausulas.Count; c++)
        {
            string clausulaPath = $"{pathPai}.{chave}[{c}]";
            if (clausulas[c] is not JsonArray condicoesDaClausula)
            {
                return leitor.Propagar<IReadOnlyList<(int, string, Operador, JsonElement)>>(new DomainError(
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

                condicoes.Add((c, fato, OperadorCodigo.FromCodigo(operadorCodigo), valor));
            }
        }

        return condicoes;
    }

    /// <summary>
    /// Recusa fail-closed do bloco de coleta/derivação (RN08): um envelope válido nunca declara fato
    /// duplicado, cita fato inexistente, contribui código fora do domínio de modalidades, fecha ciclo,
    /// nem congela um grafo/modalidades que não reproduzem o recomputado. Um envelope adulterado que o
    /// faça é recusado como malformado — o grafo conjunto ignora deliberadamente referências ausentes
    /// (projeta só o que existe), então o testemunho por si só não prova a completude do vocabulário
    /// citado; estas checagens fecham o que o grafo não prova. Devolve <see langword="null"/> quando o
    /// bloco é íntegro.
    /// </summary>
    private static DomainError? ValidarBlocoDeFatosEDerivacao(
        IReadOnlyList<FatoColetado> fatos,
        IReadOnlyList<ConfiguracaoDerivacaoFato> regrasDerivacao,
        IReadOnlyList<DocumentoExigido> documentosExigidos,
        string versaoInterpretador,
        IReadOnlyList<string> modalidadesOfertadas,
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicao,
        JsonObject payload)
    {
        if (!string.Equals(versaoInterpretador, MotorDerivacao.VersaoSemantica, StringComparison.Ordinal))
        {
            return Malformado(
                $"'versaoInterpretador' desconhecida: '{versaoInterpretador}' — este sistema resolve a semântica "
                + $"'{MotorDerivacao.VersaoSemantica}'.");
        }

        HashSet<string> coletados = new(StringComparer.Ordinal);
        HashSet<int> ordens = [];
        Dictionary<string, int> ordemPorCodigo = new(StringComparer.Ordinal);
        foreach (FatoColetado fato in fatos)
        {
            if (!coletados.Add(fato.FatoCodigo))
            {
                return Malformado($"'fatosColetados': o fato '{fato.FatoCodigo}' aparece mais de uma vez.");
            }

            if (!ordens.Add(fato.Ordem))
            {
                return Malformado($"'fatosColetados': a ordem {fato.Ordem} é usada por mais de um fato.");
            }

            ordemPorCodigo[fato.FatoCodigo] = fato.Ordem;
        }

        HashSet<string> derivados = new(StringComparer.Ordinal);
        HashSet<string> universo = new(coletados, StringComparer.Ordinal);
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao)
        {
            if (!derivados.Add(config.CodigoFato))
            {
                return Malformado($"'regrasDerivacao': o fato '{config.CodigoFato}' tem mais de uma configuração de derivação.");
            }

            universo.Add(config.CodigoFato);
        }

        // Pré-condição de campo só cita fato COLETADO e ANTERIOR (a garantia de anterioridade que o
        // resolvedor de runtime pressupõe — ele percorre os coletados por ordem, sem acionar o motor).
        foreach (FatoColetado fato in fatos)
        {
            foreach (CondicaoPrecondicaoFato precondicao in fato.Precondicoes)
            {
                if (!ordemPorCodigo.TryGetValue(precondicao.Fato, out int ordemCitada))
                {
                    return Malformado(
                        $"'fatosColetados': a pré-condição do fato '{fato.FatoCodigo}' cita '{precondicao.Fato}', que o processo não coleta.");
                }

                if (ordemCitada >= fato.Ordem)
                {
                    return Malformado(
                        $"'fatosColetados': a pré-condição do fato '{fato.FatoCodigo}' cita '{precondicao.Fato}', que não é anterior na ordem de coleta.");
                }
            }
        }

        // Toda citação de regra de derivação e de gatilho de exigência existe em coletados ∪ derivados.
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao)
        {
            foreach (RegraDerivacaoConfigurada regra in config.Regras)
            {
                foreach (CondicaoRegraDerivacao condicao in regra.Condicoes)
                {
                    if (!universo.Contains(condicao.Fato))
                    {
                        return Malformado(
                            $"'regrasDerivacao': a derivação de '{config.CodigoFato}' cita '{condicao.Fato}', que o processo não coleta nem deriva.");
                    }
                }
            }
        }

        // A completude das citações de GATILHO (o fato citado existe em coletados ∪ derivados) é
        // recusa da fatia de dependência declarada (§7.3), não desta: o publish ainda não a barra,
        // e recusá-la só aqui divergiria decode de encode. O grafo conjunto já ignora de propósito a
        // citação a fato ausente, então o testemunho reproduz o mesmo grafo (sem a aresta) dos dois
        // lados — consistente até a fatia que fecha a recusa no publish.

        // O código contribuído pela derivação de MODALIDADE tem de pertencer ao domínio congelado das
        // modalidades ofertadas — o testemunho de conjunto prova o domínio, não que cada `contribui`
        // caiba nele. O VO da regra é o contrato: reconstruí-lo contra o domínio recusa `contribui`
        // fora dele (e revalida dependências/auto-referência de brinde).
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao)
        {
            if (string.Equals(config.CodigoFato, RegrasDerivacaoModalidadeLei12711.CodigoFato, StringComparison.Ordinal))
            {
                Result<RegrasDerivacaoFato> vo = config.ParaRegrasDerivacao(modalidadesOfertadas);
                if (vo.IsFailure)
                {
                    return vo.Error;
                }
            }
        }

        // Aciclicidade do grafo conjunto + testemunho: o grafo/modalidades congelados têm de reproduzir
        // exatamente o recomputado das partes reidratadas (byte a byte, pela mesma projeção canônica).
        Result<GrafoDependenciaConjunta> grafo =
            GrafoDependenciaConjunta.Construir(fatos, regrasDerivacao, documentosExigidos);
        if (grafo.IsFailure)
        {
            return grafo.Error;
        }

        if (DivergeDoCongelado(payload, "grafoDependencia",
            SnapshotPublicacaoCanonicalizer.SerializarGrafoDependencia(grafo.Value!)))
        {
            return Malformado("'grafoDependencia' congelado não reproduz o grafo recomputado das partes reidratadas.");
        }

        if (DivergeDoCongelado(payload, "modalidadesOfertadas",
            SnapshotPublicacaoCanonicalizer.SerializarModalidadesOfertadas(distribuicao)))
        {
            return Malformado("'modalidadesOfertadas' congelado não reproduz o conjunto recomputado da distribuição.");
        }

        return null;

        static DomainError Malformado(string mensagem) => new(ErrosCodecEnvelope.EnvelopeMalformado, mensagem);
    }

    /// <summary>
    /// Compara o bloco congelado com o esperado recomputado — ambos pela MESMA projeção canônica
    /// (<see cref="PerfilCanonicoV1"/>), imune à ordem de chaves. É o testemunho: o congelado não é
    /// segunda fonte de verdade, e sim uma cópia verificável do que as partes reidratadas reproduzem.
    /// </summary>
    private static bool DivergeDoCongelado(JsonObject payload, string chave, JsonNode esperado)
    {
        // O perfil serializa um JsonObject; envolve-se cada lado num wrapper para comparar array ou
        // objeto pela mesma projeção. O congelado é clonado antes de reparentar — mutá-lo tiraria o
        // bloco do payload que ainda está sendo lido.
        byte[] bytesEsperados = PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["v"] = esperado });
        byte[] bytesCongelados = PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["v"] = payload[chave]!.DeepClone() });
        return !bytesEsperados.AsSpan().SequenceEqual(bytesCongelados);
    }
}

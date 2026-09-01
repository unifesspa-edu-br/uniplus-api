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
/// congelado, o sistema emite e lê uma forma canônica corrente (ver <see cref="SchemaVersion"/>)
/// e a evolui livremente: mudar a forma reescreve a fixture, não gera um encoder congelado ao
/// lado. O
/// versionamento forense — um codec por <c>schema_version</c>, encoders aposentados só quando
/// deixam de ser correntes — volta a valer no primeiro certame publicado em qualquer ambiente,
/// inclusive homologação. Não se espera a primeira release de produção: publicar em homologação
/// já cria um envelope que precisa ser preservado.
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
    private static readonly string[] BlocosReais =
    [
        "tipoProcesso",
        "periodo",
        "etapas",
        "distribuicao",
        "modalidades",
        "ofertas",
        "atendimento",
        "bonusRegional",
        "cascataRemanejamento",
        "criteriosDesempate",
        "classificacao",
        "hashesEdital",
        "cronogramaFases",
        "documentosExigidos",
        "vagas",
        "arvoreSatisfacao",
        "formulario",
        "divulgacao",
        "identidadesUnidade",
        "fatosColetados",
        "regrasDerivacao",
        "grafoDependencia",
        "versaoInterpretador",
        "modalidadesOfertadas",
        "taxaInscricao",
        "localidade",
        "algoritmoContagemPrazo",
        "calendarioDiasUteis",
    ];

    private readonly SnapshotPublicacaoCanonicalizer _encoder = new();

    public string SchemaVersion => "0.0.14";

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
            ? [.. BlocosReais, "retificacao"]
            : BlocosReais;

        leitor.ExigirChaves(payload, "$", chavesEsperadas);

        LerTipoProcesso(leitor, payload);

        DadosEdital? dados = EnvelopeCodecV11.LerDadosEdital(leitor, payload, out string hashDocumento);
        IReadOnlyList<EtapaProcesso> etapas = EnvelopeCodecV11.LerEtapas(leitor, payload);
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicao = EnvelopeCodecV11.LerDistribuicao(leitor, payload);
        OfertaAtendimentoEspecializado? atendimento = EnvelopeCodecV11.LerAtendimento(leitor, payload);
        ConfiguracaoBonusRegional? bonus = EnvelopeCodecV11.LerBonusRegional(leitor, payload);
        ConfiguracaoCascataRemanejamento? cascata = EnvelopeCodecV11.LerCascataRemanejamento(leitor, payload);
        IReadOnlyList<CriterioDesempate> desempate = EnvelopeCodecV11.LerCriteriosDesempate(leitor, payload);
        ConfiguracaoClassificacao? classificacao = EnvelopeCodecV11.LerClassificacao(leitor, payload);
        IReadOnlyList<FaseCronograma> cronogramaFases = EnvelopeCodecV11.LerCronogramaFases(leitor, payload, comId: true);
        EnvelopeCodecV11.LerIdentidadesUnidade(leitor, payload);
        (ResultadoConformidade? conformidade, IReadOnlyList<DocumentoExigido> documentosExigidos, ReferenciaTemporalFatos? referenciaTemporalFatos,
            IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatosCongelados) = EnvelopeCodecV13.LerDocumentosExigidos(leitor, payload);
        (string? formularioTitulo, string? formularioTermoAceiteTexto) = LerFormulario(leitor, payload);
        ConfiguracaoDivulgacao? configuracaoDivulgacao = LerDivulgacao(leitor, payload);
        ConfiguracaoTaxaInscricao? configuracaoTaxaInscricao = LerTaxaInscricao(leitor, payload);
        (LocalidadeRegente? localidade, string? fusoHorario) = LerLocalidade(leitor, payload);
        ReferenciaRegra? algoritmoContagemPrazo = LerAlgoritmoContagemPrazo(leitor, payload);
        CalendarioDiasUteisCongelado? calendarioDiasUteis = LerCalendarioDiasUteis(leitor, payload, cronogramaFases);
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

        (IReadOnlyList<FatoColetado> Fatos, IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresSelecionaveis)
            fatosColetadosLidos = LerFatosColetados(leitor, payload);
        IReadOnlyList<FatoColetado> fatosColetados = fatosColetadosLidos.Fatos;
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
            documentosExigidos, todosOsNos, referenciaTemporalFatos, fatosColetados, regrasDerivacao,
            cascataRemanejamento: cascata,
            formularioTitulo: formularioTitulo,
            formularioTermoAceiteTexto: formularioTermoAceiteTexto,
            configuracaoDivulgacao: configuracaoDivulgacao,
            configuracaoTaxaInscricao: configuracaoTaxaInscricao,
            localidade: localidade,
            algoritmoContagemPrazo: algoritmoContagemPrazo);
        return Result<EnvelopeReidratado>.Success(
            new EnvelopeReidratado(
                grafo, dados!, hashDocumento, fusoHorario!, retificacao, conformidade,
                metadadosFatosCongelados, fatosColetadosLidos.ValoresSelecionaveis, calendarioDiasUteis));
    }

    /// <summary>
    /// Lê e fecha a forma do tipo autocontido. O agregado vivo já carrega a
    /// mesma cópia por valor e a recanonicalização posterior prova a paridade;
    /// o decoder não consulta Configuração para validar este dado histórico.
    /// </summary>
    private static void LerTipoProcesso(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject tipo = leitor.Objeto(payload, "tipoProcesso", "$");
        if (leitor.Falhou)
        {
            return;
        }

        leitor.ExigirChaves(tipo, "tipoProcesso", "origemId", "codigo", "nome");
        leitor.Identificador(tipo, "origemId", "tipoProcesso");
        leitor.TextoNaoVazio(tipo, "codigo", "tipoProcesso");
        leitor.TextoNaoVazio(tipo, "nome", "tipoProcesso");
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
    /// <c>ValoresSelecionaveis</c> (issue #1059, UNI-REQ-0072) é o dicionário reidratado —
    /// completo, uma entrada por fato coletado (array para os de seleção, <see langword="null"/>
    /// para os demais) — que <see cref="Application.Services.RestauradorDeConfiguracao"/> repassa
    /// intacto para recanonicalizar, sem reconsultar o catálogo vivo.
    /// </summary>
    private static (
        IReadOnlyList<FatoColetado> Fatos,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresSelecionaveis)
        LerFatosColetados(LeitorEnvelope leitor, JsonObject payload)
    {
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis = new(StringComparer.Ordinal);

        JsonArray array = leitor.Array(payload, "fatosColetados", "$");
        if (leitor.Falhou)
        {
            return ([], valoresSelecionaveis);
        }

        List<FatoColetado> fatos = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"fatosColetados[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "fatosColetados");
            leitor.ExigirChaves(
                item, path,
                "fatoCodigo", "ordem", "rotulo", "tipoRenderizacao", "obrigatorio", "precondicao", "valoresSelecionaveis");

            string fatoCodigo = leitor.TextoNaoVazio(item, "fatoCodigo", path, LimitesDoEnvelope.Fato);
            int ordem = leitor.Inteiro(item, "ordem", path);
            string rotulo = leitor.TextoNaoVazio(item, "rotulo", path, LimitesDoEnvelope.NomeDeCadastro);
            string tipoRenderizacaoCodigo = leitor.TextoNaoVazio(item, "tipoRenderizacao", path);
            bool obrigatorio = leitor.Booleano(item, "obrigatorio", path);
            if (leitor.Falhou)
            {
                return ([], valoresSelecionaveis);
            }

            TipoRenderizacao tipoRenderizacao = TipoRenderizacaoCodigo.FromCodigo(tipoRenderizacaoCodigo);

            IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> condicoes =
                LerDnf(leitor, item, "precondicao", path);
            if (leitor.Falhou)
            {
                return ([], valoresSelecionaveis);
            }

            List<CondicaoPrecondicaoFato> precondicoes = [];
            foreach ((int clausula, string fato, Operador operador, JsonElement valor) in condicoes)
            {
                Result<CondicaoPrecondicaoFato> condicao = CondicaoPrecondicaoFato.Criar(clausula, fato, operador, valor);
                if (condicao.IsFailure)
                {
                    return (leitor.Propagar<IReadOnlyList<FatoColetado>>(condicao.Error!) ?? [], valoresSelecionaveis);
                }

                precondicoes.Add(condicao.Value!);
            }

            IReadOnlyList<ValorDominioDeclaradoCongelado>? valoresDoFato =
                LerValoresSelecionaveis(leitor, item, path, tipoRenderizacao);
            if (leitor.Falhou)
            {
                return ([], valoresSelecionaveis);
            }

            Result<FatoColetado> fatoColetado = FatoColetado.Criar(
                fatoCodigo, ordem, rotulo, tipoRenderizacao, obrigatorio, precondicoes);
            if (fatoColetado.IsFailure)
            {
                return (leitor.Propagar<IReadOnlyList<FatoColetado>>(fatoColetado.Error!) ?? [], valoresSelecionaveis);
            }

            fatos.Add(fatoColetado.Value!);

            // A unicidade de fatoCodigo é reconferida por ValidarBlocoDeFatosEDerivacao, mais
            // adiante — um envelope adulterado com fato duplicado sobrescreve a entrada aqui
            // (last-wins) e é recusado como malformado lá, não aqui.
            valoresSelecionaveis[fatoCodigo] = valoresDoFato;
        }

        return (fatos, valoresSelecionaveis);
    }

    /// <summary>
    /// Os valores selecionáveis de um fato coletado (issue #1059, UNI-REQ-0072) — bicondicional
    /// com <paramref name="tipoRenderizacao"/> (D1-bis do plano da issue): <c>SELECAO_UNICA</c>/
    /// <c>SELECAO_MULTIPLA</c> exige array com cardinalidade mínima 1 (issue #1077: nunca vazio);
    /// <c>BOOLEANO</c>/<c>NUMERO</c> exige <see langword="null"/>. Um envelope que descumpra a
    /// bicondicional em qualquer sentido — seletor mudo (<c>null</c> ou <c>[]</c> onde deveria
    /// ter opção), ou vocabulário pendurado num campo booleano/numérico — é malformado. Cada
    /// item exige <c>ordem</c> não negativa e
    /// <c>valorCodigo</c> sem repetição, mesma disciplina que
    /// <c>EnvelopeCodecV13.LerValoresDominioDeclarados</c> aplica a <c>valoresDominioDeclarados</c>.
    /// </summary>
    private static IReadOnlyList<ValorDominioDeclaradoCongelado>? LerValoresSelecionaveis(
        LeitorEnvelope leitor, JsonObject item, string pathPai, TipoRenderizacao tipoRenderizacao)
    {
        string path = $"{pathPai}.valoresSelecionaveis";
        bool ehFatoDeSelecao = tipoRenderizacao is TipoRenderizacao.SelecaoUnica or TipoRenderizacao.SelecaoMultipla;

        if (item["valoresSelecionaveis"] is not JsonNode node)
        {
            if (ehFatoDeSelecao)
            {
                return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}' é null, mas o tipo de renderização é de seleção — a bicondicional exige um array."));
            }

            return null;
        }

        if (node is not JsonArray array)
        {
            return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}' deveria ser um array ou null."));
        }

        if (!ehFatoDeSelecao)
        {
            return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'{path}' é um array, mas o tipo de renderização '{tipoRenderizacao}' não é de seleção — " +
                "a bicondicional exige null."));
        }

        List<ValorDominioDeclaradoCongelado> valores = [];
        HashSet<string> codigos = new(StringComparer.Ordinal);
        for (int i = 0; i < array.Count; i++)
        {
            string itemPath = $"{path}[{i}]";
            JsonObject valorItem = leitor.ItemObjeto(array, i, path);
            leitor.ExigirChaves(valorItem, itemPath, "valorCodigo", "descricao", "ordem");

            string valorCodigo = leitor.TextoNaoVazio(valorItem, "valorCodigo", itemPath);
            string? descricao = leitor.TextoOpcional(valorItem, "descricao", itemPath);
            int ordem = leitor.Inteiro(valorItem, "ordem", itemPath);
            if (leitor.Falhou)
            {
                return null;
            }

            if (ordem < 0)
            {
                return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{itemPath}.ordem' não pode ser negativa."));
            }

            if (!codigos.Add(valorCodigo))
            {
                return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}': o valor '{valorCodigo}' aparece mais de uma vez."));
            }

            valores.Add(new ValorDominioDeclaradoCongelado(valorCodigo, descricao, ordem));
        }

        // issue #1077: a bicondicional exige array, mas array VAZIO para um fato de seleção não
        // é forma válida — um seletor publicado sem opção nenhuma não é respondível. Um envelope
        // adulterado com "valoresSelecionaveis": [] é malformado, não um caso legítimo a
        // reidratar silenciosamente. ehFatoDeSelecao já é garantidamente true aqui (o ramo
        // !ehFatoDeSelecao retornou acima) — checagem redundante removida (achado CodeQL).
        if (valores.Count == 0)
        {
            return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'{path}' é um array vazio, mas o tipo de renderização é de seleção — a cardinalidade mínima é 1."));
        }

        return valores;
    }

    /// <summary>
    /// Título e termo de aceite do formulário de inscrição (Story #559) — forma fechada mesmo
    /// quando os dois campos são nulos, mesmo raciocínio de <see cref="LerDadosEdital"/> para
    /// campos individualmente opcionais (não um toggle por presença como
    /// <see cref="EnvelopeCodecV11.LerBonusRegional"/>).
    /// </summary>
    private static (string? Titulo, string? TermoAceiteTexto) LerFormulario(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "formulario", "$");
        if (leitor.Falhou)
        {
            return (null, null);
        }

        leitor.ExigirChaves(bloco, "formulario", "titulo", "termoAceiteTexto");

        string? titulo = leitor.TextoOpcional(bloco, "titulo", "formulario", LimitesDoEnvelope.NomeDeCadastro);
        string? termoAceiteTexto = leitor.TextoOpcional(bloco, "termoAceiteTexto", "formulario", LimitesDoEnvelope.TermoDeAceite);
        return leitor.Falhou ? (null, null) : (titulo, termoAceiteTexto);
    }

    /// <summary>
    /// Divulgação pública do certame (UNI-REQ-0050, issue #563) — forma fechada, no molde de
    /// <see cref="LerFormulario"/>. <see cref="LeitorEnvelope.ExigirChaves"/> roda ANTES de
    /// qualquer retorno antecipado: é o que fecha a gramática do bloco agora que a guarda global
    /// de stub saiu — o antigo <c>{"status":"nao_construido"}</c> é recusado exatamente aqui, por
    /// faltarem as três chaves e sobrar <c>status</c>.
    /// </summary>
    /// <remarks>
    /// O decodificador é tão estrito quanto o encoder: vocabulário fechado, piso
    /// <c>numero_inscricao</c> sempre presente e <c>nome</c>/<c>nome_abreviado</c> nunca juntos
    /// são reconferidos por <see cref="ConfiguracaoDivulgacao.Criar"/>, que este método chama
    /// como fonte única daquelas invariantes. O que só a LEITURA pode conferir — porque o
    /// congelamento nunca as violaria pelo caminho normal de escrita — fica aqui: repetição,
    /// ordem canônica (pela MESMA política do encoder,
    /// <see cref="SnapshotPublicacaoCanonicalizer.OrdenarPorConteudo(IEnumerable{JsonValue})"/>,
    /// nunca um <see cref="StringComparer.Ordinal"/> reimplementado), a bicondicional entre
    /// <c>nome_abreviado</c> e <c>regraNomeAbreviado</c>, o identificador de regra conhecido, e a
    /// forma canônica (Trim + NFC) da justificativa. Um bloco cujo conteúdo é exatamente o
    /// default minimizado (D5) reidrata como <see langword="null"/> — a restauração não fabrica
    /// entidade para um processo que nunca configurou divulgação.
    /// </remarks>
    private static ConfiguracaoDivulgacao? LerDivulgacao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "divulgacao", "$");
        if (leitor.Falhou)
        {
            return null;
        }

        leitor.ExigirChaves(bloco, "divulgacao", "camposPublicos", "regraNomeAbreviado", "justificativa");

        IReadOnlyList<string> camposPublicos = leitor.Textos(bloco, "camposPublicos", "divulgacao");
        string? regraNomeAbreviado = leitor.TextoOpcional(bloco, "regraNomeAbreviado", "divulgacao");
        string? justificativa = leitor.TextoOpcional(bloco, "justificativa", "divulgacao", LimitesDoEnvelope.Justificativa);
        if (leitor.Falhou)
        {
            return null;
        }

        if (camposPublicos.Distinct(StringComparer.Ordinal).Count() != camposPublicos.Count)
        {
            return leitor.Propagar<ConfiguracaoDivulgacao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, "'divulgacao.camposPublicos' tem token repetido."));
        }

        // A MESMA política do encoder (ADR-0109 D9) — nunca um comparador próprio, que
        // coincidiria hoje (três tokens ASCII) e divergiria no dia em que o vocabulário crescesse
        // com um token não-ASCII. Não reordena: fora de ordem é malformado.
        IReadOnlyList<string> naOrdemCanonica = [.. SnapshotPublicacaoCanonicalizer
            .OrdenarPorConteudo(camposPublicos.Select(static c => JsonValue.Create(c)!))
            .Select(static n => n!.GetValue<string>())];
        if (!naOrdemCanonica.SequenceEqual(camposPublicos, StringComparer.Ordinal))
        {
            return leitor.Propagar<ConfiguracaoDivulgacao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, "'divulgacao.camposPublicos' não está na ordem canônica."));
        }

        bool temNomeAbreviado = camposPublicos.Contains(ConfiguracaoDivulgacao.NomeAbreviado, StringComparer.Ordinal);
        if (temNomeAbreviado != (regraNomeAbreviado is not null))
        {
            return leitor.Propagar<ConfiguracaoDivulgacao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "'divulgacao.regraNomeAbreviado' tem de estar presente se, e somente se, 'camposPublicos' contém 'nome_abreviado'."));
        }

        if (regraNomeAbreviado is not null && !RegrasDeNomeAbreviado.EhConhecida(regraNomeAbreviado))
        {
            return leitor.Propagar<ConfiguracaoDivulgacao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'divulgacao.regraNomeAbreviado' não é uma regra conhecida: '{regraNomeAbreviado}'."));
        }

        // O codificador NUNCA emite justificativa vazia nem só-espaços — quando não há
        // justificativa, ele emite null (D5). Uma string vazia/em branco só chega aqui por
        // adulteração, e tem de ser recusada ANTES da conferência de Trim/NFC abaixo: uma
        // string vazia já é, trivialmente, a sua própria forma Trim+NFC, então a conferência
        // seguinte não a pegaria, e o teste de default (mais abaixo) também não — ele só
        // reconhece justificativa null, e uma string vazia não é null.
        if (justificativa is not null && string.IsNullOrWhiteSpace(justificativa))
        {
            return leitor.Propagar<ConfiguracaoDivulgacao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "'divulgacao.justificativa' não pode ser vazia nem só espaços — o codificador nunca emite essa forma, só null."));
        }

        if (justificativa is not null
            && !string.Equals(HashCanonicalComputer.NormalizeNfc(justificativa.Trim()), justificativa, StringComparison.Ordinal))
        {
            return leitor.Propagar<ConfiguracaoDivulgacao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "'divulgacao.justificativa' não está na forma canônica (sem espaço nas bordas, em NFC)."));
        }

        if (ConfiguracaoDivulgacao.EhDefaultMinimizado(camposPublicos, justificativa))
        {
            return null;
        }

        Result<ConfiguracaoDivulgacao> configuracao = ConfiguracaoDivulgacao.Criar(camposPublicos, justificativa);
        return configuracao.IsFailure ? leitor.Propagar<ConfiguracaoDivulgacao>(configuracao.Error!) : configuracao.Value;
    }

    /// <summary>
    /// Taxa de inscrição e isenção (issue #1112) — forma do bloco no molde do toggle
    /// <c>"presente"</c> de <see cref="EnvelopeCodecV11.LerBonusRegional"/>, mas o desfecho
    /// <c>presente:false</c> nunca é um estado válido AQUI: CA-01 recusa <see cref="Domain.Entities.ProcessoSeletivo.Publicar"/>
    /// quando <see cref="Domain.Entities.ProcessoSeletivo.ConfiguracaoTaxaInscricao"/> é
    /// <see langword="null"/>, então nenhum envelope que passou pelo gate consegue congelar
    /// <c>presente:false</c> — só chega aqui bytes adulterados ou de um caminho que nunca deveria
    /// ter sido aceito. Decodificar como "sem taxa declarada" repetiria, na leitura, o próprio
    /// default silencioso que a issue existe para banir na escrita.
    /// </summary>
    /// <remarks>
    /// O decodificador é tão estrito quanto o encoder (mesmo raciocínio de
    /// <see cref="LerDivulgacao"/>): vocabulário fechado de <c>fundamentos</c> (token desconhecido
    /// é malformado, nunca ignorado), sem repetição, na MESMA ordem canônica que
    /// <see cref="SnapshotPublicacaoCanonicalizer.OrdenarPorConteudo(IEnumerable{JsonValue})"/>
    /// usaria — o encoder nunca embaralha porque <see cref="ConfiguracaoTaxaInscricao.Criar"/> já
    /// deduplica e ordena antes de guardar; achar duplicata ou ordem diferente aqui é sinal de
    /// bytes que não vieram desse caminho. <c>cobra:true</c> com <c>fundamentos:[]</c> é a mesma
    /// classe de impossível: a fábrica recusa a combinação (issue #1310), então o encoder nunca
    /// a emite.
    /// </remarks>
    /// <summary>
    /// Lê o bloco da convenção de contagem congelada (UNI-REQ-0112).
    /// </summary>
    /// <remarks>
    /// <para>
    /// O bloco é fechado nas duas formas: ausente, só a chave de presença; presente, a
    /// identidade inteira. A combinação parcial não é representável — uma versão que
    /// declarasse código sem hash não provaria qual definição aplicou, que é a única razão
    /// de o bloco existir.
    /// </para>
    /// <para>
    /// A referência é reconstruída por <see cref="ReferenciaRegra.Criar"/>, e não por
    /// atribuição direta, pela mesma razão da localidade: um envelope adulterado com hash
    /// fora de forma é malformado, e reidratá-lo daria ao processo restaurado uma
    /// referência que o domínio recusaria criar.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Calendário congelado por valor (UNI-REQ-0080). Reconstruído pelos mesmos value objects que
    /// a publicação usa, e não por atribuição direta: a assimetria entre escrita e leitura é
    /// invisível ao round-trip byte a byte — o encoder reemitiria o valor sujo tal qual, a prova
    /// passaria, e a configuração restaurada carregaria um calendário que o domínio recusaria
    /// criar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Recusa, nunca normaliza.</b> Aparar espaço ou corrigir caixa aqui faria o valor divergir
    /// dos bytes congelados, e a recanonicalização passaria a recusar um artefato legítimo.
    /// </para>
    /// <para>
    /// A ordem canônica é conferida em vez de reordenada, pela mesma razão: o envelope é comparado
    /// byte a byte, e reordenar em silêncio esconderia um artefato que não foi emitido por este
    /// encoder.
    /// </para>
    /// </remarks>
    private static CalendarioDiasUteisCongelado? LerCalendarioDiasUteis(
        LeitorEnvelope leitor,
        JsonObject payload,
        IReadOnlyList<FaseCronograma> cronogramaFases)
    {
        JsonObject bloco = leitor.Objeto(payload, "calendarioDiasUteis", "$");
        if (leitor.Falhou)
        {
            return null;
        }

        bool presente = leitor.Booleano(bloco, "presente", "calendarioDiasUteis");
        if (leitor.Falhou)
        {
            return null;
        }

        if (!presente)
        {
            leitor.ExigirChaves(bloco, "calendarioDiasUteis", "presente");

            // Invariante entre blocos: nenhuma transição que gera versão publica processo com
            // fase que aceita recurso e sem calendário vigente. Um envelope nessa combinação não
            // foi produzido por publicação legítima, e aceitá-lo restauraria configuração que o
            // gate recusa — o round-trip byte a byte não acusaria, porque o encoder reemitiria a
            // mesma ausência.
            if (cronogramaFases.Any(static fase => fase.RegraRecurso is not null))
            {
                return leitor.Propagar<CalendarioDiasUteisCongelado?>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    "'calendarioDiasUteis' declara ausência num processo com fase que aceita recurso — "
                        + "combinação que nenhuma publicação produz."));
            }

            return null;
        }

        leitor.ExigirChaves(bloco, "calendarioDiasUteis", "presente", "origemId", "versaoDataset", "diasNaoUteis");

        Guid origemId = leitor.Identificador(bloco, "origemId", "calendarioDiasUteis");
        string versaoDataset = leitor.TextoNaoVazio(bloco, "versaoDataset", "calendarioDiasUteis");
        JsonArray dias = leitor.Array(bloco, "diasNaoUteis", "calendarioDiasUteis");
        if (leitor.Falhou)
        {
            return null;
        }

        List<DiaNaoUtilCongelado> congelados = [];
        for (int i = 0; i < dias.Count; i++)
        {
            string path = $"calendarioDiasUteis.diasNaoUteis[{i}]";
            JsonObject item = leitor.ItemObjeto(dias, i, path);
            if (leitor.Falhou)
            {
                return null;
            }

            leitor.ExigirChaves(item, path, "data", "abrangencia", "municipioIbge", "municipioNome", "uf");

            DateOnly data = leitor.Data(item, "data", path);
            string abrangencia = leitor.TextoNaoVazio(item, "abrangencia", path);
            string? municipioIbge = leitor.TextoOpcional(item, "municipioIbge", path);
            string? municipioNome = leitor.TextoOpcional(item, "municipioNome", path);
            string? uf = leitor.TextoOpcional(item, "uf", path);
            if (leitor.Falhou)
            {
                return null;
            }

            Result<DiaNaoUtilCongelado> dia = DiaNaoUtilCongelado.Criar(
                data, abrangencia, municipioIbge, municipioNome, uf);
            if (dia.IsFailure)
            {
                return leitor.Propagar<CalendarioDiasUteisCongelado?>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}' congelado não é um dia não útil válido: {dia.Error!.Message}"));
            }

            // A factory normaliza — apara espaço e sobe a UF para maiúscula —, porque é o caminho
            // de ESCRITA. Aqui isso seria aceitar em silêncio um artefato que este encoder nunca
            // emitiria: o objeto reidratado passaria a divergir dos bytes que o originaram, e a
            // saída de Reidratar deixaria de representar fielmente o envelope. Comparar o lido
            // com o normalizado é o que mantém o decoder fail-closed sem abrir mão da fonte única
            // de validação.
            if (!MesmoTextoOriginal(dia.Value!, abrangencia, municipioIbge, municipioNome, uf))
            {
                return leitor.Propagar<CalendarioDiasUteisCongelado?>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}' congelado não está na forma canônica — espaço em volta do valor ou UF fora de caixa alta."));
            }

            congelados.Add(dia.Value!);
        }

        // A ordem do artefato tem de ser a canônica. Criar() reordena; comparar antes é o que
        // distingue "envelope emitido por este encoder" de "envelope montado à mão".
        List<DiaNaoUtilCongelado> canonica = [.. CalendarioDiasUteisCongelado.Ordenar(congelados)];
        if (!canonica.SequenceEqual(congelados))
        {
            return leitor.Propagar<CalendarioDiasUteisCongelado?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "'calendarioDiasUteis.diasNaoUteis' não está na ordem canônica (data, abrangência, município, UF)."));
        }

        Result<CalendarioDiasUteisCongelado> calendario =
            CalendarioDiasUteisCongelado.Criar(origemId, versaoDataset, congelados);

        if (calendario.IsFailure)
        {
            return leitor.Propagar<CalendarioDiasUteisCongelado?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'calendarioDiasUteis' congelado é inválido: {calendario.Error!.Message}"));
        }

        // Mesma razão da conferência por dia: a factory apara espaço da versão, e aceitar o
        // texto normalizado devolveria um objeto que não representa os bytes decodificados.
        if (!string.Equals(calendario.Value!.VersaoDataset, versaoDataset, StringComparison.Ordinal)
            || !string.Equals(HashCanonicalComputer.NormalizeNfc(versaoDataset), versaoDataset, StringComparison.Ordinal))
        {
            return leitor.Propagar<CalendarioDiasUteisCongelado?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "'calendarioDiasUteis.versaoDataset' não está na forma canônica — espaço em volta do valor "
                    + "ou texto fora da normalização Unicode que o encoder aplica."));
        }

        return calendario.Value;
    }

    /// <summary>
    /// Se o dia reconstruído reproduz, caractere a caractere, os textos que estavam no envelope.
    /// Divergir significa que a factory normalizou algo — e o artefato não era o que este encoder
    /// emite.
    /// </summary>
    private static bool MesmoTextoOriginal(
        DiaNaoUtilCongelado dia,
        string abrangencia,
        string? municipioIbge,
        string? municipioNome,
        string? uf) =>
        string.Equals(dia.Abrangencia, abrangencia, StringComparison.Ordinal)
        && string.Equals(dia.MunicipioIbge, municipioIbge, StringComparison.Ordinal)
        && string.Equals(dia.MunicipioNome, municipioNome, StringComparison.Ordinal)
        && string.Equals(dia.Uf, uf, StringComparison.Ordinal)
        // O encoder normaliza o nome do município para NFC. Sem exigir o mesmo aqui, um texto
        // decomposto passaria na comparação — a factory só apara espaço — e a recanonicalização
        // mudaria os bytes, revelando a divergência tarde demais, já na restauração.
        && (municipioNome is null
            || string.Equals(HashCanonicalComputer.NormalizeNfc(municipioNome), municipioNome, StringComparison.Ordinal));

    private static ReferenciaRegra? LerAlgoritmoContagemPrazo(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "algoritmoContagemPrazo", "$");
        if (leitor.Falhou)
        {
            return null;
        }

        bool presente = leitor.Booleano(bloco, "presente", "algoritmoContagemPrazo");
        if (leitor.Falhou)
        {
            return null;
        }

        if (!presente)
        {
            leitor.ExigirChaves(bloco, "algoritmoContagemPrazo", "presente");
            return null;
        }

        leitor.ExigirChaves(bloco, "algoritmoContagemPrazo", "presente", "codigo", "versao", "hash");

        string codigo = leitor.TextoNaoVazio(bloco, "codigo", "algoritmoContagemPrazo");
        string versao = leitor.TextoNaoVazio(bloco, "versao", "algoritmoContagemPrazo");
        string hash = leitor.TextoNaoVazio(bloco, "hash", "algoritmoContagemPrazo");
        if (leitor.Falhou)
        {
            return null;
        }

        Result<ReferenciaRegra> referencia = ReferenciaRegra.Criar(codigo, versao, hash);
        return referencia.IsFailure
            ? leitor.Propagar<ReferenciaRegra?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'algoritmoContagemPrazo' congelado não é uma referência de regra válida: {referencia.Error!.Message}"))
            : referencia.Value;
    }

    /// <summary>
    /// Lê o bloco fechado com a localidade regente e o fuso aplicado (UNI-REQ-0111).
    /// </summary>
    /// <remarks>
    /// A localidade é reconstruída por <see cref="LocalidadeRegente.Criar"/>, e não por atribuição
    /// direta: um envelope adulterado com código fora de forma ou UF incoerente é malformado, e
    /// reidratá-lo daria ao processo restaurado uma localidade que o domínio recusaria criar. O
    /// fuso é validado como zona conhecida aqui mesmo — deixar
    /// <c>FindSystemTimeZoneById</c> estourar depois transformaria envelope adulterado em exceção
    /// sem causa nomeada.
    /// </remarks>
    private static (LocalidadeRegente? Localidade, string? FusoHorario) LerLocalidade(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "localidade", "$");
        if (leitor.Falhou)
        {
            return (null, null);
        }

        leitor.ExigirChaves(bloco, "localidade", "codigoIbge", "nome", "uf", "fusoHorario");

        string codigoIbge = leitor.TextoNaoVazio(bloco, "codigoIbge", "localidade");
        string nome = leitor.TextoNaoVazio(bloco, "nome", "localidade");
        string uf = leitor.TextoNaoVazio(bloco, "uf", "localidade");
        string fusoHorario = leitor.TextoNaoVazio(bloco, "fusoHorario", "localidade");
        if (leitor.Falhou)
        {
            return (null, null);
        }

        Result<LocalidadeRegente> localidade = LocalidadeRegente.Criar(codigoIbge, nome, uf);
        if (localidade.IsFailure)
        {
            return (leitor.Propagar<LocalidadeRegente?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'localidade' congelada não é uma referência de cidade válida: {localidade.Error!.Message}")), null);
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(fusoHorario, out _))
        {
            return (leitor.Propagar<LocalidadeRegente?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'localidade.fusoHorario' congelado ('{fusoHorario}') não é uma zona reconhecida por este ambiente.")), null);
        }

        return (localidade.Value!, fusoHorario);
    }

    private static ConfiguracaoTaxaInscricao? LerTaxaInscricao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "taxaInscricao", "$");
        if (leitor.Falhou)
        {
            return null;
        }

        bool presente = leitor.Booleano(bloco, "presente", "taxaInscricao");
        if (leitor.Falhou)
        {
            return null;
        }

        if (!presente)
        {
            return leitor.Propagar<ConfiguracaoTaxaInscricao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "'taxaInscricao.presente' não pode ser falso — CA-01 recusa publicar sem declarar taxa."));
        }

        leitor.ExigirChaves(bloco, "taxaInscricao", "presente", "cobra", "valor", "fundamentos");

        bool cobra = leitor.Booleano(bloco, "cobra", "taxaInscricao");
        decimal? valor = leitor.DecimalOpcional(bloco, "valor", ConfiguracaoTaxaInscricao.ValorEscala, "taxaInscricao", LimitesDoEnvelope.PrecisaoTaxaInscricao);
        IReadOnlyList<string> fundamentos = leitor.Textos(bloco, "fundamentos", "taxaInscricao");
        if (leitor.Falhou)
        {
            return null;
        }

        if (fundamentos.Distinct(StringComparer.Ordinal).Count() != fundamentos.Count)
        {
            return leitor.Propagar<ConfiguracaoTaxaInscricao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, "'taxaInscricao.fundamentos' tem token repetido."));
        }

        IReadOnlyList<string> fundamentosNaOrdemCanonica = [.. SnapshotPublicacaoCanonicalizer
            .OrdenarPorConteudo(fundamentos.Select(static f => JsonValue.Create(f)!))
            .Select(static n => n!.GetValue<string>())];
        if (!fundamentosNaOrdemCanonica.SequenceEqual(fundamentos, StringComparer.Ordinal))
        {
            return leitor.Propagar<ConfiguracaoTaxaInscricao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, "'taxaInscricao.fundamentos' não está na ordem canônica."));
        }

        Result<ConfiguracaoTaxaInscricao> configuracao = ConfiguracaoTaxaInscricao.Criar(cobra, valor, fundamentos);
        return configuracao.IsFailure ? leitor.Propagar<ConfiguracaoTaxaInscricao>(configuracao.Error!) : configuracao.Value;
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

        // O encoder nunca emite DNF vazio: "sem condição" é `null`, não `[]`. Um array vazio (ou uma
        // cláusula vazia `[]`) num envelope adulterado colapsaria para "sem pré-condição"/"regra âncora"
        // — uma forma que a projeção viva não produz — e a restauração o recanonicalizaria como `null`,
        // aceitando em silêncio um snapshot que o certame nunca congelou. Recusa como malformado.
        if (clausulas.Count == 0)
        {
            return leitor.Propagar<IReadOnlyList<(int, string, Operador, JsonElement)>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'{pathPai}.{chave}' é um array de cláusulas vazio — a ausência de condição é `null`, nunca `[]`.")) ?? [];
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

            if (condicoesDaClausula.Count == 0)
            {
                return leitor.Propagar<IReadOnlyList<(int, string, Operador, JsonElement)>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{clausulaPath}' é uma cláusula vazia — toda cláusula tem ao menos uma condição.")) ?? [];
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
        // Pré-produção: há UMA semântica de motor ("1") e nenhum snapshot congelado em banco. Exigir
        // que a versão do envelope seja a corrente é a leitura honesta enquanto não existe versão
        // legada a preservar — uma evolução da semântica antes da produção reescreve as fixtures (bump
        // de forma no 0.x), não reidrata um "1" antigo. O despacho por versão do interpretador (aceitar
        // uma versão anterior e recanonicalizar na semântica DELA, com encoder legado) é o mesmo
        // versionamento forense deliberadamente adiado para a 1ª release de produção (1.0.0) — a versão
        // é congelada AGORA justamente para esse despacho futuro poder existir sem migrar dado.
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
        // Bloco ausente ou JSON `null` (uma coluna adulterada com hash recomputado consegue produzir
        // `"grafoDependencia": null`): o recomputado nunca é nulo, então diverge — recusa fail-closed,
        // nunca um NullReferenceException (500).
        if (payload[chave] is not JsonNode congelado)
        {
            return true;
        }

        // O perfil serializa um JsonObject; envolve-se cada lado num wrapper para comparar array ou
        // objeto pela mesma projeção. O congelado é clonado antes de reparentar — mutá-lo tiraria o
        // bloco do payload que ainda está sendo lido.
        byte[] bytesEsperados = PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["v"] = esperado });
        byte[] bytesCongelados = PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["v"] = congelado.DeepClone() });
        return !bytesEsperados.AsSpan().SequenceEqual(bytesCongelados);
    }
}

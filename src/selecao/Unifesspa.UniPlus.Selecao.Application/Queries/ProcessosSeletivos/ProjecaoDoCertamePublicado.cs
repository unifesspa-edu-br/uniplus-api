namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Projeta os blocos públicos do envelope congelado para o contrato do certame.
/// </summary>
/// <remarks>
/// <b>Tipos declarados campo a campo</b>, nunca recorte de subárvore do documento congelado. A
/// distinção não é estilo: um contrato montado por filtro devolve o campo novo por omissão — basta
/// o envelope ganhar um bloco para ele atravessar a fronteira pública sem que ninguém decida. Com
/// a forma declarada, a fronteira é a assinatura do tipo, e o campo novo só passa quando alguém o
/// escreve aqui.
/// <para>
/// Toda extração confere presença, tipo e nulidade antes de usar o valor. Uma forma inesperada
/// recusa a leitura inteira em vez de emitir campo silenciosamente vazio: o certame é documento com
/// efeito jurídico, e meia projeção mente mais que uma recusa.
/// </para>
/// </remarks>
internal static class ProjecaoDoCertamePublicado
{
    /// <summary>
    /// Versão desta projeção pública. Sobe sempre que o formato desta resposta muda — acrescentar,
    /// remover ou renomear campo. O hash da configuração NÃO cobre essa mudança: ele identifica o
    /// envelope congelado, que continua o mesmo quando só a projeção muda, e um cache endereçado
    /// apenas por ele serviria a resposta antiga depois do deploy.
    /// </summary>
    public const string Versao = "1";

    /// <summary>
    /// Código único da recusa de leitura do envelope. Vive aqui, e não repetido em cada ponto que
    /// recusa, porque é ele que o mapeador de erros da API associa ao 422.
    /// </summary>
    internal const string CodigoEnvelopeInesperado = "CertamePublicado.EnvelopeInesperado";

    /// <summary>
    /// Recusa a leitura de um documento congelado que sequer é um objeto JSON — a mesma recusa que
    /// um bloco de forma inesperada produz, pela mesma razão: é falha de leitura, e uma exceção
    /// solta aqui viraria 500 num endereço anônimo.
    /// </summary>
    public static Result<CertamePublicadoDto> RecusarDocumentoIlegivel() => Recusar("documento do certame");

    /// <summary>
    /// O documento congelado como objeto JSON, ou <see langword="null"/> quando ele não é sequer
    /// isso. Uma só implementação para os dois contratos públicos do certame — o detalhe recusa,
    /// a vitrine omite, e ambos partem da mesma leitura.
    /// </summary>
    /// <remarks>
    /// Conteúdo que não fecha como JSON, ou que fecha como array ou escalar, só é alcançável por
    /// uma linha adulterada diretamente no banco — nunca pelo caminho de escrita, que sempre passa
    /// pelo canonicalizador. Ainda assim a leitura o trata como recusa: um <c>cast</c> cru viraria
    /// 500 num endereço anônimo.
    /// </remarks>
    internal static JsonObject? TentarLerDocumento(string documentoCongelado)
    {
        try
        {
            return JsonNode.Parse(documentoCongelado) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static Result<CertamePublicadoDto> Projetar(
        Guid processoSeletivoId,
        Guid atoCriadorId,
        string hashConfiguracao,
        JsonObject envelope)
    {
        if (!TentarObjeto(envelope, BlocoPublico("tipoProcesso"), out JsonObject? tipoProcessoNode)
            || !TentarTipoNomeado(tipoProcessoNode, out TipoCatalogadoCertameDto? tipoProcesso))
        {
            return Recusar("tipo do processo");
        }

        if (!TentarObjeto(envelope, BlocoPublico("periodo"), out JsonObject? periodo)
            || !TentarTextoOpcional(periodo, "numero", out string? numero)
            || !TentarInstante(periodo, "inicio", out DateTimeOffset inicio)
            || !TentarInstante(periodo, "fim", out DateTimeOffset fim))
        {
            return Recusar("período de inscrição");
        }

        if (!TentarObjeto(envelope, BlocoPublico("localidade"), out JsonObject? localidade)
            || !TentarTexto(localidade, "codigoIbge", out string codigoIbge)
            || !TentarTexto(localidade, "nome", out string localidadeNome)
            || !TentarTexto(localidade, "uf", out string uf)
            || !TentarTexto(localidade, "fusoHorario", out string fusoHorario))
        {
            return Recusar("localidade");
        }

        if (!TentarUnidadeAdministradora(envelope, out UnidadeAdministradoraCertameDto? unidade))
        {
            return Recusar("unidade administradora");
        }

        if (!TentarObjeto(envelope, BlocoPublico("hashesEdital"), out JsonObject? hashes)
            || !TentarIdentificador(hashes, "documentoEditalId", out Guid documentoEditalId)
            || !TentarTexto(hashes, "hashSha256", out string hashSha256))
        {
            return Recusar("documento do edital");
        }

        if (!TentarIdentificadores(envelope, BlocoPublico("ofertas"), out List<Guid>? ofertas))
        {
            return Recusar("ofertas");
        }

        if (!TentarTextos(envelope, BlocoPublico("modalidadesOfertadas"), out List<string>? modalidades))
        {
            return Recusar("modalidades ofertadas");
        }

        if (!TentarVagas(envelope, out List<QuadroDeVagasCertameDto>? vagas))
        {
            return Recusar("quadro de vagas");
        }

        if (!TentarEtapas(envelope, out List<EtapaCertameDto>? etapas))
        {
            return Recusar("etapas");
        }

        if (!TentarCronograma(envelope, out string? origemCandidatos, out List<FaseCronogramaCertameDto>? fases))
        {
            return Recusar("cronograma");
        }

        if (!TentarExigencias(envelope, out List<ExigenciaDocumentalCertameDto>? exigencias))
        {
            return Recusar("documentos exigidos");
        }

        if (!TentarAtendimento(envelope, out AtendimentoCertameDto? atendimento))
        {
            return Recusar("atendimento especializado");
        }

        if (!TentarTaxaInscricao(envelope, out TaxaInscricaoCertameDto? taxa))
        {
            return Recusar("taxa de inscrição");
        }

        if (!TentarRetificacao(envelope, out RetificacaoCertameDto? retificacao))
        {
            return Recusar("retificação");
        }

        return Result<CertamePublicadoDto>.Success(new CertamePublicadoDto(
            processoSeletivoId,
            atoCriadorId,
            Versao,
            hashConfiguracao,
            tipoProcesso,
            new PeriodoInscricaoCertameDto(numero, inicio, fim),
            new LocalidadeCertameDto(codigoIbge, localidadeNome, uf, fusoHorario),
            unidade,
            new DocumentoEditalCertameDto(documentoEditalId, hashSha256),
            ofertas,
            modalidades,
            vagas,
            etapas,
            origemCandidatos,
            fases,
            exigencias,
            atendimento,
            taxa,
            retificacao));
    }

    /// <summary>
    /// Recusa nomeando a parte do certame pelo nome que o CONTRATO PÚBLICO usa, nunca pela chave
    /// interna do envelope congelado. Um chamador anônimo não deve aprender a estrutura interna do
    /// documento pela mensagem de erro — é a mesma disciplina de ausência de oráculo que motivou o
    /// colapso das recusas numa só resposta.
    /// </summary>
    private static Result<CertamePublicadoDto> Recusar(string parte) =>
        Result<CertamePublicadoDto>.Failure(new DomainError(
            CodigoEnvelopeInesperado,
            $"A configuração publicada não pôde ser lida em '{parte}' — a leitura do certame foi recusada em vez de responder parcialmente."));

    /// <summary>
    /// O nome de um bloco de topo, conferido contra a classificação de exposição
    /// (<see cref="ClassificacaoDosBlocosDoCertame"/>) no momento em que ele é lido.
    /// </summary>
    /// <remarks>
    /// A classificação sozinha só recusa o bloco NOVO — o que nasce no envelope sem categoria. Ela
    /// não impediria ninguém de projetar aqui um bloco já classificado como INTERNO, que é a forma
    /// mais provável de o vazamento acontecer: o bloco existe, tem categoria, e a projeção passa a
    /// lê-lo mesmo assim, sem que verificação alguma acuse. Passar todo acesso de topo por esta
    /// conferência transforma isso em falha dura, que qualquer teste de projeção do certame
    /// alcança.
    /// </remarks>
    internal static string BlocoPublico(string chave) =>
        ClassificacaoDosBlocosDoCertame.Publicados.Contains(chave)
            ? chave
            : throw new InvalidOperationException(
                $"O contrato público do certame não projeta '{chave}': o bloco não está classificado como público.");

    /// <summary>Par código/nome, a forma que tipo de processo e tipo de etapa compartilham.</summary>
    internal static bool TentarTipoNomeado(JsonObject? objeto, [NotNullWhen(true)] out TipoCatalogadoCertameDto? tipo)
    {
        tipo = null;
        if (!TentarTexto(objeto, "codigo", out string codigo) || !TentarTexto(objeto, "nome", out string nome))
        {
            return false;
        }

        tipo = new TipoCatalogadoCertameDto(codigo, nome);
        return true;
    }

    private static bool TentarUnidadeAdministradora(
        JsonObject envelope,
        [NotNullWhen(true)] out UnidadeAdministradoraCertameDto? unidade)
    {
        unidade = null;
        if (!TentarObjeto(envelope, BlocoPublico("identidadesUnidade"), out JsonObject? bloco)
            || !TentarObjeto(bloco, "administradora", out JsonObject? administradora)
            || !TentarTexto(administradora, "sigla", out string sigla)
            || !TentarTexto(administradora, "nome", out string nome)
            || !TentarTexto(administradora, "tipo", out string tipo)
            || !TentarTextoOpcional(administradora, "cidadeNome", out string? cidadeNome)
            || !TentarTextoOpcional(administradora, "cidadeUf", out string? cidadeUf))
        {
            return false;
        }

        // O identificador de origem e o slug ficam fora: são chave de integração com o cadastro
        // institucional, não informação que o edital comunica ao candidato.
        unidade = new UnidadeAdministradoraCertameDto(sigla, nome, tipo, cidadeNome, cidadeUf);
        return true;
    }

    private static bool TentarVagas(JsonObject envelope, [NotNullWhen(true)] out List<QuadroDeVagasCertameDto>? vagas)
    {
        vagas = null;
        if (!TentarArray(envelope, BlocoPublico("vagas"), out JsonArray? array))
        {
            return false;
        }

        List<QuadroDeVagasCertameDto> lidas = [];
        foreach (JsonNode? item in array)
        {
            if (item is not JsonObject configuracao
                || !TentarIdentificador(configuracao, "ofertaCursoOrigemId", out Guid ofertaId)
                || !TentarInteiro(configuracao, "totalPublicado", out int total)
                || !TentarArray(configuracao, "quadro", out JsonArray? quadroArray))
            {
                return false;
            }

            List<VagaPorModalidadeCertameDto> quadro = [];
            foreach (JsonNode? linha in quadroArray)
            {
                if (linha is not JsonObject vaga
                    || !TentarTexto(vaga, "modalidadeCodigo", out string modalidadeCodigo)
                    || !TentarInteiro(vaga, "quantidade", out int quantidade))
                {
                    return false;
                }

                quadro.Add(new VagaPorModalidadeCertameDto(modalidadeCodigo, quantidade));
            }

            lidas.Add(new QuadroDeVagasCertameDto(ofertaId, quadro, total));
        }

        vagas = lidas;
        return true;
    }

    private static bool TentarEtapas(JsonObject envelope, [NotNullWhen(true)] out List<EtapaCertameDto>? etapas)
    {
        etapas = null;
        if (!TentarArray(envelope, BlocoPublico("etapas"), out JsonArray? array))
        {
            return false;
        }

        List<EtapaCertameDto> lidas = [];
        foreach (JsonNode? item in array)
        {
            if (item is not JsonObject etapa
                || !TentarTexto(etapa, "nome", out string nome)
                || !TentarTexto(etapa, "carater", out string carater)
                || !TentarObjeto(etapa, "tipoEtapa", out JsonObject? tipoEtapaNode)
                || !TentarTipoNomeado(tipoEtapaNode, out TipoCatalogadoCertameDto? tipoEtapa)
                || !TentarTextoOpcional(etapa, "peso", out string? peso)
                || !TentarTextoOpcional(etapa, "notaMinima", out string? notaMinima)
                || !TentarInteiroOpcional(etapa, "ordem", out int? ordem)
                || !TentarTextoOpcional(etapa, "faseCodigo", out string? faseCodigo)
                || !TentarInstanteOpcional(etapa, "inicio", out DateTimeOffset? inicio)
                || !TentarInstanteOpcional(etapa, "fim", out DateTimeOffset? fim)
                || !TentarBooleano(etapa, "emiteParecerIndividual", out bool emiteParecer))
            {
                return false;
            }

            lidas.Add(new EtapaCertameDto(
                nome, carater, tipoEtapa, peso, notaMinima, ordem, faseCodigo, inicio, fim, emiteParecer));
        }

        etapas = lidas;
        return true;
    }

    private static bool TentarCronograma(
        JsonObject envelope,
        [NotNullWhen(true)] out string? origemCandidatos,
        [NotNullWhen(true)] out List<FaseCronogramaCertameDto>? fases)
    {
        origemCandidatos = null;
        fases = null;

        if (!TentarObjeto(envelope, BlocoPublico("cronogramaFases"), out JsonObject? bloco)
            || !TentarTexto(bloco, "origemCandidatos", out string origem)
            || !TentarArray(bloco, "fases", out JsonArray? array))
        {
            return false;
        }

        List<FaseCronogramaCertameDto> lidas = [];
        foreach (JsonNode? item in array)
        {
            if (item is not JsonObject fase
                || !TentarInteiro(fase, "ordem", out int ordem)
                || !TentarTexto(fase, "codigo", out string codigo)
                || !TentarInstanteOpcional(fase, "inicio", out DateTimeOffset? inicio)
                || !TentarInstanteOpcional(fase, "fim", out DateTimeOffset? fim)
                || !TentarBooleano(fase, "coletaInscricao", out bool coletaInscricao)
                || !TentarBooleano(fase, "coletaSolicitacaoIsencao", out bool coletaIsencao)
                || !TentarBooleano(fase, "permiteComplementacao", out bool permiteComplementacao))
            {
                return false;
            }

            lidas.Add(new FaseCronogramaCertameDto(
                ordem, codigo, inicio, fim, coletaInscricao, coletaIsencao, permiteComplementacao));
        }

        origemCandidatos = origem;
        fases = lidas;
        return true;
    }

    /// <summary>
    /// Só o que o candidato precisa para reunir documentos. Os blocos irmãos de
    /// <c>documentosExigidos</c> — obrigatoriedades legais, referência temporal dos fatos e os
    /// metadados dos fatos que condicionam cada exigência — não entram.
    /// </summary>
    private static bool TentarExigencias(
        JsonObject envelope,
        [NotNullWhen(true)] out List<ExigenciaDocumentalCertameDto>? exigencias)
    {
        exigencias = null;
        if (!TentarObjeto(envelope, BlocoPublico("documentosExigidos"), out JsonObject? bloco)
            || !TentarArray(bloco, "exigencias", out JsonArray? array))
        {
            return false;
        }

        List<ExigenciaDocumentalCertameDto> lidas = [];
        foreach (JsonNode? item in array)
        {
            if (item is not JsonObject exigencia
                || !TentarTexto(exigencia, "tipoDocumentoNome", out string rotulo)
                || !TentarTexto(exigencia, "aplicabilidade", out string aplicabilidade)
                || !TentarBooleano(exigencia, "obrigatorio", out bool obrigatorio)
                || !TentarFormatos(exigencia, out FormatosAceitosCertameDto? formatos))
            {
                return false;
            }

            lidas.Add(new ExigenciaDocumentalCertameDto(rotulo, aplicabilidade, obrigatorio, formatos));
        }

        exigencias = lidas;
        return true;
    }

    /// <summary>
    /// Bicondicional do envelope: lista nula equivale a "qualquer formato". O tamanho máximo por
    /// formato fica fora — é limite de upload, não informação do edital.
    /// </summary>
    private static bool TentarFormatos(JsonObject exigencia, [NotNullWhen(true)] out FormatosAceitosCertameDto? formatos)
    {
        formatos = null;
        if (!TentarObjeto(exigencia, "formatosPermitidos", out JsonObject? bloco)
            || !TentarBooleano(bloco, "qualquer", out bool qualquer)
            || !bloco.TryGetPropertyValue("lista", out JsonNode? listaNode))
        {
            return false;
        }

        // A bicondicional é CONFERIDA, não só documentada: "aceita qualquer formato" com uma lista
        // de formatos ao lado são duas afirmações contraditórias, e repassá-las publicaria a
        // contradição num documento com efeito jurídico — o resto desta classe recusa forma
        // inesperada em vez de repassar, e aqui não é diferente.
        if (listaNode is null)
        {
            if (!qualquer)
            {
                return false;
            }

            formatos = new FormatosAceitosCertameDto(true, null);
            return true;
        }

        // Lista VAZIA é a terceira forma contraditória, e não passa: "não aceita qualquer formato"
        // sem nenhum formato ao lado publicaria uma exigência que nenhum arquivo satisfaz. O
        // emissor nunca a produz — a lista, quando presente, tem ao menos uma entrada.
        if (qualquer || listaNode is not JsonArray array || array.Count == 0)
        {
            return false;
        }

        List<string> lista = [];
        foreach (JsonNode? item in array)
        {
            if (item is not JsonObject entrada || !TentarTexto(entrada, "formato", out string formato))
            {
                return false;
            }

            lista.Add(formato);
        }

        formatos = new FormatosAceitosCertameDto(false, lista);
        return true;
    }

    private static bool TentarAtendimento(JsonObject envelope, [NotNullWhen(true)] out AtendimentoCertameDto? atendimento)
    {
        atendimento = null;
        if (!TentarObjeto(envelope, BlocoPublico("atendimento"), out JsonObject? bloco)
            || !TentarParesNomeados(bloco, "condicoes", "condicaoCodigo", "condicaoNome", out List<CondicaoAtendimentoCertameDto>? condicoes)
            || !TentarParesNomeados(bloco, "tiposDeficiencia", "tipoDeficienciaCodigo", "tipoDeficienciaNome", out List<CondicaoAtendimentoCertameDto>? tipos)
            || !TentarArray(bloco, "recursos", out JsonArray? recursosArray))
        {
            return false;
        }

        List<string> recursos = [];
        foreach (JsonNode? item in recursosArray)
        {
            if (item is not JsonObject recurso || !TentarTexto(recurso, "recursoNome", out string nome))
            {
                return false;
            }

            recursos.Add(nome);
        }

        atendimento = new AtendimentoCertameDto(condicoes, recursos, tipos);
        return true;
    }

    private static bool TentarParesNomeados(
        JsonObject bloco,
        string chaveArray,
        string chaveCodigo,
        string chaveNome,
        [NotNullWhen(true)] out List<CondicaoAtendimentoCertameDto>? pares)
    {
        pares = null;
        if (!TentarArray(bloco, chaveArray, out JsonArray? array))
        {
            return false;
        }

        List<CondicaoAtendimentoCertameDto> lidos = [];
        foreach (JsonNode? item in array)
        {
            if (item is not JsonObject entrada
                || !TentarTexto(entrada, chaveCodigo, out string codigo)
                || !TentarTexto(entrada, chaveNome, out string nome))
            {
                return false;
            }

            lidos.Add(new CondicaoAtendimentoCertameDto(codigo, nome));
        }

        pares = lidos;
        return true;
    }

    /// <summary>
    /// Bloco de presença explícita: <c>presente: false</c> significa publicação sem taxa
    /// configurada, e vira ausência do bloco na resposta pública — não um objeto com campos nulos,
    /// que o candidato leria como "taxa de valor desconhecido".
    /// </summary>
    private static bool TentarTaxaInscricao(JsonObject envelope, out TaxaInscricaoCertameDto? taxa)
    {
        taxa = null;
        if (!TentarObjeto(envelope, BlocoPublico("taxaInscricao"), out JsonObject? bloco)
            || !TentarBooleano(bloco, "presente", out bool presente))
        {
            return false;
        }

        if (!presente)
        {
            return true;
        }

        if (!TentarBooleano(bloco, "cobra", out bool cobra)
            || !TentarTextoOpcional(bloco, "valor", out string? valor)
            || !TentarTextos(bloco, "fundamentos", out List<string>? fundamentos))
        {
            return false;
        }

        taxa = new TaxaInscricaoCertameDto(cobra, valor, fundamentos);
        return true;
    }

    /// <summary>
    /// Bloco condicional: ausente na publicação original, presente quando o edital foi emendado.
    /// Ausência é sucesso com <see langword="null"/>; presença malformada é falha.
    /// </summary>
    private static bool TentarRetificacao(JsonObject envelope, out RetificacaoCertameDto? retificacao)
    {
        retificacao = null;
        if (!envelope.TryGetPropertyValue(BlocoPublico("retificacao"), out JsonNode? node) || node is null)
        {
            return true;
        }

        if (node is not JsonObject bloco
            || !TentarIdentificador(bloco, "editalRetificadoId", out Guid atoRetificadoId)
            || !TentarTexto(bloco, "motivo", out string motivo))
        {
            return false;
        }

        retificacao = new RetificacaoCertameDto(atoRetificadoId, motivo);
        return true;
    }

    private static bool TentarIdentificadores(JsonObject objeto, string chave, [NotNullWhen(true)] out List<Guid>? valores)
    {
        valores = null;
        if (!TentarArray(objeto, chave, out JsonArray? array))
        {
            return false;
        }

        List<Guid> lidos = [];
        foreach (JsonNode? item in array)
        {
            if (item is not JsonValue valor
                || !valor.TryGetValue(out string? texto)
                || !Guid.TryParse(texto, CultureInfo.InvariantCulture, out Guid identificador))
            {
                return false;
            }

            lidos.Add(identificador);
        }

        valores = lidos;
        return true;
    }

    internal static bool TentarTextos(JsonObject objeto, string chave, [NotNullWhen(true)] out List<string>? valores)
    {
        valores = null;
        if (!TentarArray(objeto, chave, out JsonArray? array))
        {
            return false;
        }

        List<string> lidos = [];
        foreach (JsonNode? item in array)
        {
            if (item is not JsonValue valor || !valor.TryGetValue(out string? texto))
            {
                return false;
            }

            lidos.Add(texto);
        }

        valores = lidos;
        return true;
    }

    internal static bool TentarArray(JsonObject? objeto, string chave, [NotNullWhen(true)] out JsonArray? valor)
    {
        valor = null;
        if (objeto is null || !objeto.TryGetPropertyValue(chave, out JsonNode? node) || node is not JsonArray array)
        {
            return false;
        }

        valor = array;
        return true;
    }

    internal static bool TentarObjeto(JsonObject? objeto, string chave, [NotNullWhen(true)] out JsonObject? valor)
    {
        valor = null;
        if (objeto is null || !objeto.TryGetPropertyValue(chave, out JsonNode? node) || node is not JsonObject encontrado)
        {
            return false;
        }

        valor = encontrado;
        return true;
    }

    internal static bool TentarTexto(JsonObject? objeto, string chave, out string valor)
    {
        valor = "";
        return objeto is not null
            && objeto.TryGetPropertyValue(chave, out JsonNode? node)
            && node is JsonValue jv
            && jv.TryGetValue(out valor!);
    }

    /// <summary>
    /// Chave presente com <c>null</c> explícito ou com texto: sucesso. Chave ausente ou de outro
    /// tipo: falha — a ausência da chave é forma inesperada, não campo opcional vazio.
    /// </summary>
    internal static bool TentarTextoOpcional(JsonObject? objeto, string chave, out string? valor)
    {
        valor = null;
        if (objeto is null || !objeto.TryGetPropertyValue(chave, out JsonNode? node))
        {
            return false;
        }

        return node is null || (node is JsonValue jv && jv.TryGetValue(out valor));
    }

    private static bool TentarBooleano(JsonObject? objeto, string chave, out bool valor)
    {
        valor = false;
        return objeto is not null
            && objeto.TryGetPropertyValue(chave, out JsonNode? node)
            && node is JsonValue jv
            && jv.TryGetValue(out valor);
    }

    internal static bool TentarInteiro(JsonObject? objeto, string chave, out int valor)
    {
        valor = 0;
        return objeto is not null
            && objeto.TryGetPropertyValue(chave, out JsonNode? node)
            && node is JsonValue jv
            && jv.TryGetValue(out valor);
    }

    private static bool TentarInteiroOpcional(JsonObject? objeto, string chave, out int? valor)
    {
        valor = null;
        if (objeto is null || !objeto.TryGetPropertyValue(chave, out JsonNode? node))
        {
            return false;
        }

        if (node is null)
        {
            return true;
        }

        if (node is JsonValue jv && jv.TryGetValue(out int lido))
        {
            valor = lido;
            return true;
        }

        return false;
    }

    private static bool TentarIdentificador(JsonObject? objeto, string chave, out Guid valor)
    {
        valor = Guid.Empty;
        return TentarTexto(objeto, chave, out string texto)
            && Guid.TryParse(texto, CultureInfo.InvariantCulture, out valor);
    }

    /// <summary>
    /// A forma exata em que o envelope congela um instante: RFC 3339, UTC, granularidade de
    /// segundo, sem fração.
    /// </summary>
    private const string FormatoInstanteCanonico = "yyyy-MM-ddTHH:mm:ssZ";

    /// <summary>Instante congelado, exigido na forma canônica — nunca em outra.</summary>
    /// <remarks>
    /// Exato, e assumindo UTC, pelos dois lados da mesma moeda. Um texto sem designador de fuso
    /// recebe, numa leitura permissiva, o fuso LOCAL do processo que lê: o mesmo envelope
    /// responderia instantes diferentes conforme a máquina que serve a requisição. E aceitar
    /// variações que o emissor nunca produz (fração de segundo, deslocamento explícito) tornaria
    /// a projeção mais leniente que o decodificador do envelope, que recusa qualquer forma fora
    /// da canônica — divergência que esconde adulteração em vez de expô-la.
    /// </remarks>
    internal static bool TentarInstante(JsonObject? objeto, string chave, out DateTimeOffset valor)
    {
        valor = default;
        return TentarTexto(objeto, chave, out string texto) && TentarInstanteCanonico(texto, out valor);
    }

    private static bool TentarInstanteOpcional(JsonObject? objeto, string chave, out DateTimeOffset? valor)
    {
        valor = null;
        if (objeto is null || !objeto.TryGetPropertyValue(chave, out JsonNode? node))
        {
            return false;
        }

        if (node is null)
        {
            return true;
        }

        if (node is JsonValue jv
            && jv.TryGetValue(out string? texto)
            && TentarInstanteCanonico(texto, out DateTimeOffset lido))
        {
            valor = lido;
            return true;
        }

        return false;
    }

    private static bool TentarInstanteCanonico(string texto, out DateTimeOffset valor) =>
        DateTimeOffset.TryParseExact(
            texto,
            FormatoInstanteCanonico,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out valor);
}

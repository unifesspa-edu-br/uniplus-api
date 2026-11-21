namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Collections.Frozen;
using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Serializacao;

/// <summary>
/// O conjunto básico de dados do candidato que todo formulário de inscrição coleta, na seção
/// reservada <see cref="CodigoDaSecao"/>, a primeira do formulário: a identificação, a filiação, os
/// contatos e a residência — o tipo de localidade antes do endereço —, com a exibição e a
/// obrigatoriedade de cada item.
/// </summary>
/// <remarks>
/// A seção e os itens estão na forma de entrada, para passarem pela mesma escrita dos demais itens
/// e herdarem do catálogo o formato e a origem dos valores. O que foi gravado num formulário ou
/// num modelo é a referência dele; a constante só vale onde ainda não há seção.
/// </remarks>
public static class ConjuntoBasicoDaInscricao
{
    public const string CodigoDaSecao = "DADOS_BASICOS";

    /// <summary>O CPF do candidato, obrigatório de todos, inclusive do estrangeiro, e que tem de ser o do dono da inscrição.</summary>
    public const string Cpf = "CPF";

    /// <summary>O nome civil do candidato, que tem de ser o do login.</summary>
    public const string Nome = "NOME";

    /// <summary>Se o candidato deseja usar nome social.</summary>
    public const string DesejaNomeSocial = "DESEJA_NOME_SOCIAL";

    /// <summary>O nome social do candidato, que tem de ser o da conta.</summary>
    public const string NomeSocial = "NOME_SOCIAL";

    /// <summary>
    /// O formato com que a resposta do fato é conferida. O nome civil vem do login e se confere pela
    /// igualdade com ele, não por formato: o formato congelado num formulário publicado não o alcança.
    /// </summary>
    public static string? FormatoDaResposta(string fatoCodigo, string? formato) =>
        string.Equals(fatoCodigo, Nome, StringComparison.Ordinal) ? null : formato;

    /// <summary>
    /// A ajuda com que o campo é publicado. O nome social vem da conta em toda inscrição, inclusive na
    /// de formulário já gravado ou publicado sem essa orientação: a ajuda acompanha a regra, não a
    /// versão do formulário. A regra é da inscrição; fora dela, vale a ajuda gravada.
    /// </summary>
    public static string? AjudaDoCampo(FinalidadeFormulario finalidade, string fatoCodigo, string? ajuda) =>
        finalidade == FinalidadeFormulario.Inscricao && string.Equals(fatoCodigo, NomeSocial, StringComparison.Ordinal)
            ? AjudaDoNomeSocial
            : ajuda;

    private const string Nacionalidade = "NACIONALIDADE";
    private const string Estrangeiro = "ESTRANGEIRO";
    private const string TipoDeLocalidade = "TIPO_ENDERECO";
    private const string OrgaoEmissor = "RG_ORGAO_EMISSOR";

    /// <summary>O órgão emissor fora da lista, cujo nome o candidato escreve.</summary>
    private const string OutroOrgaoEmissor = "OUTRO";

    private const string AjudaDoNomeSocial =
        "Vem da sua conta e não pode ser alterado aqui. Para cadastrar ou alterar, salve a inscrição, acesse Meu perfil no menu da sua conta e depois volte para concluir.";

    private const int TamanhoMaximoDoNomeDoOrgaoEmissor = 60;

    /// <summary>
    /// Quantos anos antes do envio a data de nascimento e a de emissão do RG ainda são plausíveis: o
    /// limite de sanidade acima da maior longevidade registrada.
    /// </summary>
    private const int AnosDePlausibilidadeDaData = 120;

    /// <summary>Os tipos de localidade em que se pede o nome da aldeia, da comunidade ou do quilombo.</summary>
    private static readonly string[] LocalidadesComComunidade = ["ALDEIA", "COMUNIDADE", "QUILOMBO"];

    private static readonly string CampoDeSelecao = TipoRenderizacao.SelecaoUnica.Codigo();
    private static readonly string CampoDeTexto = TipoRenderizacao.Texto.Codigo();
    private static readonly string CampoDeData = TipoRenderizacao.Data.Codigo();
    private static readonly string Sempre = PredicadoDnfJson.ObrigatoriedadeSempre;
    private static readonly IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> QuandoNaoEstrangeiro = Quando(Nacionalidade, Operador.Diferente.Codigo(), Estrangeiro);
    private static readonly IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> QuandoEstrangeiro = Quando(Nacionalidade, Operador.Igual.Codigo(), Estrangeiro);

    private static readonly IReadOnlyList<RestricaoValorInput> DataPlausivel =
    [
        new(RestricaoValorJson.FaixaDeData,
            DataMinima: new LimiteDeDataInput(RestricaoValorJson.AncoraEnvio, Anos: -AnosDePlausibilidadeDaData),
            DataMaxima: new LimiteDeDataInput(RestricaoValorJson.AncoraEnvio)),
    ];

    /// <summary>A seção reservada, a primeira etapa do formulário de inscrição.</summary>
    public static EtapaFormularioInput Secao { get; } =
        new(CodigoDaSecao, 0, EstruturaFormulario.TipoSecao, null, new ApresentacaoDaEtapaInput("Dados do candidato", null, null));

    /// <summary>Os itens da seção, na ordem em que são coletados.</summary>
    public static IReadOnlyList<FatoColetadoInput> Itens { get; } = MontarItens();

    /// <summary>Os códigos dos fatos do conjunto básico.</summary>
    public static IReadOnlySet<string> Fatos { get; } = Itens.Select(static i => i.FatoCodigo).ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// A referência dos itens básicos: o gravado na seção reservada de um formulário ou de um
    /// modelo, normalizado, e o do conjunto básico onde ele ainda não foi gravado. O item básico
    /// gravado fora da seção não é referência: o envio que o repete é recusado como alteração.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A normalização é pura: a ordem segue sempre a sequência do conjunto básico, renumerada a partir
    /// da menor ordem gravada, e o rótulo vem do conjunto; o restante do item gravado é preservado.
    /// Assim, o item gravado antes de uma mudança na sequência ou nos rótulos do conjunto volta a ela.
    /// </para>
    /// <para>
    /// O item básico que falta numa seção já gravada — por ter entrado no conjunto depois — ocupa a
    /// posição que o conjunto lhe dá: o que vem depois da seção desloca-se em
    /// <see cref="MesclarItens"/>. Sem nenhum item básico gravado, o conjunto básico vale como é.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<FatoColetadoInput> Referencia(IEnumerable<FatoColetadoInput> gravados)
    {
        ArgumentNullException.ThrowIfNull(gravados);
        Dictionary<string, FatoColetadoInput> porCodigo = BasicosGravadosNaSecao(gravados);
        if (porCodigo.Count == 0)
        {
            return Itens;
        }

        int inicio = porCodigo.Values.Min(static g => g.Ordem);
        return [.. Itens.Select((canonico, posicao) =>
            ComOrdemERotuloDoConjunto(porCodigo.GetValueOrDefault(canonico.FatoCodigo) ?? canonico, canonico, inicio + posicao))];
    }

    private static Dictionary<string, FatoColetadoInput> BasicosGravadosNaSecao(IEnumerable<FatoColetadoInput> gravados) =>
        gravados
            .Where(static g => g is not null && Fatos.Contains(g.FatoCodigo) && EhDaSecao(g.EtapaCodigo))
            .DistinctBy(static g => g.FatoCodigo, StringComparer.Ordinal)
            .ToDictionary(static g => g.FatoCodigo, StringComparer.Ordinal);

    private static FatoColetadoInput ComOrdemERotuloDoConjunto(FatoColetadoInput gravado, FatoColetadoInput canonico, int ordem) =>
        gravado with { Ordem = ordem, Apresentacao = ComRotuloDe(gravado.Apresentacao, canonico.Apresentacao) };

    private static ApresentacaoDoCampoInput? ComRotuloDe(ApresentacaoDoCampoInput? gravada, ApresentacaoDoCampoInput? canonica) =>
        (gravada ?? canonica) is { } apresentacao ? apresentacao with { Rotulo = canonica?.Rotulo } : null;

    /// <summary>A seção gravada num formulário ou num modelo, ou a do conjunto básico.</summary>
    public static EtapaFormularioInput SecaoDe(IEnumerable<EtapaFormularioInput> gravadas)
    {
        ArgumentNullException.ThrowIfNull(gravadas);
        return gravadas.FirstOrDefault(static e => e is not null && EhDaSecao(e.Codigo)) ?? Secao;
    }

    /// <summary>
    /// Mescla os itens enviados com os da seção de <paramref name="referencia"/>: os itens básicos
    /// entram pela referência, e os itens e grupos do cliente sobem acima da seção só quando alguma
    /// ordem deles colide com a dela. Recusa o item básico enviado que difere da referência,
    /// e o item ou o grupo do cliente na seção reservada.
    /// </summary>
    public static (IReadOnlyList<FatoColetadoInput> Itens, IReadOnlyList<GrupoColetadoInput> Grupos, List<FieldError> Erros) MesclarItens(
        IReadOnlyList<FatoColetadoInput> enviados, IReadOnlyList<GrupoColetadoInput> grupos, IReadOnlyList<FatoColetadoInput> referencia)
    {
        ArgumentNullException.ThrowIfNull(enviados);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(referencia);

        int deslocamento = DeslocamentoDoQueVemDepoisDaSecao(enviados, grupos, referencia);
        List<FieldError> erros = [.. ConferirBasicosEnviados(enviados, referencia), .. RecusasDoClienteNaSecaoReservada(enviados, grupos)];
        IReadOnlyList<GrupoColetadoInput> gruposMesclados = [.. grupos.Select(g => g is null ? g! : g with { Ordem = g.Ordem + deslocamento })];
        return (ItensMesclados(enviados, referencia, deslocamento), gruposMesclados, erros);
    }

    private static IEnumerable<FieldError> RecusasDoClienteNaSecaoReservada(
        IReadOnlyList<FatoColetadoInput> enviados, IReadOnlyList<GrupoColetadoInput> grupos) =>
    [
        .. enviados.Index().Where(static p => ItemDoClienteNaSecaoReservada(p.Item)).Select(static p => SecaoReservada($"itens[{p.Index}].etapaCodigo")),
        .. grupos.Index().Where(static p => p.Item is { } grupo && EhDaSecao(grupo.EtapaCodigo)).Select(static p => SecaoReservada($"grupos[{p.Index}].etapaCodigo")),
    ];

    private static bool ItemDoClienteNaSecaoReservada(FatoColetadoInput? item) =>
        item is not null && !Fatos.Contains(item.FatoCodigo) && EhDaSecao(item.EtapaCodigo);

    /// <summary>
    /// O que vem depois da seção desloca-se só o bastante para abrir lugar ao último item dela: dos
    /// itens e grupos do cliente, o primeiro passa a ser o seguinte ao último básico.
    /// </summary>
    private static int DeslocamentoDoQueVemDepoisDaSecao(
        IReadOnlyList<FatoColetadoInput> enviados, IReadOnlyList<GrupoColetadoInput> grupos, IReadOnlyList<FatoColetadoInput> referencia)
    {
        int teto = referencia.Count == 0 ? -1 : referencia.Max(static r => r.Ordem);
        int primeiro = enviados.Where(i => i is not null && !Fatos.Contains(i.FatoCodigo)).Select(static i => i.Ordem)
            .Concat(grupos.Where(static g => g is not null).Select(static g => g.Ordem))
            .DefaultIfEmpty(int.MaxValue).Min();
        return primeiro <= teto ? teto + 1 - primeiro : 0;
    }

    /// <summary>
    /// O item básico enviado vale pelo da referência, que já tem a ordem e o rótulo do conjunto; o
    /// omitido entra por ela.
    /// </summary>
    private static IReadOnlyList<FatoColetadoInput> ItensMesclados(
        IReadOnlyList<FatoColetadoInput> enviados, IReadOnlyList<FatoColetadoInput> referencia, int deslocamento)
    {
        Dictionary<string, FatoColetadoInput> basicos = referencia.ToDictionary(static r => r.FatoCodigo, StringComparer.Ordinal);
        HashSet<string> enviadosBasicos = new(enviados.Where(static i => i is not null && Fatos.Contains(i.FatoCodigo)).Select(static i => i.FatoCodigo), StringComparer.Ordinal);
        return
        [
            .. enviados.Select(i => ItemMesclado(i, basicos, deslocamento)),
            .. referencia.Where(r => !enviadosBasicos.Contains(r.FatoCodigo)),
        ];
    }

    private static FatoColetadoInput ItemMesclado(FatoColetadoInput? enviado, Dictionary<string, FatoColetadoInput> basicos, int deslocamento)
    {
        if (enviado is null)
        {
            return enviado!;
        }

        return basicos.GetValueOrDefault(enviado.FatoCodigo) ?? (Fatos.Contains(enviado.FatoCodigo) ? enviado : enviado with { Ordem = enviado.Ordem + deslocamento });
    }

    /// <summary>
    /// Mescla as etapas enviadas com a seção de <paramref name="referencia"/>: a seção que falta
    /// entra, e as etapas do cliente sobem uma posição só quando alguma colide com a ordem dela. A
    /// seção enviada é igual à referência.
    /// </summary>
    public static (IReadOnlyList<EtapaFormularioInput> Etapas, List<FieldError> Erros) MesclarEtapas(
        IReadOnlyList<EtapaFormularioInput> enviadas, EtapaFormularioInput referencia)
    {
        ArgumentNullException.ThrowIfNull(enviadas);
        ArgumentNullException.ThrowIfNull(referencia);

        int indice = enviadas.ToList().FindIndex(static e => e is not null && EhDaSecao(e.Codigo));
        return indice >= 0
            ? (enviadas, RecusasDaSecaoEnviada(enviadas[indice], indice, referencia))
            : (EtapasComASecao(enviadas, referencia), []);
    }

    private static List<FieldError> RecusasDaSecaoEnviada(EtapaFormularioInput enviada, int indice, EtapaFormularioInput referencia) =>
        MesmaSecao(enviada, referencia)
            ? []
            :
            [
                new($"etapas[{indice}]", new DomainError(
                    EstruturaFormularioErrorCodes.SecaoReservadaAlterada,
                    $"A seção {CodigoDaSecao} é a dos dados básicos do candidato e não pode ser alterada.")),
            ];

    private static IReadOnlyList<EtapaFormularioInput> EtapasComASecao(IReadOnlyList<EtapaFormularioInput> enviadas, EtapaFormularioInput referencia)
    {
        bool colide = enviadas.Any(e => e is not null && e.Ordem <= referencia.Ordem);
        return [.. enviadas.Select(e => colide && e is not null ? e with { Ordem = e.Ordem + 1 } : e!), referencia];
    }

    /// <summary>
    /// Recusa o item básico enviado que difere do de <paramref name="referencia"/>: o conjunto
    /// básico não é alterado pela escrita, só repetido ou omitido. A ordem e o rótulo são do
    /// conjunto, não do cliente: o item gravado antes de uma mudança neles continua repetível.
    /// </summary>
    private static List<FieldError> ConferirBasicosEnviados(IReadOnlyList<FatoColetadoInput> enviados, IReadOnlyList<FatoColetadoInput> referencia)
    {
        Dictionary<string, string> canonicos = referencia.ToDictionary(static r => r.FatoCodigo, static r => FormaCanonica(SemOrdemNemRotulo(r)), StringComparer.Ordinal);
        return [.. enviados.Index()
            .Where(p => p.Item is { } item && Fatos.Contains(item.FatoCodigo) && !RepeteOCanonico(item, canonicos))
            .Select(static p => DadoBasicoAlterado(p.Index, p.Item!.FatoCodigo))];
    }

    private static bool RepeteOCanonico(FatoColetadoInput item, Dictionary<string, string> canonicos) =>
        canonicos.TryGetValue(item.FatoCodigo, out string? canonico)
        && !TemValorAusente(item)
        && string.Equals(canonico, FormaCanonica(SemOrdemNemRotulo(item)), StringComparison.Ordinal);

    private static FieldError DadoBasicoAlterado(int indice, string fatoCodigo) =>
        new($"itens[{indice}]", new DomainError(
            EstruturaFormularioErrorCodes.DadoBasicoAlterado,
            $"O item '{fatoCodigo}' é do conjunto básico de dados do candidato: pode ser repetido como está, ou omitido, mas não alterado."));

    private static FatoColetadoInput SemOrdemNemRotulo(FatoColetadoInput item) =>
        item with { Ordem = 0, Apresentacao = item.Apresentacao is null ? null : item.Apresentacao with { Rotulo = string.Empty } };

    /// <summary>
    /// A forma canônica do item na entrada, para conferir que o item básico enviado é o de
    /// referência: os textos aparados, as listas vazias como nulas e o token da obrigatoriedade.
    /// </summary>
    public static string FormaCanonica(FatoColetadoInput item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return JsonSerializer.Serialize(item with
        {
            EtapaCodigo = string.IsNullOrWhiteSpace(item.EtapaCodigo) ? null : item.EtapaCodigo.Trim(),
            Apresentacao = ApresentacaoCanonica(item.Apresentacao),
            Regras = RegrasCanonicas(item.Regras),
        });
    }

    private static ApresentacaoDoCampoInput? ApresentacaoCanonica(ApresentacaoDoCampoInput? apresentacao) =>
        apresentacao is null
            ? null
            : apresentacao with
            {
                Rotulo = apresentacao.Rotulo?.Trim() ?? string.Empty,
                Ajuda = string.IsNullOrWhiteSpace(apresentacao.Ajuda) ? null : apresentacao.Ajuda.Trim(),
            };

    private static RegrasDoCampoInput RegrasCanonicas(RegrasDoCampoInput? regras) => new(
        regras?.Exibicao is { Count: > 0 } exibicao ? exibicao : null,
        ObrigatoriedadeCanonica(regras?.Obrigatoriedade),
        regras?.Restricoes is { Count: > 0 } restricoes ? [.. restricoes.Select(static r => RestricaoCanonica(r)!)] : null,
        regras?.Impedimento);

    private static ObrigatoriedadeInput ObrigatoriedadeCanonica(ObrigatoriedadeInput? obrigatoriedade) => new(
        obrigatoriedade?.Tipo is { } tipo && !string.IsNullOrWhiteSpace(tipo) ? tipo.Trim() : Sempre,
        obrigatoriedade?.Predicado is { Count: > 0 } predicado ? predicado : null);

    /// <summary>
    /// A restrição na forma que a escrita grava: lida pelo domínio e devolvida à entrada, para que o
    /// deslocamento omitido de um limite de data e o explícito em zero sejam a mesma restrição. A
    /// restrição ausente ou que o domínio recusa fica como veio, e a recusa sai na própria escrita.
    /// </summary>
    private static RestricaoValorInput? RestricaoCanonica(RestricaoValorInput? restricao) =>
        restricao is not null && EntradaDeRegras.Restricao(restricao) is { IsSuccess: true } lida
            ? EntradaDeRegras.ParaEntrada(lida.Value!)
            : restricao;

    /// <summary>A condição sem valor não tem forma canônica, e o item que a traz não é o de referência.</summary>
    private static bool TemValorAusente(FatoColetadoInput item) =>
        (item.Regras?.Predicados() ?? [])
            .SelectMany(static predicado => predicado ?? [])
            .SelectMany(static clausula => clausula ?? [])
            .Any(static condicao => condicao is not null && condicao.Valor.ValueKind == JsonValueKind.Undefined);

    private static bool EhDaSecao(string? etapaCodigo) => string.Equals(etapaCodigo?.Trim(), CodigoDaSecao, StringComparison.Ordinal);

    private static bool MesmaSecao(EtapaFormularioInput enviada, EtapaFormularioInput referencia) =>
        enviada.Ordem == referencia.Ordem
        && string.Equals(enviada.Tipo, referencia.Tipo, StringComparison.Ordinal)
        && string.IsNullOrWhiteSpace(enviada.Bloco)
        && MesmaApresentacao(enviada.Apresentacao, referencia.Apresentacao)
        && enviada.Exibicao is not { Count: > 0 };

    private static bool MesmaApresentacao(ApresentacaoDaEtapaInput? enviada, ApresentacaoDaEtapaInput? referencia) =>
        string.Equals(enviada?.Titulo?.Trim(), referencia?.Titulo, StringComparison.Ordinal)
        && string.Equals(FormaDoItem.TextoOpcional(enviada?.Descricao), referencia?.Descricao, StringComparison.Ordinal)
        && string.Equals(FormaDoItem.TextoOpcional(enviada?.Aviso), referencia?.Aviso, StringComparison.Ordinal);

    private static FieldError SecaoReservada(string campo) =>
        new(campo, new DomainError(
            EstruturaFormularioErrorCodes.SecaoReservada,
            $"A seção {CodigoDaSecao} é reservada aos dados básicos do candidato."));

    private static List<FatoColetadoInput> MontarItens()
    {
        List<FatoColetadoInput> itens =
        [
            .. Identificacao(), .. DocumentoDeIdentidade(), .. Nascimento(), .. Filiacao(), .. Caracterizacao(), .. ContatoEResidencia(),
        ];

        return [.. itens.Select(static (item, ordem) => item with { Ordem = ordem, EtapaCodigo = CodigoDaSecao })];
    }

    private static FatoColetadoInput[] Identificacao() =>
    [
        Item(Nome, "Nome completo", CampoDeTexto),
        Item(DesejaNomeSocial, "Deseja usar nome social?", TipoRenderizacao.Booleano.Codigo()),
        Item(NomeSocial, "Nome social", CampoDeTexto, Quando(DesejaNomeSocial, Operador.Igual.Codigo(), true), ajuda: AjudaDoNomeSocial),
        Item(Nacionalidade, "Nacionalidade", CampoDeSelecao),
        Item(Cpf, "CPF", CampoDeTexto),
    ];

    private static FatoColetadoInput[] DocumentoDeIdentidade() =>
    [
        Item("RG_NUMERO", "Número do RG", CampoDeTexto, QuandoNaoEstrangeiro),
        Item(OrgaoEmissor, "Órgão emissor do RG", CampoDeSelecao, QuandoNaoEstrangeiro),
        Item("RG_ORGAO_EMISSOR_OUTRO", "Nome do órgão emissor", CampoDeTexto, Quando(OrgaoEmissor, Operador.Igual.Codigo(), OutroOrgaoEmissor),
            [new RestricaoValorInput(RestricaoValorJson.TamanhoTexto, Maximo: TamanhoMaximoDoNomeDoOrgaoEmissor)]),
        Item("RG_UF", "UF de emissão do RG", CampoDeSelecao, QuandoNaoEstrangeiro),
        Item("RG_DATA_EMISSAO", "Data de emissão do RG", CampoDeData, QuandoNaoEstrangeiro, DataPlausivel),
        Item("PASSAPORTE_NUMERO", "Número do passaporte", CampoDeTexto, QuandoEstrangeiro),
        Item("RNM_NUMERO", "Número do Registro Nacional Migratório (RNM)", CampoDeTexto, QuandoEstrangeiro),
    ];

    private static FatoColetadoInput[] Nascimento() =>
    [
        Item("DATA_NASCIMENTO", "Data de nascimento", CampoDeData, restricoes: DataPlausivel),
        Item("NATURALIDADE_UF", "UF de nascimento", CampoDeSelecao, QuandoNaoEstrangeiro),
        Item("NATURALIDADE_MUNICIPIO", "Município de nascimento", TipoRenderizacao.Municipio.Codigo(), QuandoNaoEstrangeiro,
            [new RestricaoValorInput(RestricaoValorJson.MunicipiosDaUf, Fatos: ["NATURALIDADE_UF"])]),
    ];

    private static FatoColetadoInput[] Filiacao() =>
    [
        Item("NOME_MAE", "Nome da mãe", CampoDeTexto),
        Item("NOME_PAI", "Nome do pai", CampoDeTexto, obrigatoriedade: PredicadoDnfJson.ObrigatoriedadeNunca),
    ];

    private static FatoColetadoInput[] Caracterizacao() =>
    [
        Item("ESTADO_CIVIL", "Estado civil", CampoDeSelecao),
        Item("SEXO", "Sexo biológico", CampoDeSelecao),
        Item("SEXO_CIVIL", "Sexo civil", CampoDeSelecao),
        Item("IDENTIDADE_GENERO", "Identidade de gênero", CampoDeSelecao),
        Item("ORIENTACAO_SEXUAL", "Orientação sexual", CampoDeSelecao),
        Item("COR_RACA", "Cor ou raça", CampoDeSelecao),
    ];

    private static FatoColetadoInput[] ContatoEResidencia() =>
    [
        Item("EMAIL", "E-mail", CampoDeTexto),
        Item("TELEFONE", "Telefone", CampoDeTexto),
        Item(TipoDeLocalidade, "Tipo de localidade", CampoDeSelecao),
        Item("NOME_COMUNIDADE", "Nome da aldeia, comunidade ou quilombo", CampoDeTexto, Quando(TipoDeLocalidade, Operador.Em.Codigo(), LocalidadesComComunidade)),
        Item("ENDERECO_RESIDENCIAL", "Endereço residencial", TipoRenderizacao.Endereco.Codigo()),
    ];

    /// <summary>O item na forma de entrada: sempre obrigatório, salvo a obrigatoriedade dada, e sem ordem nem seção, que o conjunto dá depois.</summary>
    private static FatoColetadoInput Item(
        string fatoCodigo,
        string rotulo,
        string tipoRenderizacao,
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? exibicao = null,
        IReadOnlyList<RestricaoValorInput>? restricoes = null,
        string? ajuda = null,
        string? obrigatoriedade = null) =>
        new(
            fatoCodigo,
            0,
            new ApresentacaoDoCampoInput(rotulo, tipoRenderizacao, ajuda),
            new RegrasDoCampoInput(exibicao, new ObrigatoriedadeInput(obrigatoriedade ?? Sempre), restricoes));

    private static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> Quando(string fato, string operador, object valor) =>
        [[new CondicaoPrecondicaoInput(fato, operador, JsonSerializer.SerializeToElement(valor))]];
}

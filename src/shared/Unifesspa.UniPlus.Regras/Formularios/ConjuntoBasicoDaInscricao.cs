namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Collections.Frozen;
using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Serializacao;

/// <summary>
/// O conjunto básico de dados do candidato que todo formulário de inscrição coleta, na seção
/// reservada <see cref="CodigoDaSecao"/>, a primeira do formulário: a identificação, a filiação e os
/// contatos, com a exibição e a obrigatoriedade de cada item.
/// </summary>
/// <remarks>
/// A seção e os itens estão na forma de entrada, para passarem pela mesma escrita dos demais itens
/// e herdarem do catálogo o formato e a origem dos valores. O que foi gravado num formulário ou
/// num modelo é a referência dele; a constante só vale onde ainda não há seção.
/// </remarks>
public static class ConjuntoBasicoDaInscricao
{
    public const string CodigoDaSecao = "DADOS_BASICOS";

    private const string Nacionalidade = "NACIONALIDADE";
    private const string Estrangeiro = "ESTRANGEIRO";
    private const string DesejaNomeSocial = "DESEJA_NOME_SOCIAL";

    /// <summary>A seção reservada, a primeira etapa do formulário de inscrição.</summary>
    public static EtapaFormularioInput Secao { get; } =
        new(CodigoDaSecao, 0, EstruturaFormulario.TipoSecao, null, "Dados do candidato", null, null);

    /// <summary>Os itens da seção, na ordem em que são coletados.</summary>
    public static IReadOnlyList<FatoColetadoInput> Itens { get; } = MontarItens();

    /// <summary>Os códigos dos fatos do conjunto básico.</summary>
    public static IReadOnlySet<string> Fatos { get; } = Itens.Select(static i => i.FatoCodigo).ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// A referência dos itens básicos: o gravado na seção reservada de um formulário ou de um
    /// modelo, e o do conjunto básico onde ele ainda não foi gravado. O item básico gravado fora da
    /// seção não é referência: o envio que o repete é recusado como alteração.
    /// </summary>
    public static IReadOnlyList<FatoColetadoInput> Referencia(IEnumerable<FatoColetadoInput> gravados)
    {
        ArgumentNullException.ThrowIfNull(gravados);
        Dictionary<string, FatoColetadoInput> porCodigo = gravados
            .Where(static g => g is not null && Fatos.Contains(g.FatoCodigo) && EhDaSecao(g.EtapaCodigo))
            .DistinctBy(static g => g.FatoCodigo, StringComparer.Ordinal)
            .ToDictionary(static g => g.FatoCodigo, StringComparer.Ordinal);
        return [.. Itens.Select(i => porCodigo.GetValueOrDefault(i.FatoCodigo) ?? i)];
    }

    /// <summary>A seção gravada num formulário ou num modelo, ou a do conjunto básico.</summary>
    public static EtapaFormularioInput SecaoDe(IEnumerable<EtapaFormularioInput> gravadas)
    {
        ArgumentNullException.ThrowIfNull(gravadas);
        return gravadas.FirstOrDefault(static e => e is not null && EhDaSecao(e.Codigo)) ?? Secao;
    }

    /// <summary>
    /// Mescla os itens enviados com os da seção de <paramref name="referencia"/>: o item básico que
    /// falta entra no fim da lista, e os itens e grupos do cliente sobem acima da seção só quando
    /// alguma ordem deles colide com a dela. Recusa o item básico enviado que difere da referência,
    /// e o item ou o grupo do cliente na seção reservada.
    /// </summary>
    public static (IReadOnlyList<FatoColetadoInput> Itens, IReadOnlyList<GrupoColetadoInput> Grupos, List<FieldError> Erros) MesclarItens(
        IReadOnlyList<FatoColetadoInput> enviados, IReadOnlyList<GrupoColetadoInput> grupos, IReadOnlyList<FatoColetadoInput> referencia)
    {
        ArgumentNullException.ThrowIfNull(enviados);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(referencia);

        List<FieldError> erros = ConferirBasicosEnviados(enviados, referencia);
        for (int i = 0; i < enviados.Count; i++)
        {
            if (enviados[i] is { } item && !Fatos.Contains(item.FatoCodigo) && EhDaSecao(item.EtapaCodigo))
            {
                erros.Add(SecaoReservada($"itens[{i}].etapaCodigo"));
            }
        }

        for (int i = 0; i < grupos.Count; i++)
        {
            if (grupos[i] is { } grupo && EhDaSecao(grupo.EtapaCodigo))
            {
                erros.Add(SecaoReservada($"grupos[{i}].etapaCodigo"));
            }
        }

        int teto = referencia.Count == 0 ? -1 : referencia.Max(static r => r.Ordem);
        bool colide = enviados.Any(i => i is not null && !Fatos.Contains(i.FatoCodigo) && i.Ordem <= teto)
            || grupos.Any(g => g is not null && g.Ordem <= teto);
        int deslocamento = colide ? teto + 1 : 0;

        HashSet<string> enviadosBasicos = new(enviados.Where(static i => i is not null && Fatos.Contains(i.FatoCodigo)).Select(static i => i.FatoCodigo), StringComparer.Ordinal);
        IReadOnlyList<FatoColetadoInput> itens =
        [
            .. enviados.Select(i => i is null || Fatos.Contains(i.FatoCodigo) ? i! : i with { Ordem = i.Ordem + deslocamento }),
            .. referencia.Where(r => !enviadosBasicos.Contains(r.FatoCodigo)),
        ];
        IReadOnlyList<GrupoColetadoInput> gruposMesclados = [.. grupos.Select(g => g is null ? g! : g with { Ordem = g.Ordem + deslocamento })];
        return (itens, gruposMesclados, erros);
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

        List<FieldError> erros = [];
        int indice = enviadas.ToList().FindIndex(static e => e is not null && EhDaSecao(e.Codigo));
        if (indice >= 0)
        {
            if (!MesmaSecao(enviadas[indice], referencia))
            {
                erros.Add(new($"etapas[{indice}]", new DomainError(
                    EstruturaFormularioErrorCodes.SecaoReservadaAlterada,
                    $"A seção {CodigoDaSecao} é a dos dados básicos do candidato e não pode ser alterada.")));
            }

            return (enviadas, erros);
        }

        bool colide = enviadas.Any(e => e is not null && e.Ordem <= referencia.Ordem);
        return ([.. enviadas.Select(e => colide && e is not null ? e with { Ordem = e.Ordem + 1 } : e!), referencia], erros);
    }

    /// <summary>
    /// Recusa o item básico enviado que difere do de <paramref name="referencia"/>: o conjunto
    /// básico não é alterado pela escrita, só repetido ou omitido.
    /// </summary>
    private static List<FieldError> ConferirBasicosEnviados(IReadOnlyList<FatoColetadoInput> enviados, IReadOnlyList<FatoColetadoInput> referencia)
    {
        Dictionary<string, string> canonicos = referencia.ToDictionary(static r => r.FatoCodigo, FormaCanonica, StringComparer.Ordinal);
        List<FieldError> erros = [];
        for (int i = 0; i < enviados.Count; i++)
        {
            if (enviados[i] is { } item && Fatos.Contains(item.FatoCodigo)
                && (!canonicos.TryGetValue(item.FatoCodigo, out string? canonico)
                    || TemValorAusente(item)
                    || !string.Equals(canonico, FormaCanonica(item), StringComparison.Ordinal)))
            {
                erros.Add(new($"itens[{i}]", new DomainError(
                    EstruturaFormularioErrorCodes.DadoBasicoAlterado,
                    $"O item '{item.FatoCodigo}' é do conjunto básico de dados do candidato: pode ser repetido como está, ou omitido, mas não alterado.")));
            }
        }

        return erros;
    }

    /// <summary>
    /// A forma canônica do item na entrada, para conferir que o item básico enviado é o de
    /// referência: os textos aparados, as listas vazias como nulas e o token da obrigatoriedade.
    /// </summary>
    public static string FormaCanonica(FatoColetadoInput item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return JsonSerializer.Serialize(item with
        {
            Rotulo = item.Rotulo?.Trim() ?? string.Empty,
            EtapaCodigo = string.IsNullOrWhiteSpace(item.EtapaCodigo) ? null : item.EtapaCodigo.Trim(),
            Obrigatoriedade = string.IsNullOrWhiteSpace(item.Obrigatoriedade) ? PredicadoDnfJson.ObrigatoriedadeSempre : item.Obrigatoriedade.Trim(),
            Precondicao = item.Precondicao is { Count: > 0 } precondicao ? precondicao : null,
            PredicadoObrigatoriedade = item.PredicadoObrigatoriedade is { Count: > 0 } predicado ? predicado : null,
            Ajuda = string.IsNullOrWhiteSpace(item.Ajuda) ? null : item.Ajuda.Trim(),
            Restricoes = item.Restricoes is { Count: > 0 } restricoes ? restricoes : null,
        });
    }

    /// <summary>A condição sem valor não tem forma canônica, e o item que a traz não é o de referência.</summary>
    private static bool TemValorAusente(FatoColetadoInput item) =>
        new[] { item.Precondicao, item.PredicadoObrigatoriedade }
            .Concat((item.Restricoes ?? []).SelectMany(static r => (r?.Entradas ?? []).Select(static e => e?.Quando)))
            .SelectMany(static predicado => predicado ?? [])
            .SelectMany(static clausula => clausula ?? [])
            .Any(static condicao => condicao is not null && condicao.Valor.ValueKind == JsonValueKind.Undefined);

    private static bool EhDaSecao(string? etapaCodigo) => string.Equals(etapaCodigo?.Trim(), CodigoDaSecao, StringComparison.Ordinal);

    private static bool MesmaSecao(EtapaFormularioInput enviada, EtapaFormularioInput referencia) =>
        enviada.Ordem == referencia.Ordem
        && string.Equals(enviada.Tipo, referencia.Tipo, StringComparison.Ordinal)
        && string.IsNullOrWhiteSpace(enviada.Bloco)
        && string.Equals(enviada.Titulo?.Trim(), referencia.Titulo, StringComparison.Ordinal)
        && string.Equals(string.IsNullOrWhiteSpace(enviada.Descricao) ? null : enviada.Descricao.Trim(), referencia.Descricao, StringComparison.Ordinal)
        && string.Equals(string.IsNullOrWhiteSpace(enviada.Aviso) ? null : enviada.Aviso.Trim(), referencia.Aviso, StringComparison.Ordinal)
        && enviada.Exibicao is not { Count: > 0 };

    private static FieldError SecaoReservada(string campo) =>
        new(campo, new DomainError(
            EstruturaFormularioErrorCodes.SecaoReservada,
            $"A seção {CodigoDaSecao} é reservada aos dados básicos do candidato."));

    private static List<FatoColetadoInput> MontarItens()
    {
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> naoEstrangeiro = Quando(Nacionalidade, OperadorCodigo.Diferente, Estrangeiro);
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> estrangeiro = Quando(Nacionalidade, OperadorCodigo.Igual, Estrangeiro);

        string selecao = TipoRenderizacaoCodigo.SelecaoUnica;
        string texto = TipoRenderizacaoCodigo.Texto;
        string data = TipoRenderizacaoCodigo.Data;
        string sempre = PredicadoDnfJson.ObrigatoriedadeSempre;

        List<FatoColetadoInput> itens =
        [
            new("NOME", 0, "Nome completo", texto, sempre, null),
            new(DesejaNomeSocial, 0, "Deseja usar nome social?", TipoRenderizacaoCodigo.Booleano, sempre, null),
            new("NOME_SOCIAL", 0, "Nome social", texto, sempre, Quando(DesejaNomeSocial, OperadorCodigo.Igual, true)),
            new(Nacionalidade, 0, "Nacionalidade", selecao, sempre, null),
            new("CPF", 0, "CPF", texto, PredicadoDnfJson.ObrigatoriedadeQuando, null, PredicadoObrigatoriedade: naoEstrangeiro),
            new("RG_NUMERO", 0, "Número do RG", texto, sempre, naoEstrangeiro),
            new("RG_ORGAO_EMISSOR", 0, "Órgão emissor do RG", texto, sempre, naoEstrangeiro),
            new("RG_UF", 0, "UF de emissão do RG", selecao, sempre, naoEstrangeiro),
            new("RG_DATA_EMISSAO", 0, "Data de emissão do RG", data, sempre, naoEstrangeiro),
            new("DOCUMENTO_ESTRANGEIRO_TIPO", 0, "Documento de identificação", selecao, sempre, estrangeiro),
            new("DOCUMENTO_ESTRANGEIRO_NUMERO", 0, "Número do documento", texto, sempre, estrangeiro),
            new("DATA_NASCIMENTO", 0, "Data de nascimento", data, sempre, null),
            new("NATURALIDADE_UF", 0, "UF de nascimento", selecao, sempre, naoEstrangeiro),
            new("NATURALIDADE_MUNICIPIO", 0, "Município de nascimento", TipoRenderizacaoCodigo.Municipio, sempre, naoEstrangeiro,
                Restricoes: [new RestricaoValorInput(RestricaoValorJson.MunicipiosDaUf, Fatos: ["NATURALIDADE_UF"])]),
            new("NOME_MAE", 0, "Nome da mãe", texto, sempre, null),
            new("NOME_PAI", 0, "Nome do pai", texto, PredicadoDnfJson.ObrigatoriedadeNunca, null),
            new("ESTADO_CIVIL", 0, "Estado civil", selecao, sempre, null),
            new("SEXO", 0, "Sexo", selecao, sempre, null),
            new("COR_RACA", 0, "Cor ou raça", selecao, sempre, null),
            new("EMAIL", 0, "E-mail", texto, sempre, null),
            new("TELEFONE", 0, "Telefone", texto, sempre, null),
            new("ENDERECO_RESIDENCIAL", 0, "Endereço residencial", TipoRenderizacaoCodigo.Endereco, sempre, null),
        ];

        return [.. itens.Select(static (item, ordem) => item with { Ordem = ordem, EtapaCodigo = CodigoDaSecao })];
    }

    private static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> Quando(string fato, string operador, object valor) =>
        [[new CondicaoPrecondicaoInput(fato, operador, JsonSerializer.SerializeToElement(valor))]];
}

namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;

/// <summary>Um item do formulário, no que o grafo confere: o fato, a ordem, a seção e os fatos que as regras dele citam.</summary>
public sealed record ItemDoGrafo(string FatoCodigo, int Ordem, string? EtapaCodigo, IReadOnlyCollection<string> FatosCitados);

/// <summary>Uma etapa do formulário, no que o grafo confere: o código, a ordem e os fatos que a exibição cita.</summary>
public sealed record EtapaDoGrafo(string Codigo, int Ordem, IReadOnlyCollection<string> FatosCitados);

/// <summary>Um termo exigido pelo formulário, no que o grafo confere: o código e os fatos que as condições citam.</summary>
public sealed record TermoDoGrafo(string Codigo, IReadOnlyCollection<string> FatosCitados);

/// <summary>
/// O grafo de coleta do formulário, o mesmo no processo e no modelo (UNI-REQ-0144, UNI-REQ-0145):
/// cada fato uma vez, a ordem total, sem ciclo entre as regras, e cada regra — de item, de exibição
/// de seção e de termo — citando só o que o formulário conhece antes dela.
/// </summary>
/// <remarks>
/// A primeira recusa orienta quem corrige, e por isso a ordem é fixa: fato duplicado, ordem
/// duplicada, ciclo, citação dos itens, das seções e dos termos. O ciclo vem antes da citação
/// porque nomeia o caminho inteiro; a citação apontaria só o primeiro par fora de sequência do
/// mesmo problema.
/// </remarks>
public static class GrafoDoFormulario
{
    /// <summary>
    /// O que as regras do formulário podem citar: os itens dele, na ordem; os fatos conhecidos antes
    /// do formulário; e os derivados por regra cujas dependências estão entre esses.
    /// </summary>
    public static DependenciasDoFormulario Dependencias(
        IEnumerable<ItemDoGrafo> itens,
        IReadOnlySet<string> conhecidosAntes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        ArgumentNullException.ThrowIfNull(itens);
        return DependenciasDoFormulario.Criar(
            itens.ToDictionary(static i => i.FatoCodigo, static i => i.Ordem, StringComparer.Ordinal), conhecidosAntes, derivacoes);
    }

    /// <summary>
    /// A coleta do formulário: fato uma vez, ordem total, sem ciclo, e as regras dos itens e a
    /// exibição das seções citando só o que é conhecido antes delas. As dependências só são montadas
    /// depois de o fato aparecer uma vez só.
    /// </summary>
    public static DomainError? ValidarColeta(
        IReadOnlyList<ItemDoGrafo> itens,
        IReadOnlyCollection<EtapaDoGrafo> etapas,
        IReadOnlySet<string> conhecidosAntes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(etapas);

        Dictionary<string, ItemDoGrafo> porCodigo = new(StringComparer.Ordinal);
        HashSet<int> ordens = [];
        foreach (ItemDoGrafo item in itens)
        {
            if (!porCodigo.TryAdd(item.FatoCodigo, item))
            {
                return new DomainError(GrafoFormularioErrorCodes.FatoDuplicado, $"O fato '{item.FatoCodigo}' aparece mais de uma vez no formulário.");
            }

            if (!ordens.Add(item.Ordem))
            {
                return new DomainError(
                    GrafoFormularioErrorCodes.OrdemDuplicada,
                    $"A ordem {item.Ordem} é usada por mais de um item — a ordem de coleta precisa ser total.");
            }
        }

        Dictionary<string, IReadOnlyCollection<string>> citacoes = porCodigo.ToDictionary(
            static par => par.Key, static par => par.Value.FatosCitados, StringComparer.Ordinal);
        if (GrafoDeFatos.DetectarCiclo(citacoes) is { } caminho)
        {
            return new DomainError(GrafoFormularioErrorCodes.GrafoComCiclo, $"As regras dos campos formam um ciclo: {string.Join(" → ", caminho)}.");
        }

        DependenciasDoFormulario dependencias = Dependencias(itens, conhecidosAntes, derivacoes);
        return CitacaoInvalidaDosItens(itens, dependencias) ?? CitacaoInvalidaDasEtapas(etapas, itens, dependencias);
    }

    /// <summary>
    /// A primeira regra do formulário — de item, de exibição de seção e de termo, nessa ordem — que
    /// cita fato que ele não conhece.
    /// </summary>
    public static DomainError? CitacaoInvalida(
        IReadOnlyCollection<ItemDoGrafo> itens,
        IReadOnlyCollection<EtapaDoGrafo> etapas,
        IEnumerable<TermoDoGrafo> termos,
        DependenciasDoFormulario dependencias)
    {
        ArgumentNullException.ThrowIfNull(termos);
        return CitacaoInvalidaDosItens(itens, dependencias)
            ?? CitacaoInvalidaDasEtapas(etapas, itens, dependencias)
            ?? termos.Select(t => CitacaoInvalidaDoTermo(t, dependencias)).FirstOrDefault(static e => e is not null);
    }

    /// <summary>A primeira regra de item que cita fato que o formulário não conhece antes do item.</summary>
    public static DomainError? CitacaoInvalidaDosItens(IEnumerable<ItemDoGrafo> itens, DependenciasDoFormulario dependencias)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(dependencias);

        foreach (ItemDoGrafo item in itens)
        {
            foreach (string citado in item.FatosCitados)
            {
                switch (dependencias.Conferir(citado, item.Ordem))
                {
                    case RecusaDeCitacao.FatoNaoConhecido:
                        return new DomainError(
                            GrafoFormularioErrorCodes.CitaFatoNaoConhecido,
                            $"Uma regra do fato '{item.FatoCodigo}' cita '{citado}', que o formulário não coleta nem deriva, nem é conhecido antes dele.");
                    case RecusaDeCitacao.FatoPosterior:
                        return new DomainError(
                            GrafoFormularioErrorCodes.CitaFatoPosterior,
                            $"Uma regra do fato '{item.FatoCodigo}' cita '{citado}', que só fica conhecido nesse campo ou depois dele — "
                            + "o campo dependeria de uma resposta ainda não dada.");
                    case null:
                    default:
                        break;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// A exibição da seção cita só o que o formulário conhece antes dela: antes do primeiro item da
    /// própria seção ou, se ela está vazia, das seções seguintes.
    /// </summary>
    public static DomainError? CitacaoInvalidaDasEtapas(
        IReadOnlyCollection<EtapaDoGrafo> etapas, IEnumerable<ItemDoGrafo> itens, DependenciasDoFormulario dependencias)
    {
        ArgumentNullException.ThrowIfNull(etapas);
        return etapas.OrderBy(static e => e.Ordem)
            .Select(etapa => CitacaoInvalidaDaEtapa(etapa, PosicaoDaEtapa(etapa, etapas, itens), dependencias))
            .FirstOrDefault(static e => e is not null);
    }

    /// <summary>A exibição da etapa, na posição dada, cita fato que o formulário não conhece antes dela.</summary>
    public static DomainError? CitacaoInvalidaDaEtapa(EtapaDoGrafo etapa, long posicao, DependenciasDoFormulario dependencias)
    {
        ArgumentNullException.ThrowIfNull(etapa);
        ArgumentNullException.ThrowIfNull(dependencias);

        foreach (string citado in etapa.FatosCitados)
        {
            switch (dependencias.Conferir(citado, posicao))
            {
                case RecusaDeCitacao.FatoNaoConhecido:
                    return new DomainError(
                        GrafoFormularioErrorCodes.CitaFatoNaoConhecido,
                        $"A exibição da seção '{etapa.Codigo}' cita '{citado}', que o formulário não coleta nem deriva antes dela.");
                case RecusaDeCitacao.FatoPosterior:
                    return new DomainError(
                        GrafoFormularioErrorCodes.CitaFatoPosterior,
                        $"A exibição da seção '{etapa.Codigo}' cita '{citado}', que só fica conhecido na própria seção ou depois dela.");
                case null:
                default:
                    break;
            }
        }

        return null;
    }

    /// <summary>A condição do termo cita fato que o formulário não conhece.</summary>
    public static DomainError? CitacaoInvalidaDoTermo(TermoDoGrafo termo, DependenciasDoFormulario dependencias)
    {
        ArgumentNullException.ThrowIfNull(termo);
        ArgumentNullException.ThrowIfNull(dependencias);

        return termo.FatosCitados.FirstOrDefault(f => dependencias.Conferir(f, DependenciasDoFormulario.PosicaoDosTermos) is not null) is { } citado
            ? new DomainError(
                GrafoFormularioErrorCodes.CitaFatoNaoConhecido,
                $"A condição do termo '{termo.Codigo}' cita '{citado}', que o formulário não coleta nem deriva, nem é conhecido antes dele.")
            : null;
    }

    /// <summary>A posição da etapa na ordem de coleta: a do primeiro item dela ou das etapas seguintes.</summary>
    public static long PosicaoDaEtapa(EtapaDoGrafo etapa, IEnumerable<EtapaDoGrafo> etapas, IEnumerable<ItemDoGrafo> itens)
    {
        ArgumentNullException.ThrowIfNull(etapa);
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(itens);

        HashSet<string> daquiEmDiante = new(etapas.Where(e => e.Ordem >= etapa.Ordem).Select(static e => e.Codigo), StringComparer.Ordinal);
        return itens.Where(i => i.EtapaCodigo is { } codigo && daquiEmDiante.Contains(codigo))
            .Select(static i => (long)i.Ordem)
            .DefaultIfEmpty(DependenciasDoFormulario.PosicaoDosTermos)
            .Min();
    }
}

/// <summary>Códigos de recusa do grafo do formulário, no processo e no modelo.</summary>
public static class GrafoFormularioErrorCodes
{
    public const string FatoDuplicado = "GrafoFormulario.FatoDuplicado";
    public const string OrdemDuplicada = "GrafoFormulario.OrdemDuplicada";
    public const string GrafoComCiclo = "GrafoFormulario.GrafoComCiclo";
    public const string CitaFatoNaoConhecido = "GrafoFormulario.CitaFatoNaoConhecido";
    public const string CitaFatoPosterior = "GrafoFormulario.CitaFatoPosterior";
    public const string CitaAtributoDoCandidato = "GrafoFormulario.CitaAtributoDoCandidato";
}

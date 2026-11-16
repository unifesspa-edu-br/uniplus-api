namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// A pergunta única que decide se uma regra é válida: o fato que ela cita é conhecido no ponto onde ela
/// é usada? (UNI-REQ-0144, UNI-REQ-0077.) O fato é conhecido quando (1) o produtor é o formulário do
/// ponto ou um que ele alcança, (2) a fase do catálogo é igual ou anterior ao momento do ponto e (3) o
/// derivado tem todas as dependências conhecidas pelas mesmas condições; o ciclo não é conhecido. O fato
/// que nenhum formulário produz e o catálogo situa numa fase é resolvido pelo sistema nela.
/// </summary>
public sealed record ConhecimentoDosFatos(
    IReadOnlyList<ColetaDoFormulario> Coletas,
    CatalogoDosFatosCitados Catalogo,
    IReadOnlyCollection<FaseCronograma> Cronograma)
{
    public ConhecimentoDoFatoNoPonto NoPonto(string fato, PontoDeUso ponto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fato);
        ArgumentNullException.ThrowIfNull(ponto);
        return NoPonto(fato, ponto, []);
    }

    /// <summary>O código do grupo repetível de que o fato é campo, em qualquer formulário, ou nulo.</summary>
    public string? GrupoDoCampo(string fato) =>
        Coletas.Select(coleta => coleta.GrupoDoCampo(fato)).FirstOrDefault(static grupo => grupo is not null);

    /// <summary>
    /// O ponto de uso da exigência de documento: o formulário que a entrega, na fase dela. Nulo quando o
    /// processo não tem o formulário na fase da exigência, que é recusa da conferência das exigências.
    /// </summary>
    public PontoDeUso? PontoDaExigencia(CodigoDoFormulario codigoDoFormulario, Guid faseDaExigenciaId) =>
        Coletas.Select(static coleta => coleta.Formulario).FirstOrDefault(formulario => formulario.Codigo == codigoDoFormulario && formulario.FaseId == faseDaExigenciaId) is { } formularioDaExigencia
        && Cronograma.FirstOrDefault(fase => fase.Id == faseDaExigenciaId) is { } faseDaExigencia
            ? new PontoDeUso(faseDaExigencia, formularioDaExigencia)
            : null;

    /// <summary>
    /// Os fatos que os formulários alcançados pelo ponto produzem e que são conhecidos nele, sem os do
    /// próprio formulário do ponto: o que as regras dele podem citar além dos campos dele.
    /// </summary>
    public IReadOnlySet<string> ConhecidosAntesDoFormulario(PontoDeUso ponto)
    {
        ArgumentNullException.ThrowIfNull(ponto);
        return Coletas
            .Where(coleta => coleta.Formulario.Codigo != ponto.Formulario?.Codigo && AlcanceDasRespostas.Alcanca(ponto, coleta.Formulario, Cronograma))
            .SelectMany(static coleta => coleta.FatosProduzidos)
            .Where(fato => NoPonto(fato, ponto) == ConhecimentoDoFatoNoPonto.Conhecido)
            .ToHashSet(StringComparer.Ordinal);
    }

    private ConhecimentoDoFatoNoPonto NoPonto(string fato, PontoDeUso ponto, HashSet<string> emAvaliacao)
    {
        ConhecimentoDoFatoNoPonto daOrigem = DaOrigem(fato, ponto);
        if (daOrigem != ConhecimentoDoFatoNoPonto.Conhecido)
        {
            return daOrigem;
        }

        ConhecimentoDoFatoNoPonto daFase = FaseDoCatalogoNoPonto(fato, ponto);
        return daFase == ConhecimentoDoFatoNoPonto.Conhecido && Catalogo.DependenciasDosDerivados.ContainsKey(fato)
            ? DependenciasNoPonto(fato, ponto, emAvaliacao)
            : daFase;
    }

    /// <summary>
    /// O formulário que produz o fato é o do ponto ou um que ele alcança. Sem formulário produtor, o fato
    /// declarado não existe no processo, e o derivado ou o resolvido pelo sistema vale pelo catálogo.
    /// </summary>
    private ConhecimentoDoFatoNoPonto DaOrigem(string fato, PontoDeUso ponto)
    {
        ColetaDoFormulario[] produtores = [.. Coletas.Where(coleta => coleta.Produz(fato))];
        if (produtores.Length > 0)
        {
            return produtores.Any(produtor => AlcanceDasRespostas.Alcanca(ponto, produtor.Formulario, Cronograma))
                ? ConhecimentoDoFatoNoPonto.Conhecido
                : ConhecimentoDoFatoNoPonto.RespostaNaoAlcancada;
        }

        bool situadoNoCatalogo = Catalogo.FaseDoCatalogoPorFato.ContainsKey(fato) || Catalogo.DependenciasDosDerivados.ContainsKey(fato);
        return situadoNoCatalogo && !Catalogo.FatosDeclarados.Contains(fato)
            ? ConhecimentoDoFatoNoPonto.Conhecido
            : ConhecimentoDoFatoNoPonto.ForaDoProcesso;
    }

    private ConhecimentoDoFatoNoPonto FaseDoCatalogoNoPonto(string fato, PontoDeUso ponto)
    {
        if (!Catalogo.FaseDoCatalogoPorFato.TryGetValue(fato, out string? codigoDaFase))
        {
            return ConhecimentoDoFatoNoPonto.Conhecido;
        }

        FaseCronograma? faseDoCatalogo = Cronograma.FirstOrDefault(fase => string.Equals(fase.Codigo, codigoDaFase, StringComparison.Ordinal));
        if (faseDoCatalogo is null)
        {
            return ConhecimentoDoFatoNoPonto.FaseDoCatalogoForaDoCronograma;
        }

        return MomentoDaFase.Da(faseDoCatalogo, Cronograma) <= MomentoDaFase.Da(ponto.Fase, Cronograma)
            ? ConhecimentoDoFatoNoPonto.Conhecido
            : ConhecimentoDoFatoNoPonto.ConhecidoEmFasePosterior;
    }

    private ConhecimentoDoFatoNoPonto DependenciasNoPonto(string fato, PontoDeUso ponto, HashSet<string> emAvaliacao)
    {
        if (!emAvaliacao.Add(fato))
        {
            return ConhecimentoDoFatoNoPonto.DependenciaDesconhecida;
        }

        bool todasConhecidas = Catalogo.DependenciasDosDerivados[fato]
            .All(dependencia => NoPonto(dependencia, ponto, emAvaliacao) == ConhecimentoDoFatoNoPonto.Conhecido);
        emAvaliacao.Remove(fato);
        return todasConhecidas ? ConhecimentoDoFatoNoPonto.Conhecido : ConhecimentoDoFatoNoPonto.DependenciaDesconhecida;
    }
}

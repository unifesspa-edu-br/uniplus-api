namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A pergunta que decide se uma regra é válida: o fato que ela cita é conhecido no ponto onde ela
/// é usada? (UNI-REQ-0144, UNI-REQ-0077.) O fato é conhecido quando (1) o produtor é o formulário do
/// ponto ou um que ele alcança, (2) a fase do catálogo é igual ou anterior à vez do envio do ponto
/// (<see cref="NoPonto"/>, regra de formulário) ou à fase da exigência na ordem do cronograma
/// (<see cref="NaExigencia"/>, regra de aplicação e de repetição da exigência de documento) e (3) o
/// derivado tem todas as dependências conhecidas pelas mesmas condições; o ciclo não é conhecido. O fato
/// que nenhum formulário produz e o catálogo situa numa fase é resolvido pelo sistema nela. O dado que um
/// relatório da importação traz em toda linha, inteiro quando é parte de dado básico, é resposta anterior a
/// todos os formulários, e o fato "veio na importação" do dado básico é conhecido quando algum relatório
/// declara o dado inteiro, mesmo em coluna opcional, que pode vir preenchida.
/// </summary>
public sealed record ConhecimentoDosFatos(
    IReadOnlyList<ColetaDoFormulario> Coletas,
    CatalogoDosFatosCitados Catalogo,
    IReadOnlyCollection<FaseCronograma> Cronograma) : IOrigensDoFato
{
    /// <summary>Os relatórios da importação do resultado; nenhum no processo que não declara importação.</summary>
    public RelatoriosConsiderados Relatorios { get; init; } = RelatoriosConsiderados.Nenhum;

    /// <summary>Se o fato é conhecido no ponto, para a regra de formulário: a fase do catálogo vale pela vez do envio.</summary>
    public ConhecimentoDoFatoNoPonto NoPonto(string fato, PontoDeUso ponto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fato);
        ArgumentNullException.ThrowIfNull(ponto);
        return Conhecimento(fato, ponto, [], FaseDoCatalogoNoEnvio);
    }

    /// <summary>
    /// Se o fato é conhecido no formulário que entrega a exigência de documento, na ordem das recusas: a origem e o alcance das
    /// respostas, a fase em que o fato fica conhecido, que não pode ser posterior à da exigência na ordem do cronograma
    /// (<see cref="FaseEmQueOFatoFicaConhecido"/>), e as dependências do derivado.
    /// </summary>
    public ConhecimentoNaExigencia NaExigencia(string fato, PontoDeUso ponto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fato);
        ArgumentNullException.ThrowIfNull(ponto);

        ConhecimentoDoFatoNoPonto daOrigem = DaOrigem(fato, ponto);
        if (daOrigem != ConhecimentoDoFatoNoPonto.Conhecido)
        {
            return new(daOrigem, null);
        }

        return RecusaDaFaseNaExigencia(fato, ponto) is { } daFase
            ? new(ConhecimentoDoFatoNoPonto.Conhecido, daFase)
            : new(DasDependenciasNaExigencia(fato, ponto), null);
    }

    IReadOnlyCollection<FaseCronograma> IOrigensDoFato.Cronograma => Cronograma;

    string? IOrigensDoFato.CodigoDaFaseDoCatalogo(string fato) => Catalogo.FaseDoCatalogoPorFato.GetValueOrDefault(fato);

    IEnumerable<FaseCronograma> IOrigensDoFato.FasesDosFormulariosQueColetam(string fato) =>
        Coletas.Where(coleta => coleta.Produz(fato))
            .SelectMany(coleta => Cronograma.Where(fase => fase.Id == coleta.Formulario.FaseId));

    IEnumerable<string> IOrigensDoFato.Dependencias(string fato) =>
        Catalogo.DependenciasDosDerivados.GetValueOrDefault(fato) ?? [];

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
            .Concat(FatosDaImportacao)
            .Where(fato => NoPonto(fato, ponto) == ConhecimentoDoFatoNoPonto.Conhecido)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Os fatos que os relatórios trazem em toda linha e os fatos de presença dos dados que eles trazem inteiros.</summary>
    private IEnumerable<string> FatosDaImportacao =>
        Relatorios.Considerados.SelectMany(static leiaute => leiaute.Dados)
            .Where(static dado => dado.Presenca == PresencaDoDadoNoArquivo.EmTodaLinha)
            .Select(static dado => dado.FatoCodigo)
            .Concat(Catalogo.PresencasNaImportacao.Select(static presenca => presenca.Fato));

    private ConhecimentoDoFatoNoPonto Conhecimento(
        string fato, PontoDeUso ponto, HashSet<string> emAvaliacao, Func<string, PontoDeUso, ConhecimentoDoFatoNoPonto> faseDoCatalogo)
    {
        ConhecimentoDoFatoNoPonto daOrigem = DaOrigem(fato, ponto);
        if (daOrigem != ConhecimentoDoFatoNoPonto.Conhecido)
        {
            return daOrigem;
        }

        ConhecimentoDoFatoNoPonto daFase = faseDoCatalogo(fato, ponto);
        return daFase == ConhecimentoDoFatoNoPonto.Conhecido && Catalogo.DependenciasDosDerivados.ContainsKey(fato)
            ? DependenciasNoPonto(fato, ponto, emAvaliacao, faseDoCatalogo)
            : daFase;
    }

    private DomainError? RecusaDaFaseNaExigencia(string fato, PontoDeUso ponto)
    {
        OrdemDoCronograma ordem = OrdemDoCronograma.Do(Cronograma);

        return FaseEmQueOFatoFicaConhecido.RecusaDeFasePosterior(fato, FaseEmQueOFatoFicaConhecido.Resolver(this, ordem, fato), ponto.Fase, ordem);
    }

    // A fase do catálogo já foi julgada pela ordem do cronograma: aqui só contam a origem e o alcance das dependências.
    private ConhecimentoDoFatoNoPonto DasDependenciasNaExigencia(string fato, PontoDeUso ponto) =>
        Catalogo.DependenciasDosDerivados.ContainsKey(fato)
            ? DependenciasNoPonto(fato, ponto, [], static (_, _) => ConhecimentoDoFatoNoPonto.Conhecido)
            : ConhecimentoDoFatoNoPonto.Conhecido;

    /// <summary>
    /// O formulário que produz o fato é o do ponto ou um que ele alcança. Sem formulário produtor, o fato vale
    /// pela importação, quando um relatório o traz, ou pelo catálogo: o declarado não existe no processo, e o
    /// derivado ou o resolvido pelo sistema vale pelo catálogo.
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

        return DaImportacao(fato) ?? DoCatalogo(fato);
    }

    private ConhecimentoDoFatoNoPonto? DaImportacao(string fato)
    {
        if (Catalogo.PresencasNaImportacao.FirstOrDefault(presenca => string.Equals(presenca.Fato, fato, StringComparison.Ordinal)) is { } presenca)
        {
            return Relatorios.DeclaramTodosEmAlgum(presenca.FatosDoDadoBasico) ? ConhecimentoDoFatoNoPonto.Conhecido : ConhecimentoDoFatoNoPonto.ForaDoProcesso;
        }

        IReadOnlySet<string>? dadoBasico = Catalogo.PresencasNaImportacao.FirstOrDefault(presenca => presenca.FatosDoDadoBasico.Contains(fato))?.FatosDoDadoBasico;
        bool trazido = dadoBasico is null ? Relatorios.Traz(fato) : Relatorios.TrazTodosEmAlgum(dadoBasico);
        return trazido ? ConhecimentoDoFatoNoPonto.Conhecido : null;
    }

    private ConhecimentoDoFatoNoPonto DoCatalogo(string fato)
    {
        bool situadoNoCatalogo = Catalogo.FaseDoCatalogoPorFato.ContainsKey(fato) || Catalogo.DependenciasDosDerivados.ContainsKey(fato);
        return situadoNoCatalogo && !Catalogo.FatosDeclarados.Contains(fato)
            ? ConhecimentoDoFatoNoPonto.Conhecido
            : ConhecimentoDoFatoNoPonto.ForaDoProcesso;
    }

    private ConhecimentoDoFatoNoPonto FaseDoCatalogoNoEnvio(string fato, PontoDeUso ponto)
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

        return MomentoDoEnvio.Da(faseDoCatalogo, Cronograma) <= MomentoDoEnvio.Da(ponto.Fase, Cronograma)
            ? ConhecimentoDoFatoNoPonto.Conhecido
            : ConhecimentoDoFatoNoPonto.ConhecidoEmFasePosterior;
    }

    private ConhecimentoDoFatoNoPonto DependenciasNoPonto(
        string fato, PontoDeUso ponto, HashSet<string> emAvaliacao, Func<string, PontoDeUso, ConhecimentoDoFatoNoPonto> faseDoCatalogo)
    {
        if (!emAvaliacao.Add(fato))
        {
            return ConhecimentoDoFatoNoPonto.DependenciaDesconhecida;
        }

        bool todasConhecidas = Catalogo.DependenciasDosDerivados[fato]
            .All(dependencia => Conhecimento(dependencia, ponto, emAvaliacao, faseDoCatalogo) == ConhecimentoDoFatoNoPonto.Conhecido);
        emAvaliacao.Remove(fato);
        return todasConhecidas ? ConhecimentoDoFatoNoPonto.Conhecido : ConhecimentoDoFatoNoPonto.DependenciaDesconhecida;
    }
}

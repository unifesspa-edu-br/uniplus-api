namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class FaseEfetivaDoFato
{
    /// <summary>
    /// A recusa de citar o fato numa condição de aplicação de exigência da fase dada (UNI-REQ-0144, UNI-REQ-0077):
    /// nenhum documento é pedido antes de o fato ser conhecido. O fato fica conhecido na mais
    /// tardia entre a fase do catálogo (<paramref name="pontoResolucaoPorFato"/>) e a fase do
    /// formulário que o produz; o derivado por regra, não antes das suas dependências; e o agregado
    /// sobre grupo repetível (<paramref name="membroPorAgregado"/>), não antes do fato de membro. O fato só é
    /// citado por documento cujo formulário alcança a resposta do formulário que o produz
    /// (<see cref="AlcanceDasRespostas"/>): a habilitação usa, de outro formulário, só fatos dos obrigatórios respondidos antes.
    /// </summary>
    /// <param name="publicadas">
    /// O que as versões publicadas coletaram, na retificação: o fato de outro formulário tem de ter sido
    /// coletado em todas elas. Nula na escrita da exigência e na primeira publicação.
    /// </param>
    /// <remarks>
    /// Fato que o processo não coleta, não deriva e o catálogo não situa não é recusado aqui: a
    /// recusa por fato fora do processo é da conferência do universo. O derivado do sistema usa a
    /// fase do catálogo e a das dependências declaradas pelo mecanismo dele. O formulário que o processo não tem na fase da
    /// exigência também não é recusado aqui: a recusa que orienta é a da conferência das exigências do formulário.
    /// </remarks>
    public static DomainError? RecusaDeFaseDoGatilho(
        ConfiguracaoDeclarada configuracao,
        string fato,
        Guid exigidoNaFaseId,
        CodigoDoFormulario? formularioDaExigencia,
        IReadOnlyDictionary<string, string> pontoResolucaoPorFato,
        IReadOnlyDictionary<string, string> membroPorAgregado,
        FatosDasVersoesPublicadas? publicadas = null)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentException.ThrowIfNullOrWhiteSpace(fato);
        ArgumentNullException.ThrowIfNull(pontoResolucaoPorFato);
        ArgumentNullException.ThrowIfNull(membroPorAgregado);

        if (PontoDaExigencia(configuracao, exigidoNaFaseId, formularioDaExigencia) is not { } ponto)
        {
            return null;
        }

        return RecusaDaFaseEfetiva(configuracao, fato, ponto.Fase, pontoResolucaoPorFato, membroPorAgregado)
            ?? RecusaDaRespostaNaoAlcancada(configuracao, fato, ponto, membroPorAgregado)
            ?? RecusaDaCitacaoNaoGarantida(configuracao, fato, ponto, membroPorAgregado, publicadas);
    }

    /// <summary>
    /// A fase da exigência e o formulário que a entrega, quando o processo tem o formulário nessa fase; sem formulário
    /// declarado, só a fase. Nulo quando a fase não pertence ao cronograma ou o formulário não está nela.
    /// </summary>
    private static PontoDeUso? PontoDaExigencia(ConfiguracaoDeclarada configuracao, Guid exigidoNaFaseId, CodigoDoFormulario? formularioDaExigencia)
    {
        if (configuracao.Cronograma.CronogramaFases.FirstOrDefault(fase => fase.Id == exigidoNaFaseId) is not { } faseDaExigencia)
        {
            return null;
        }

        if (formularioDaExigencia is null)
        {
            return new PontoDeUso(faseDaExigencia, null);
        }

        return configuracao.Coleta.FormulariosDeclarados.FirstOrDefault(formulario => formulario.Codigo == formularioDaExigencia && formulario.FaseId == exigidoNaFaseId) is { } formulario
            ? new PontoDeUso(faseDaExigencia, formulario)
            : null;
    }

    private static DomainError? RecusaDaFaseEfetiva(
        ConfiguracaoDeclarada configuracao,
        string fato,
        FaseCronograma faseDaExigencia,
        IReadOnlyDictionary<string, string> pontoResolucaoPorFato,
        IReadOnlyDictionary<string, string> membroPorAgregado)
    {
        OrdemDoCronograma ordem = OrdemDoCronograma.Do(configuracao.Cronograma.CronogramaFases);
        FaseConhecida conhecida = FaseEmQueOFatoFicaConhecido.Resolver(
            new OrigensDoFatoNaConfiguracaoDeclarada(configuracao, pontoResolucaoPorFato, membroPorAgregado), ordem, fato);

        return FaseEmQueOFatoFicaConhecido.RecusaDeFasePosterior(fato, conhecida, faseDaExigencia, ordem);
    }

    private static DomainError? RecusaDaRespostaNaoAlcancada(
        ConfiguracaoDeclarada configuracao, string fato, PontoDeUso ponto, IReadOnlyDictionary<string, string> membroPorAgregado) =>
        RespostaNaoAlcancada(configuracao, fato, ponto, membroPorAgregado, [])
            ? new DomainError(
                DocumentoExigidoErrorCodes.FatoDeFormularioNaoAlcancado,
                $"O fato '{fato}' vem de um formulário cuja resposta o formulário que entrega o documento não alcança.")
            : null;

    /// <summary>
    /// Se o fato, diretamente ou por uma dependência, é coletado só por formulários que o ponto não alcança; o fato
    /// que nenhum formulário coleta não tem resposta a alcançar.
    /// </summary>
    private static bool RespostaNaoAlcancada(
        ConfiguracaoDeclarada configuracao, string fato, PontoDeUso ponto, IReadOnlyDictionary<string, string> membroPorAgregado, HashSet<string> emAvaliacao)
    {
        FormularioDeclarado[] produtores = [.. FormulariosQueColetam(configuracao, fato)];
        if (produtores.Length > 0)
        {
            return !produtores.Any(produtor => AlcanceDasRespostas.Alcanca(ponto, produtor, configuracao.Cronograma.CronogramaFases));
        }

        return emAvaliacao.Add(fato)
            && DependenciasDoFato(configuracao, fato, membroPorAgregado).Any(dependencia => RespostaNaoAlcancada(configuracao, dependencia, ponto, membroPorAgregado, emAvaliacao));
    }

    private static IEnumerable<FormularioDeclarado> FormulariosQueColetam(ConfiguracaoDeclarada configuracao, string fato) =>
        configuracao.Coleta.Campos
            .Where(campo => string.Equals(campo.FatoCodigo, fato, StringComparison.Ordinal))
            .Select(campo => configuracao.Coleta.Formularios.FirstOrDefault(formulario => formulario.Id == campo.FormularioId))
            .OfType<FormularioProcesso>()
            .Select(static formulario => formulario.Declarado);

    /// <summary>
    /// As exigências de um formulário seguem a versão dele; o fato de outro formulário tem de ter sido
    /// coletado em todas as versões publicadas (UNI-REQ-0144).
    /// </summary>
    private static DomainError? RecusaDaCitacaoNaoGarantida(
        ConfiguracaoDeclarada configuracao,
        string fato,
        PontoDeUso ponto,
        IReadOnlyDictionary<string, string> membroPorAgregado,
        FatosDasVersoesPublicadas? publicadas) =>
        publicadas is null
            ? null
            : CitacoesDosFormularios.CitacaoNaoGarantida(
                configuracao,
                publicadas,
                [($"a condição de aplicação de um documento exigido na fase {ponto.Fase.Citacao}", fato)],
                ponto.Formulario?.Codigo,
                membroPorAgregado);

    /// <summary>
    /// Os fatos de que o valor do fato depende no processo: os citados pela regra de derivação dele,
    /// os declarados pelo mecanismo do derivado do sistema, ou o fato de membro, quando ele é
    /// agregado sobre grupo repetível.
    /// </summary>
    internal static IEnumerable<string> DependenciasDoFato(ConfiguracaoDeclarada configuracao, string fato, IReadOnlyDictionary<string, string> membroPorAgregado) =>
        (configuracao.DerivacoesEfetivas().FirstOrDefault(r => string.Equals(r.CodigoFato, fato, StringComparison.Ordinal))?.FatosCitados ?? [])
            .Concat(DerivadosDoSistema.Dependencias.GetValueOrDefault(fato) ?? [])
            .Concat(membroPorAgregado.TryGetValue(fato, out string? membro) ? [membro] : []);
}

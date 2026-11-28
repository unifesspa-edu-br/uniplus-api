namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

/// <summary>
/// A classificação, na data da publicação, de cada tipo de ato que o cronograma declara, para ir ao
/// envelope da versão.
/// </summary>
/// <remarks>
/// Parte da mesma leitura do catálogo que os gates de publicação (<see cref="ResolucaoDosTiposDeAtoUnicos"/>),
/// e por isso o que se congela é o que os gates aprovaram. Um tipo declarado sem versão vigente não tem o
/// que congelar: a publicação o recusa, em vez de seguir com um tipo cuja publicação, depois, não teria
/// como saber se o tipo é resultado, se congela ou se é único.
/// </remarks>
internal static class CongelamentoDosTiposDeAto
{
    /// <summary>
    /// A classificação que a versão publicada já congelou. Na retificação, o tipo que consta nela mantém
    /// a classificação congelada: o catálogo só vale para o tipo declarado pela primeira vez.
    /// </summary>
    public static Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> DaVersao(
        IRegistroCodecsEnvelope registroCodecs,
        VersaoConfiguracao versaoPublicada)
    {
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(versaoPublicada);

        return DoEnvelope(registroCodecs.Reidratar(versaoPublicada));
    }

    /// <summary>A classificação congelada no envelope já reidratado, ou a falha do codec.</summary>
    public static Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> DoEnvelope(Result<EnvelopeReidratado> publicada)
    {
        ArgumentNullException.ThrowIfNull(publicada);

        return publicada.IsFailure
            ? Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>>.Failure(publicada.Error!)
            : Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>>.Success(publicada.Value!.DadosDeCatalogo.TiposDeAtoCongelados!);
    }

    public static Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> Resolver(
        ProcessoSeletivo processo,
        TiposDeAtoUnicosPorObjeto tipos)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(tipos);

        ConfiguracaoDeclarada declarada = processo.Declaracao();
        IReadOnlyList<FieldError> recusas = ConferenciaDosTiposDeAto.ConferirTiposSemVersaoVigente(
            CronogramaDoProcesso.Do(declarada.Cronograma.CronogramaFases, declarada.Origem),
            EtapasDoProcesso.Das(declarada.Avaliacao.Etapas, declarada.Origem),
            tipos);
        return recusas.Count > 0
            ? Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>>.ValidationFailure(recusas)
            : Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>>.Success(CongelarTodos(declarada, tipos));
    }

    private static Dictionary<string, TipoDeAtoCongelado> CongelarTodos(ConfiguracaoDeclarada declarada, TiposDeAtoUnicosPorObjeto tipos) =>
        TiposDeAtoDoCronograma.DeclaracoesDeAtos(declarada)
            .Select(static declaracao => declaracao.AtoCodigo)
            .Distinct(StringComparer.Ordinal)
            .Where(codigo => tipos.ClassificacaoDe(codigo) is not null)
            .ToDictionary(static codigo => codigo, codigo => Congelar(codigo, tipos), StringComparer.Ordinal);

    private static TipoDeAtoCongelado Congelar(string codigo, TiposDeAtoUnicosPorObjeto tipos)
    {
        ClassificacaoDoTipoDeAto classificacao = tipos.ClassificacaoDe(codigo)!;
        return new TipoDeAtoCongelado(
            codigo, tipos.Nomes.NomeDe(codigo)!, tipos.EhUnico(codigo), classificacao.EhResultado, classificacao.CongelaConfiguracao);
    }
}

namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

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
            : Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>>.Success(publicada.Value!.TiposDeAtoCongelados);
    }

    public static Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> Resolver(
        ProcessoSeletivo processo,
        TiposDeAtoUnicosPorObjeto tipos)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(tipos);

        Dictionary<string, TipoDeAtoCongelado> congelados = new(StringComparer.Ordinal);
        foreach (string codigo in processo.DeclaracoesDeAtos().Select(static d => d.AtoCodigo).Distinct(StringComparer.Ordinal))
        {
            if (tipos.ClassificacaoDe(codigo) is not { } classificacao)
            {
                return Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>>.Failure(new DomainError(
                    "ProcessoSeletivo.TipoDeAtoSemVersaoVigente",
                    $"Não há versão vigente do tipo de ato '{codigo}', declarado no cronograma, e a publicação congela a classificação de cada tipo declarado."));
            }

            congelados[codigo] = new TipoDeAtoCongelado(
                codigo, tipos.EhUnico(codigo), classificacao.EhResultado, classificacao.CongelaConfiguracao);
        }

        return Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>>.Success(congelados);
    }
}

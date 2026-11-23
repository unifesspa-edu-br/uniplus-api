namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>As etapas lidas pela forma, sem leitura externa, com a exibição de cada posição da entrada.</summary>
internal sealed record EtapasDoFormularioLidas(IReadOnlyList<EtapaFormulario> Etapas, IReadOnlyList<PredicadoDnf?> Exibicoes, IReadOnlyList<FieldError> Erros);

/// <summary>A etapa lida de uma posição da entrada: a etapa criada, a exibição lida e as recusas das duas.</summary>
internal sealed record EtapaLida(EtapaFormulario? Etapa, PredicadoDnf? Exibicao, IReadOnlyList<FieldError> Erros);

/// <summary>
/// A escrita das etapas do formulário do processo, sem ler nem mudar o processo: a forma de cada
/// etapa, sem I/O, e a semântica das exibições contra o catálogo. A estrutura por finalidade e a
/// posição das citações são do agregado.
/// </summary>
internal static class EscritaDasEtapasDoFormulario
{
    public static EtapasDoFormularioLidas Ler(IReadOnlyList<EtapaFormularioInput?> entradas)
    {
        ArgumentNullException.ThrowIfNull(entradas);
        List<EtapaLida> lidas = [.. entradas.Select((entrada, i) => LerEtapa(entrada, $"etapas[{i}]"))];
        return new EtapasDoFormularioLidas(
            [.. lidas.Select(static l => l.Etapa).OfType<EtapaFormulario>()],
            [.. lidas.Select(static l => l.Exibicao)],
            [.. lidas.SelectMany(static l => l.Erros)]);
    }

    /// <summary>
    /// A etapa criada pela forma e a exibição lida, que a semântica confere depois contra o catálogo; as recusas da
    /// exibição saem antes das da etapa.
    /// </summary>
    private static EtapaLida LerEtapa(EtapaFormularioInput? entrada, string campo)
    {
        if (entrada is null)
        {
            return new(null, null, [new(campo, new DomainError(EstruturaFormularioErrorCodes.EtapaCodigoInvalido, "A etapa veio nula."))]);
        }

        Result<PredicadoDnf?> exibicao = EntradaDeRegras.Predicado(entrada.Exibicao);
        PredicadoDnf? lida = exibicao.IsSuccess ? exibicao.Value : null;
        Result<EtapaFormulario> etapa = CriarEtapa(entrada, lida);
        FieldError[] recusasDaExibicao = exibicao.IsFailure ? [new($"{campo}.exibicao", exibicao.Error!)] : [];
        return new(etapa.IsSuccess ? etapa.Value : null, lida, [.. recusasDaExibicao, .. etapa.Errors.Select(erro => erro.NoCaminhoDe(campo))]);
    }

    private static Result<EtapaFormulario> CriarEtapa(EtapaFormularioInput entrada, PredicadoDnf? exibicao) =>
        EtapaFormulario.Criar(
            new EtapaEstrutura(
                entrada.Codigo,
                entrada.Ordem,
                EstruturaFormulario.TipoDoToken(entrada.Tipo),
                EstruturaFormulario.BlocoDoToken(entrada.Bloco)),
            entrada.Apresentacao?.Titulo ?? string.Empty,
            entrada.Apresentacao?.Descricao,
            entrada.Apresentacao?.Aviso,
            exibicao);

    /// <summary>A exibição de cada seção pelo vocabulário e pelos domínios do processo, sem fato calculado de atributos.</summary>
    public static IEnumerable<FieldError> ConferirExibicoes(EtapasDoFormularioLidas lidas, ContextoDoCatalogo contexto)
    {
        ArgumentNullException.ThrowIfNull(lidas);
        ArgumentNullException.ThrowIfNull(contexto);
        for (int i = 0; i < lidas.Exibicoes.Count; i++)
        {
            if (lidas.Exibicoes[i] is { } exibicao
                && (VocabularioDeFatos.CitacaoDeAtributoDoCandidato(exibicao.FatosCitados, contexto.Fatos)
                    ?? PredicadoDnfValidador.Validar(exibicao, contexto.Vocabulario, null, contexto.DominiosDinamicos).Error) is { } semantica)
            {
                yield return new($"etapas[{i}].exibicao", semantica);
            }
        }
    }
}

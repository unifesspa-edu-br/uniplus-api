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
internal sealed record EtapasLidas(IReadOnlyList<EtapaFormulario> Etapas, IReadOnlyList<PredicadoDnf?> Exibicoes, IReadOnlyList<FieldError> Erros);

/// <summary>
/// A escrita das etapas do formulário do processo, sem ler nem mudar o processo: a forma de cada
/// etapa, sem I/O, e a semântica das exibições contra o catálogo. A estrutura por finalidade e a
/// posição das citações são do agregado.
/// </summary>
internal static class EscritaDasEtapas
{
    public static EtapasLidas Ler(IReadOnlyList<EtapaFormularioInput?> entradas)
    {
        ArgumentNullException.ThrowIfNull(entradas);
        List<FieldError> erros = [];
        List<EtapaFormulario> etapas = [];
        PredicadoDnf?[] exibicoes = new PredicadoDnf?[entradas.Count];
        for (int i = 0; i < entradas.Count; i++)
        {
            if (entradas[i] is not { } entrada)
            {
                erros.Add(new($"etapas[{i}]", new DomainError(EstruturaFormularioErrorCodes.EtapaCodigoInvalido, "A etapa veio nula.")));
                continue;
            }

            Result<PredicadoDnf?> exibicao = EntradaDeRegras.Predicado(entrada.Exibicao);
            if (exibicao.IsFailure)
            {
                erros.Add(new($"etapas[{i}].exibicao", exibicao.Error!));
            }

            exibicoes[i] = exibicao.IsSuccess ? exibicao.Value : null;
            Result<EtapaFormulario> etapa = EtapaFormulario.Criar(
                entrada.Codigo, entrada.Ordem, EstruturaFormulario.TipoDoToken(entrada.Tipo), EstruturaFormulario.BlocoDoToken(entrada.Bloco),
                entrada.Titulo, entrada.Descricao, entrada.Aviso, exibicoes[i]);
            if (etapa.IsSuccess)
            {
                etapas.Add(etapa.Value!);
            }
            else
            {
                erros.AddRange(etapa.Errors.Select(e => new FieldError($"etapas[{i}].{e.Field}", e.Error)));
            }
        }

        return new EtapasLidas(etapas, exibicoes, erros);
    }

    /// <summary>A exibição de cada seção pelo vocabulário e pelos domínios do processo, sem fato calculado de atributos.</summary>
    public static IEnumerable<FieldError> ConferirExibicoes(EtapasLidas lidas, ContextoDoCatalogo contexto)
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

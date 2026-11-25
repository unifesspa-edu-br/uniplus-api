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

/// <summary>As páginas lidas pela forma, sem leitura externa, com a exibição de cada posição da entrada.</summary>
internal sealed record PaginasDoFormularioLidas(IReadOnlyList<PaginaDoFormulario> Paginas, IReadOnlyList<PredicadoDnf?> Exibicoes, IReadOnlyList<FieldError> Erros);

/// <summary>A página lida de uma posição da entrada: a página criada, a exibição lida e as recusas das duas.</summary>
internal sealed record PaginaLida(PaginaDoFormulario? Pagina, PredicadoDnf? Exibicao, IReadOnlyList<FieldError> Erros);

/// <summary>
/// A escrita das páginas do formulário do processo, sem ler nem mudar o processo: a forma de cada
/// página, sem I/O, e a semântica das exibições contra o catálogo. A estrutura por finalidade e a
/// posição das citações são do agregado.
/// </summary>
internal static class EscritaDasPaginasDoFormulario
{
    public static PaginasDoFormularioLidas Ler(IReadOnlyList<PaginaDoFormularioInput?> entradas)
    {
        ArgumentNullException.ThrowIfNull(entradas);
        List<PaginaLida> lidas = [.. entradas.Select((entrada, i) => LerPagina(entrada, $"{EstruturaFormulario.CaminhoDasPaginas}[{i}]"))];
        return new PaginasDoFormularioLidas(
            [.. lidas.Select(static l => l.Pagina).OfType<PaginaDoFormulario>()],
            [.. lidas.Select(static l => l.Exibicao)],
            [.. lidas.SelectMany(static l => l.Erros)]);
    }

    /// <summary>
    /// A página criada pela forma e a exibição lida, que a semântica confere depois contra o catálogo; as recusas saem
    /// na ordem exibição, tipo, bloco e forma da página.
    /// </summary>
    private static PaginaLida LerPagina(PaginaDoFormularioInput? entrada, string campo)
    {
        if (entrada is null)
        {
            return new(null, null, [new(campo, new DomainError(EstruturaFormularioErrorCodes.PaginaCodigoInvalido, "A página veio nula."))]);
        }

        Result<PredicadoDnf?> exibicao = EntradaDeRegras.Predicado(entrada.Exibicao);
        PredicadoDnf? lida = exibicao.IsSuccess ? exibicao.Value : null;
        (PaginaDoFormulario? pagina, IReadOnlyList<FieldError> recusas) = CriarPagina(entrada, lida, campo);
        return new(pagina, lida, [.. Recusa(exibicao, $"{campo}.exibicao"), .. recusas]);
    }

    private static (PaginaDoFormulario? Pagina, IReadOnlyList<FieldError> Recusas) CriarPagina(PaginaDoFormularioInput entrada, PredicadoDnf? exibicao, string campo)
    {
        Result<TipoPaginaFormulario> tipo = TipoEBlocoDaEntrada.LerTipo(entrada.Tipo);
        Result<BlocoSistema?> bloco = TipoEBlocoDaEntrada.LerBloco(entrada.Bloco);
        FieldError[] recusas = [.. Recusa(tipo, $"{campo}.tipo"), .. Recusa(bloco, $"{campo}.bloco")];
        return recusas.Length > 0
            ? (null, recusas)
            : MontarPagina(entrada, tipo.Value, bloco.Value, exibicao, campo);
    }

    private static (PaginaDoFormulario? Pagina, IReadOnlyList<FieldError> Recusas) MontarPagina(
        PaginaDoFormularioInput entrada, TipoPaginaFormulario tipo, BlocoSistema? bloco, PredicadoDnf? exibicao, string campo)
    {
        Result<PaginaDoFormulario> pagina = PaginaDoFormulario.Criar(
            new PaginaEstrutura(entrada.Codigo, entrada.Ordem, tipo, bloco),
            entrada.Apresentacao?.Titulo ?? string.Empty,
            entrada.Apresentacao?.Descricao,
            entrada.Apresentacao?.Aviso,
            exibicao);
        return (pagina.IsSuccess ? pagina.Value : null, [.. pagina.Errors.Select(erro => erro.NoCaminhoDe(campo))]);
    }

    private static IEnumerable<FieldError> Recusa<T>(Result<T> lido, string campo) =>
        lido.IsFailure ? [new(campo, lido.Error!)] : [];

    /// <summary>A exibição de cada seção pelo vocabulário e pelos domínios do processo, sem fato calculado de atributos.</summary>
    public static IEnumerable<FieldError> ConferirExibicoes(PaginasDoFormularioLidas lidas, ContextoDoCatalogo contexto)
    {
        ArgumentNullException.ThrowIfNull(lidas);
        ArgumentNullException.ThrowIfNull(contexto);
        for (int i = 0; i < lidas.Exibicoes.Count; i++)
        {
            if (lidas.Exibicoes[i] is { } exibicao
                && (VocabularioDeFatos.CitacaoDeAtributoDoCandidato(exibicao.FatosCitados, contexto.Fatos)
                    ?? PredicadoDnfValidador.Validar(exibicao, contexto.Vocabulario, null, contexto.DominiosDinamicos).Error) is { } semantica)
            {
                yield return new($"{EstruturaFormulario.CaminhoDasPaginas}[{i}].exibicao", semantica);
            }
        }
    }
}

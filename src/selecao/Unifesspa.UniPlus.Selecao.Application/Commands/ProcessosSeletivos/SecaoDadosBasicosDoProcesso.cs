namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// Os itens e os grupos que a escrita lê depois de mesclados com o conjunto básico, as recusas da mescla
/// e se os grupos omitidos do pedido serão regravados com o deslocamento. Os itens são nulos quando o pedido não os enviou.
/// </summary>
internal sealed record ColetaMesclada(
    IReadOnlyList<FatoColetadoInput>? Itens,
    IReadOnlyList<GrupoColetadoInput> Grupos,
    IReadOnlyList<FieldError> RecusasDaSecao,
    bool GruposOmitidosRegravados);

/// <summary>
/// A seção dos dados básicos do formulário que coleta a inscrição, na forma de entrada: a gravada é a
/// referência, e a do conjunto básico só vale enquanto o formulário não a tem. O conjunto básico fica em
/// um só dos formulários da inscrição (<see cref="ConjuntoBasicoNaInscricao"/>), porque cada fato é coletado por um
/// só formulário do processo.
/// </summary>
internal static class SecaoDadosBasicosDoProcesso
{
    /// <summary>
    /// O formulário que guarda o conjunto básico o coleta na seção reservada: o item básico, omitido ou
    /// enviado, entra pela referência, com a ordem e o rótulo do conjunto, e o enviado só pode diferir dela
    /// nessas duas coisas. O item básico que falta no formulário gravado entra na seção e desloca o que vem
    /// depois dela; os grupos omitidos, que ficam como estão gravados, entram então na mescla e são regravados,
    /// para o deslocamento ter uma fonte só e a ordem seguir única. Nos demais formulários nada se mescla, nem quando o pedido não
    /// envia os itens: a mescla depende deles, e a recusa da lista ausente é da leitura dos itens.
    /// </summary>
    public static ColetaMesclada Mesclar(ProcessoSeletivo processo, CodigoDoFormulario codigo, DefinirFatosColetadosCommand command)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(command);
        FormularioProcesso? formulario = processo.Declaracao().Coleta.FormularioDe(codigo);
        if (command.Itens is null || formulario is null || !ConjuntoBasicoNaInscricao.Guarda(processo.Declaracao(), formulario.Declarado))
        {
            return new ColetaMesclada(command.Itens, command.Grupos ?? [], [], false);
        }

        bool regravaGrupos = command.Grupos is null && SecaoGanhaBasicos(processo, formulario);
        (IReadOnlyList<FatoColetadoInput> itens, IReadOnlyList<GrupoColetadoInput> grupos, List<FieldError> recusas) = ConjuntoBasicoDaInscricao.MesclarItens(
            command.Itens, regravaGrupos ? Grupos(processo, formulario) : command.Grupos ?? [], Itens(processo, formulario));
        return new ColetaMesclada(itens, grupos, recusas, regravaGrupos);
    }

    /// <summary>A seção gravada no formulário do código, ou a do conjunto básico.</summary>
    public static PaginaDoFormularioInput Secao(ProcessoSeletivo processo, CodigoDoFormulario codigo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(codigo);
        return ConjuntoBasicoDaInscricao.SecaoDe((processo.Declaracao().Coleta.FormularioDe(codigo)?.Paginas ?? [])
            .Select(static gravada => new PaginaDoFormularioInput(
                gravada.Codigo, gravada.Ordem, gravada.Tipo.Codigo(), gravada.Bloco?.Codigo(),
                new ApresentacaoDaPaginaInput(gravada.Titulo, gravada.Descricao, gravada.Aviso), EntradaDeRegras.ParaEntrada(gravada.Exibicao))));
    }

    /// <summary>Os itens básicos gravados no formulário e, onde faltam, os do conjunto básico.</summary>
    private static IReadOnlyList<FatoColetadoInput> Itens(ProcessoSeletivo processo, FormularioProcesso formulario) =>
        ConjuntoBasicoDaInscricao.Referencia(processo.FatosColetados
            .Where(f => f.FormularioId == formulario.Id)
            .Select(Entrada));

    /// <summary>Os grupos gravados no formulário, na forma de entrada.</summary>
    private static IReadOnlyList<GrupoColetadoInput> Grupos(ProcessoSeletivo processo, FormularioProcesso formulario) =>
        [.. processo.GruposColetados
            .Where(g => g.FormularioId == formulario.Id)
            .Select(static g => new GrupoColetadoInput(
                g.Codigo, g.Ordem, g.Rotulo, EntradaDeRegras.ParaEntrada(g.Ocorrencias), EntradaDeRegras.ParaEntrada(g.Regras),
                [.. g.Subitens.Select(Entrada)], g.PaginaCodigo))];

    /// <summary>
    /// A seção gravada ganha itens básicos na próxima gravação: ela já tem algum, mas não todos. O
    /// deslocamento do que vem depois dela é de <see cref="ConjuntoBasicoDaInscricao.MesclarItens"/>.
    /// </summary>
    private static bool SecaoGanhaBasicos(ProcessoSeletivo processo, FormularioProcesso formulario)
    {
        int naSecao = processo.FatosColetados
            .Where(f => f.FormularioId == formulario.Id
                && string.Equals(f.PaginaCodigo, ConjuntoBasicoDaInscricao.CodigoDaSecao, StringComparison.Ordinal)
                && ConjuntoBasicoDaInscricao.Fatos.Contains(f.FatoCodigo))
            .Select(static f => f.FatoCodigo).Distinct(StringComparer.Ordinal).Count();
        return naSecao > 0 && naSecao < ConjuntoBasicoDaInscricao.Fatos.Count;
    }

    /// <summary>O item gravado na forma de entrada.</summary>
    private static FatoColetadoInput Entrada(FatoColetado fato) =>
        new(fato.FatoCodigo, fato.Ordem, EntradaDeRegras.ParaEntrada(fato.Apresentacao), EntradaDeRegras.ParaEntrada(fato.Regras), fato.PaginaCodigo);
}

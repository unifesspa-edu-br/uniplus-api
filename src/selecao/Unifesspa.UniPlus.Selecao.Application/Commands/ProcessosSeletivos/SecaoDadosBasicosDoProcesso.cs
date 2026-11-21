namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Os itens e os grupos que a escrita lê depois de mesclados com o conjunto básico, as recusas da mescla
/// e se os grupos omitidos do pedido serão regravados com o deslocamento.
/// </summary>
internal sealed record ColetaMesclada(
    IReadOnlyList<FatoColetadoInput> Itens,
    IReadOnlyList<GrupoColetadoInput> Grupos,
    IReadOnlyList<FieldError> RecusasDaSecao,
    bool GruposOmitidosRegravados);

/// <summary>
/// A seção dos dados básicos do formulário de inscrição do processo, na forma de entrada: a gravada
/// é a referência, e a do conjunto básico só vale enquanto o formulário não a tem.
/// </summary>
internal static class SecaoDadosBasicosDoProcesso
{
    /// <summary>
    /// O formulário de inscrição coleta o conjunto básico na seção reservada: o item básico, omitido ou
    /// enviado, entra pela referência, com a ordem e o rótulo do conjunto, e o enviado só pode diferir dela
    /// nessas duas coisas. O item básico que falta no formulário gravado entra na seção e desloca o que vem
    /// depois dela; os grupos omitidos, que ficam como estão gravados, entram então na mescla e são regravados,
    /// para o deslocamento ter uma fonte só e a ordem seguir única. Nas outras finalidades nada se mescla.
    /// </summary>
    public static ColetaMesclada Mesclar(ProcessoSeletivo processo, DefinirFatosColetadosCommand command)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(command);
        if (command.Finalidade != FinalidadeFormulario.Inscricao)
        {
            return new ColetaMesclada(command.Itens, command.Grupos ?? [], [], false);
        }

        bool regravaGrupos = command.Grupos is null && SecaoGanhaBasicos(processo);
        (IReadOnlyList<FatoColetadoInput> itens, IReadOnlyList<GrupoColetadoInput> grupos, List<FieldError> recusas) = ConjuntoBasicoDaInscricao.MesclarItens(
            command.Itens, regravaGrupos ? Grupos(processo) : command.Grupos ?? [], Itens(processo));
        return new ColetaMesclada(itens, grupos, recusas, regravaGrupos);
    }

    /// <summary>Os itens básicos gravados no formulário de inscrição e, onde faltam, os do conjunto básico.</summary>
    public static IReadOnlyList<FatoColetadoInput> Itens(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        return ConjuntoBasicoDaInscricao.Referencia(processo.FatosColetados
            .Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao)
            .Select(Entrada));
    }

    /// <summary>A seção gravada no formulário de inscrição, ou a do conjunto básico.</summary>
    public static EtapaFormularioInput Secao(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        return ConjuntoBasicoDaInscricao.SecaoDe((processo.FormularioDe(FinalidadeFormulario.Inscricao)?.Etapas ?? [])
            .Select(static gravada => new EtapaFormularioInput(
                gravada.Codigo, gravada.Ordem, EstruturaFormulario.ParaToken(gravada.Tipo), EstruturaFormulario.ParaToken(gravada.Bloco),
                gravada.Titulo, gravada.Descricao, gravada.Aviso, EntradaDeRegras.ParaEntrada(gravada.Exibicao))));
    }

    /// <summary>Os grupos gravados no formulário de inscrição, na forma de entrada.</summary>
    public static IReadOnlyList<GrupoColetadoInput> Grupos(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        return [.. processo.GruposColetados
            .Where(static g => g.Finalidade == FinalidadeFormulario.Inscricao)
            .Select(static g => new GrupoColetadoInput(
                g.Codigo, g.Ordem, g.Rotulo, EntradaDeRegras.ParaEntrada(g.Ocorrencias), EntradaDeRegras.ParaEntrada(g.Regras),
                [.. g.Subitens.Select(Entrada)], g.EtapaCodigo))];
    }

    /// <summary>
    /// A seção gravada ganha itens básicos na próxima gravação: ela já tem algum, mas não todos. O
    /// deslocamento do que vem depois dela é de <see cref="ConjuntoBasicoDaInscricao.MesclarItens"/>.
    /// </summary>
    public static bool SecaoGanhaBasicos(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        int naSecao = processo.FatosColetados
            .Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao
                && string.Equals(f.EtapaCodigo, ConjuntoBasicoDaInscricao.CodigoDaSecao, StringComparison.Ordinal)
                && ConjuntoBasicoDaInscricao.Fatos.Contains(f.FatoCodigo))
            .Select(static f => f.FatoCodigo).Distinct(StringComparer.Ordinal).Count();
        return naSecao > 0 && naSecao < ConjuntoBasicoDaInscricao.Fatos.Count;
    }

    /// <summary>O item gravado na forma de entrada.</summary>
    private static FatoColetadoInput Entrada(FatoColetado fato) =>
        new(fato.FatoCodigo, fato.Ordem, EntradaDeRegras.ParaEntrada(fato.Apresentacao), EntradaDeRegras.ParaEntrada(fato.Regras), fato.EtapaCodigo);
}

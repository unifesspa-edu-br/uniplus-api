namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// A seção dos dados básicos do formulário de inscrição do processo, na forma de entrada: a gravada
/// é a referência, e a do conjunto básico só vale enquanto o formulário não a tem.
/// </summary>
internal static class SecaoDadosBasicosDoProcesso
{
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

    /// <summary>O item gravado na forma de entrada.</summary>
    private static FatoColetadoInput Entrada(FatoColetado fato) =>
        new(
            fato.FatoCodigo,
            fato.Ordem,
            fato.Rotulo,
            fato.TipoRenderizacao.ToCodigo(),
            EntradaDeRegras.ParaEntrada(fato.Obrigatoriedade),
            EntradaDeRegras.ParaEntrada(fato.Exibicao),
            fato.EtapaCodigo,
            EntradaDeRegras.ParaEntrada(fato.Obrigatoriedade.Predicado),
            fato.Ajuda,
            fato.PedirConfirmacao,
            [.. fato.Restricoes.Select(EntradaDeRegras.ParaEntrada)]);
}

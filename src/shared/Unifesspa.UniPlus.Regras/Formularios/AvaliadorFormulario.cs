namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Avalia um formulário contra as respostas de um candidato: resolve cada fato em ordem topológica,
/// com os fatos derivados e os agregados sobre grupos intercalados assim que as suas dependências
/// estão resolvidas, e diz, por
/// item, se ele aparece, se é obrigatório e quais restrições a resposta viola; por termo, se aparece e
/// se é obrigatório; e, por grupo repetível, o mesmo para cada ocorrência (UNI-REQ-0074,
/// UNI-REQ-0145, UNI-REQ-0146, ADR-0135).
/// </summary>
/// <remarks>
/// <para>
/// Função pura: a mesma entrada produz sempre a mesma saída. A mesma avaliação serve à
/// pré-visualização, com respostas simuladas, e à execução da inscrição.
/// </para>
/// <para>
/// O estado do fato de um item segue esta ordem:
/// </para>
/// <list type="number">
/// <item>etapa ou item oculto: <see cref="EstadoFato.NaoAplicavel"/>, mesmo que haja resposta gravada
/// — a resposta anterior deixa de valer sem precisar ser apagada;</item>
/// <item>exibição ainda indeterminada: <see cref="EstadoFato.Indeterminado"/>;</item>
/// <item>resposta que atende às restrições: <see cref="EstadoFato.Resolvido"/>;</item>
/// <item>resposta cuja validade depende de resposta anterior ainda desconhecida:
/// <see cref="EstadoFato.Indeterminado"/>;</item>
/// <item>sem resposta, ou com resposta que viola restrição e por isso não vale: pendente
/// (<see cref="EstadoFato.Indeterminado"/>) se o item é obrigatório ou se a etapa ainda não foi
/// concluída; <see cref="EstadoFato.NaoInformado"/> se o item é opcional e a etapa foi
/// concluída.</item>
/// </list>
/// <para>
/// Um nó que não entra na ordem, por pertencer a um ciclo ou depender de um, fica indeterminado: o
/// avaliador não decide sobre o que não consegue ordenar. A recusa do ciclo, com o caminho, é do
/// cadastro.
/// </para>
/// </remarks>
public static class AvaliadorFormulario
{
    public static AvaliacaoFormulario Avaliar(DefinicaoFormulario definicao, EntradaAvaliacaoFormulario entrada)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        ArgumentNullException.ThrowIfNull(entrada);
        OrdemTopologica ordem = GrafoDeFatos.Ordenar(definicao.NosDoGrafoDeFatos());
        ResolucaoDoFormulario resolucao = new(definicao, entrada);
        resolucao.ResolverNaOrdem(ordem);

        return resolucao.Resultado();
    }

    internal static FatoResolvido Derivar(DerivacaoDoFato derivacao, IReadOnlyDictionary<string, FatoResolvido> fatos)
    {
        ResultadoDerivacao resultado = MotorDerivacao.Derivar(derivacao, fatos);
        if (resultado.Estado != EstadoFato.Resolvido)
        {
            return FatoResolvido.Indeterminado();
        }

        return resultado.ValorBooleano is { } booleano
            ? FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(booleano))
            : FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(resultado.Valores.Order(StringComparer.Ordinal)));
    }

    /// <summary>
    /// Obrigatório só o que aparece: o E ternário deixa uma obrigatoriedade já falsa decidir mesmo
    /// com a exibição ainda indeterminada — um campo que nunca é obrigatório não fica pendente.
    /// </summary>
    internal static Ternario ObrigatorioSeVisivel(
        Ternario visivel, Obrigatoriedade obrigatoriedade, IReadOnlyDictionary<string, FatoResolvido> fatos) =>
        E(visivel, obrigatoriedade.Avaliar(fatos));

    internal static Ternario E(Ternario a, Ternario b)
    {
        if (a == Ternario.Falso || b == Ternario.Falso)
        {
            return Ternario.Falso;
        }

        return a == Ternario.Indeterminado || b == Ternario.Indeterminado ? Ternario.Indeterminado : Ternario.Verdadeiro;
    }
}

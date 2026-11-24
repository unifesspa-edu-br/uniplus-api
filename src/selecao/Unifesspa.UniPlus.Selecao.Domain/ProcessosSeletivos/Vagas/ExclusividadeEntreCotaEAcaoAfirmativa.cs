namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ExclusividadeEntreCotaEAcaoAfirmativa
{
    /// <summary>
    /// A mesma inscrição não concorre a cota da lei e a ação afirmativa, salvo a que se soma às cotas
    /// (UNI-REQ-0142 e UNI-REQ-0266). As regras de aplicabilidade das modalidades ofertadas definem as
    /// modalidades do candidato, então a recusa é sobre elas: se alguma combinação de respostas faz
    /// valer, ao mesmo tempo, a regra de uma cota e a de uma ação afirmativa que não se soma às cotas,
    /// o processo não publica. O cadastro já recusa a regra que permitiria isso; aqui é a defesa sobre
    /// a cópia congelada.
    /// </summary>
    /// <remarks>
    /// Duas cláusulas só valem juntas se nenhum fato é exigido com valores incompatíveis nelas. A prova
    /// de incompatibilidade é conservadora — o mesmo fato escalar com <c>IGUAL</c> a valores
    /// diferentes, ou <c>IGUAL</c> e <c>DIFERENTE</c> ao mesmo valor —, e o que ela não prova conta
    /// como derivável junto. Fato multivalorado (seleção múltipla) ou derivado categórico não é
    /// tratado como escalar: sobre ele <c>IGUAL</c> é pertinência, e o mesmo candidato pode conter os
    /// dois valores. O derivado booleano é escalar.
    /// A regra sem cláusula, como a da ampla concorrência, vale sempre.
    /// </remarks>
    internal static DomainError? PendenciaDaExclusividadeEntreCotaEAcaoAfirmativa(ConfiguracaoDeclarada configuracao) =>
        PrimeiroParQuePodeValerJunto(RegrasDaDistribuicaoDeVagas.ModalidadesOfertadas(configuracao), FatosMultivalorados(configuracao)) is { } par
            ? RecusaDoPar(par.Cota, par.AcaoAfirmativa)
            : null;

    private static HashSet<string> FatosMultivalorados(ConfiguracaoDeclarada configuracao) =>
    [
        .. configuracao.Coleta.Itens
            .Where(static f => f.TipoRenderizacao == TipoRenderizacao.EscolhaMultipla)
            .Select(static f => f.FatoCodigo),
        .. configuracao.DerivacoesEfetivas().Where(static c => !c.Booleano).Select(static c => c.CodigoFato),
    ];

    private static (ModalidadeSelecionada Cota, ModalidadeSelecionada AcaoAfirmativa)? PrimeiroParQuePodeValerJunto(
        List<ModalidadeSelecionada> ofertadas, HashSet<string> fatosMultivalorados)
    {
        foreach (ModalidadeSelecionada cota in ofertadas.Where(static m => m.NaturezaLegal == NaturezaLegalModalidade.CotaReservada))
        {
            foreach (ModalidadeSelecionada acaoAfirmativa in ofertadas.Where(static m => m.NaturezaLegal == NaturezaLegalModalidade.AcaoAfirmativa && !OpcaoPelasCotasDaLei.SeSomaAsCotas(m.Codigo)))
            {
                if (PodemValerJuntas(cota.RegraDeAplicabilidade, acaoAfirmativa.RegraDeAplicabilidade, fatosMultivalorados))
                {
                    return (cota, acaoAfirmativa);
                }
            }
        }

        return null;
    }

    private static DomainError RecusaDoPar(ModalidadeSelecionada cota, ModalidadeSelecionada acaoAfirmativa) => new(
        ProcessoSeletivoErrorCodes.CotaEAcaoAfirmativaDerivaveisJuntas,
        $"As regras de aplicabilidade permitem que o mesmo candidato concorra à cota \"{cota.Codigo}\" "
        + $"e à ação afirmativa \"{acaoAfirmativa.Codigo}\". Quem opta pelas cotas da lei só concorre também "
        + $"à reserva de vagas para pessoa com deficiência ({OpcaoPelasCotasDaLei.AcaoAfirmativaQueSeSomaAsCotas}); "
        + "a regra das demais ações afirmativas precisa recusar os candidatos atendidos pelas cotas da lei.");

    internal static bool PodemValerJuntas(PredicadoDnf primeiro, PredicadoDnf segundo, IReadOnlySet<string> fatosMultivalorados) =>
        ClausulasDe(primeiro).Any(a => ClausulasDe(segundo).Any(b => !Incompativeis(a, b, fatosMultivalorados)));

    internal static IEnumerable<IReadOnlyList<CondicaoDnf>> ClausulasDe(PredicadoDnf predicado) =>
        predicado.Clausulas.Count == 0 ? [[]] : predicado.Clausulas.Select(static c => c.Condicoes);

    internal static bool Incompativeis(
        IReadOnlyList<CondicaoDnf> primeira,
        IReadOnlyList<CondicaoDnf> segunda,
        IReadOnlySet<string> fatosMultivalorados) =>
        primeira.Any(a => segunda.Any(b =>
            string.Equals(a.Fato, b.Fato, StringComparison.Ordinal)
            && (a.Operador, b.Operador) switch
            {
                (Operador.Igual, Operador.Igual) => !fatosMultivalorados.Contains(a.Fato) && ValoresDistintos(a, b),
                (Operador.Igual, Operador.Diferente) or (Operador.Diferente, Operador.Igual) => MesmoValor(a, b),
                _ => false,
            }));

    /// <summary>
    /// Na régua do motor: tipos que ele não compara não provam nem igualdade nem diferença.
    /// </summary>
    internal static bool MesmoValor(CondicaoDnf a, CondicaoDnf b) =>
        ClausulaDnf.CompararIgualdade(a.Valor, b.Valor) == Ternario.Verdadeiro;

    internal static bool ValoresDistintos(CondicaoDnf a, CondicaoDnf b) =>
        ClausulaDnf.CompararIgualdade(a.Valor, b.Valor) == Ternario.Falso;
}

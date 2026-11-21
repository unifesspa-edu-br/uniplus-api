namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Os motivos que o processo escolheu, cada um com o motivo de origem no cadastro, lidos para a versão que nasce. O processo
/// congela a cópia feita na escolha, e a conferência só lê do cadastro se o motivo continua ativo. O motivo que já consta de
/// versão publicada não é recusado por ter sido desativado depois (UNI-REQ-0122), nem a retirada dele é aceita, e então o
/// único conserto de um tipo que a fase ou a etapa deixou de declarar é declará-lo de novo.
/// </summary>
public sealed class MotivosEscolhidos
{
    /// <summary>O bloco do documento da configuração em que os motivos escolhidos se identificam pelo lugar (a fase ou a etapa e a chave dela), pelo tipo e pelo código.</summary>
    public const string CampoDosMotivos = "motivos";

    private readonly IReadOnlyList<MotivoEscolhidoComOrigem> _escolha;
    private readonly int _numeroDaVersaoQueNasce;

    public MotivosEscolhidos(IReadOnlyList<MotivoEscolhidoComOrigem> escolha, int numeroDaVersaoQueNasce)
    {
        ArgumentNullException.ThrowIfNull(escolha);

        _escolha = escolha;
        _numeroDaVersaoQueNasce = numeroDaVersaoQueNasce;
    }

    /// <summary>
    /// Uma recusa por motivo que entra na versão que nasce e veio de motivo desativado no cadastro ou que já não existe lá, por
    /// lugar e depois por código. A situação do motivo não depende do lugar: o que está numa fase ou etapa que a leitura recusou
    /// ou que o documento não traz vem depois dos lugares, por código, e a mensagem não nomeia o lugar.
    /// </summary>
    public IEnumerable<FieldError> Desativados(LugaresDaDecisao lugares)
    {
        ArgumentNullException.ThrowIfNull(lugares);

        IEnumerable<FieldError> nosLugares = NosLugares(lugares)
            .Where(n => EstaDesativado(n.Motivo))
            .Select(n => Recusa(MotivoDeDecisaoErrorCodes.MotivoDesativado,
                $"{n.Lugar.Descricao} tem o motivo {n.Motivo.Escolhido.Codigo.Valor}, desativado no cadastro depois de escolhido; troque-o antes de publicar."));
        IEnumerable<FieldError> foraDosLugares = PorCodigo(_escolha.Where(m => !lugares.Contem(m.Escolhido.Lugar) && EstaDesativado(m)))
            .Select(m => Recusa(MotivoDeDecisaoErrorCodes.MotivoDesativado,
                $"O motivo {m.Escolhido.Codigo.Valor} está desativado no cadastro depois de escolhido, e a fase ou a etapa dele não está na configuração lida; troque-o antes de publicar."));

        return nosLugares.Concat(foraDosLugares);
    }

    /// <summary>
    /// Uma recusa por motivo, pendente ou publicado, cujo lugar não declara o tipo de decisão dele, por lugar e depois por
    /// código. O lugar a que a leitura recusou parte da lista dos tipos não é julgado: o tipo do motivo pode ser o recusado.
    /// </summary>
    public IEnumerable<FieldError> DeTipoNaoDeclarado(LugaresDaDecisao lugares)
    {
        ArgumentNullException.ThrowIfNull(lugares);

        return NosLugares(lugares)
            .Where(n => !n.Lugar.TiposDeDecisaoComParteRecusada && !n.Lugar.TiposDeDecisao.Contains(n.Motivo.Escolhido.Tipo))
            .Select(n => Recusa(MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado, MensagemDoTipoNaoDeclarado(n.Lugar, n.Motivo.Escolhido)));
    }

    /// <summary>Os motivos que a versão que nasce leva e que valem para a cobertura: sem os desativados e sem os de tipo que o lugar não declara.</summary>
    public IReadOnlyCollection<MotivoDoProcesso> QueCompoemAVersao(LugaresDaDecisao lugares)
    {
        ArgumentNullException.ThrowIfNull(lugares);

        return
        [
            .. _escolha
                .Where(e => !EstaDesativado(e) && lugares.Declara(e.Escolhido.Lugar, e.Escolhido.Tipo))
                .Select(static e => e.Escolhido),
        ];
    }

    private bool EstaDesativado(MotivoEscolhidoComOrigem motivo) =>
        motivo.Escolhido.PendenteNa(_numeroDaVersaoQueNasce) && motivo.Origem is not { Ativo: true };

    private IEnumerable<(LugarNoProcesso Lugar, MotivoEscolhidoComOrigem Motivo)> NosLugares(LugaresDaDecisao lugares) =>
        from lugar in lugares.Lugares
        from motivo in PorCodigo(_escolha.Where(m => m.Escolhido.Lugar == lugar.Lugar))
        select (lugar, motivo);

    private static IOrderedEnumerable<MotivoEscolhidoComOrigem> PorCodigo(IEnumerable<MotivoEscolhidoComOrigem> motivos) =>
        motivos
            .OrderBy(static m => m.Escolhido.Codigo.Valor, StringComparer.Ordinal)
            .ThenBy(static m => m.Escolhido.Tipo.Codigo(), StringComparer.Ordinal);

    private string MensagemDoTipoNaoDeclarado(LugarNoProcesso lugar, MotivoDoProcesso escolhido)
    {
        string recusa = $"{lugar.Descricao} não declara o tipo de decisão {escolhido.Tipo.Codigo()} do motivo {escolhido.Codigo.Valor}";

        return escolhido.PendenteNa(_numeroDaVersaoQueNasce)
            ? $"{recusa}; declare o tipo ou retire o motivo."
            : $"{recusa}, que já consta do edital publicado e não pode ser retirado; declare o tipo de novo.";
    }

    // O bloco dos motivos é um arranjo sem índice por código: o campo aponta o bloco, e a mensagem diz o lugar e o código.
    private static FieldError Recusa(string codigo, string mensagem) =>
        new(CampoDosMotivos, new DomainError(codigo, mensagem));
}

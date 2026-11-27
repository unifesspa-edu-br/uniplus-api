namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O atendimento especializado do documento: as condições, os recursos de acessibilidade e os tipos de deficiência pelo código, cada
/// um lido do cadastro citado. A causa de um código que o cadastro não tem aponta o item na posição enviada, sem repetir o texto; o
/// que a entidade confere sobre os itens (repetição e tipo de deficiência sem a condição PcD) vale sobre os itens que o cadastro achou.
/// O atendimento é um bloco só: qualquer recusa o deixa fora.
/// </summary>
internal static class LeituraDoAtendimento
{
    /// <summary>O nome do bloco, que é o caminho da parte recusada.</summary>
    internal const string Caminho = "atendimento";

    internal const string ListaDasCondicoes = "atendimento.condicaoIds";
    internal const string ListaDosRecursos = "atendimento.recursoIds";
    internal const string ListaDosTiposDeDeficiencia = "atendimento.tipoDeficienciaIds";

    internal static AtendimentoLido Ler(AtendimentoNoDocumento? atendimento, CadastrosCitadosNoDocumento cadastros)
    {
        ArgumentNullException.ThrowIfNull(cadastros);

        return atendimento is null
            ? AtendimentoLido.Ausente
            : Montar(Condicoes(atendimento, cadastros), Recursos(atendimento, cadastros), TiposDeDeficiencia(atendimento, cadastros));
    }

    private static ItensDoAtendimento<CondicaoOfertada> Condicoes(AtendimentoNoDocumento atendimento, CadastrosCitadosNoDocumento cadastros) => Itens(
        atendimento.Condicoes,
        cadastros.CondicaoDeAtendimento,
        static opcao => CondicaoOfertada.Criar(opcao.OrigemId, opcao.Codigo, opcao.Nome),
        "condicoes",
        "condicaoCodigo",
        AtendimentoEspecializado.CondicaoNaoEncontrada);

    private static ItensDoAtendimento<RecursoOfertado> Recursos(AtendimentoNoDocumento atendimento, CadastrosCitadosNoDocumento cadastros) => Itens(
        atendimento.Recursos,
        cadastros.RecursoDeAcessibilidade,
        static opcao => RecursoOfertado.Criar(opcao.OrigemId, opcao.Codigo, opcao.Nome),
        "recursos",
        "recursoCodigo",
        AtendimentoEspecializado.RecursoNaoEncontrado);

    private static ItensDoAtendimento<DeficienciaAtendida> TiposDeDeficiencia(AtendimentoNoDocumento atendimento, CadastrosCitadosNoDocumento cadastros) => Itens(
        atendimento.TiposDeficiencia,
        cadastros.TipoDeDeficiencia,
        static opcao => DeficienciaAtendida.Criar(opcao.OrigemId, opcao.Codigo, opcao.Nome),
        "tiposDeficiencia",
        "tipoDeficienciaCodigo",
        AtendimentoEspecializado.TipoDeficienciaNaoEncontrado);

    private static AtendimentoLido Montar(
        ItensDoAtendimento<CondicaoOfertada> condicoes, ItensDoAtendimento<RecursoOfertado> recursos, ItensDoAtendimento<DeficienciaAtendida> tipos)
    {
        IReadOnlyList<ParteRecusada> partes = Recusadas(condicoes, recursos, tipos);
        if (partes.Count > 0)
        {
            return Recusado(partes);
        }

        Result<AtendimentoEspecializado> criado = AtendimentoEspecializado.Criar([.. condicoes.Itens], [.. recursos.Itens], [.. tipos.Itens]);

        return criado.IsSuccess
            ? new(criado.Value, condicoes.Posicoes, recursos.Posicoes, tipos.Posicoes, [])
            : Recusado(RecusasDasVagas.Partes(Caminho, 0, Caminho, criado.Errors));
    }

    // A repetição e a falta da condição PcD se leem sobre os itens que o cadastro achou e vêm antes da recusa do cadastro; a falta da
    // condição só vale com todas as condições resolvidas, e a recusa de um recurso não a esconde.
    private static IReadOnlyList<ParteRecusada> Recusadas(
        ItensDoAtendimento<CondicaoOfertada> condicoes, ItensDoAtendimento<RecursoOfertado> recursos, ItensDoAtendimento<DeficienciaAtendida> tipos)
    {
        List<FieldError> repeticoes = AtendimentoEspecializado.ValidarIdsUnicos([.. condicoes.Origens], [.. recursos.Origens], [.. tipos.Origens]);
        List<FieldError> semCondicaoPcd = condicoes.Recusas.Count == 0
            ? AtendimentoEspecializado.ValidarDeficienciaComCondicaoPcd([.. condicoes.Itens], [.. tipos.Itens])
            : [];

        return RecusasDasVagas.Partes(
            Caminho, 0, Caminho, [.. repeticoes, .. semCondicaoPcd, .. condicoes.Recusas, .. recursos.Recusas, .. tipos.Recusas]);
    }

    private static AtendimentoLido Recusado(IReadOnlyList<ParteRecusada> partes) => new(null, [], [], [], partes);

    private static ItensDoAtendimento<T> Itens<T>(
        IReadOnlyList<CodigoNoDocumento> codigos,
        Func<string?, OpcaoDeAtendimentoCitada?> buscar,
        Func<OpcaoDeAtendimentoCitada, T> criar,
        string lista,
        string campo,
        Func<DomainError> naoEncontrado)
    {
        (int Posicao, OpcaoDeAtendimentoCitada? Opcao)[] lidas = [.. codigos.Select(codigo => (codigo.PosicaoNoDocumento, buscar(codigo.Codigo)))];
        (int Posicao, OpcaoDeAtendimentoCitada Opcao)[] achadas = [.. lidas.Where(static lida => lida.Opcao is not null).Select(static lida => (lida.Posicao, lida.Opcao!))];

        return new(
            [.. achadas.Select(achada => criar(achada.Opcao))],
            [.. achadas.Select(static achada => achada.Opcao.OrigemId)],
            [.. achadas.Select(static achada => achada.Posicao)],
            [.. lidas.Where(static lida => lida.Opcao is null).Select(lida => new FieldError($"{lista}[{lida.Posicao}].{campo}", naoEncontrado()))]);
    }

    private sealed record ItensDoAtendimento<T>(
        IReadOnlyList<T> Itens, IReadOnlyList<Guid> Origens, IReadOnlyList<int> Posicoes, IReadOnlyList<FieldError> Recusas);
}

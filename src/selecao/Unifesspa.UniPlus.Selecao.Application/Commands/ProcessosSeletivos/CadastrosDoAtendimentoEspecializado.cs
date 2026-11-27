namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Monta o atendimento especializado do processo a partir dos cadastros vivos do módulo Configuração
/// (ADR-0056): lê cada condição, cada recurso e cada tipo de deficiência pelo identificador e congela por valor o
/// código e o nome (ADR-0061). Todas as recusas saem juntas (ADR-0125, item 5), na ordem da leitura do documento:
/// identificadores repetidos, tipo de deficiência sem a condição de pessoa com deficiência (só quando todas as
/// condições foram encontradas) e, por fim, cada identificador sem cadastro, em condições, recursos e tipos, no item
/// e na posição em que veio no pedido. A regra de que o tipo de deficiência só vale sob a condição de pessoa com
/// deficiência (ADR-0067) é do <see cref="AtendimentoEspecializado"/>.
/// </summary>
public sealed class CadastrosDoAtendimentoEspecializado(
    ICondicaoAtendimentoReader condicaoAtendimentoReader,
    IRecursoAcessibilidadeReader recursoAcessibilidadeReader,
    ITipoDeficienciaReader tipoDeficienciaReader)
{
    internal async Task<Result<AtendimentoEspecializado>> MontarAsync(
        DefinirAtendimentoEspecializadoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        CopiasDoCadastro<CondicaoOfertada> condicoes = await CondicoesOfertadasAsync(command.CondicaoIds, cancellationToken).ConfigureAwait(false);
        CopiasDoCadastro<RecursoOfertado> recursos = await RecursosOfertadosAsync(command.RecursoIds, cancellationToken).ConfigureAwait(false);
        CopiasDoCadastro<DeficienciaAtendida> tipos = await DeficienciasAtendidasAsync(command.TipoDeficienciaIds, cancellationToken).ConfigureAwait(false);

        return Montar(command, condicoes, recursos, tipos);
    }

    private Task<CopiasDoCadastro<CondicaoOfertada>> CondicoesOfertadasAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        CopiarAsync<CondicaoAtendimentoView, CondicaoOfertada>(
            ids,
            "condicaoIds",
            condicaoAtendimentoReader.ObterPorIdAsync,
            static condicao => CondicaoOfertada.Criar(condicao.Id, condicao.Codigo, condicao.Nome),
            AtendimentoEspecializado.CondicaoNaoEncontrada(),
            cancellationToken);

    private Task<CopiasDoCadastro<RecursoOfertado>> RecursosOfertadosAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        CopiarAsync<RecursoAcessibilidadeView, RecursoOfertado>(
            ids,
            "recursoIds",
            recursoAcessibilidadeReader.ObterPorIdAsync,
            static recurso => RecursoOfertado.Criar(recurso.Id, recurso.Codigo, recurso.Nome),
            AtendimentoEspecializado.RecursoNaoEncontrado(),
            cancellationToken);

    private Task<CopiasDoCadastro<DeficienciaAtendida>> DeficienciasAtendidasAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        CopiarAsync<TipoDeficienciaView, DeficienciaAtendida>(
            ids,
            "tipoDeficienciaIds",
            tipoDeficienciaReader.ObterPorIdAsync,
            static tipo => DeficienciaAtendida.Criar(tipo.Id, tipo.Codigo, tipo.Nome),
            AtendimentoEspecializado.TipoDeficienciaNaoEncontrado(),
            cancellationToken);

    private static Result<AtendimentoEspecializado> Montar(
        DefinirAtendimentoEspecializadoCommand command,
        CopiasDoCadastro<CondicaoOfertada> condicoes,
        CopiasDoCadastro<RecursoOfertado> recursos,
        CopiasDoCadastro<DeficienciaAtendida> tipos)
    {
        List<FieldError> recusas = AtendimentoEspecializado.ValidarIdsUnicos(command.CondicaoIds, command.RecursoIds, command.TipoDeficienciaIds);
        if (condicoes.Recusas.Count == 0)
        {
            recusas.AddRange(AtendimentoEspecializado.ValidarDeficienciaComCondicaoPcd(condicoes.Copias, tipos.Copias));
        }

        recusas.AddRange([.. condicoes.Recusas, .. recursos.Recusas, .. tipos.Recusas]);

        return recusas.Count > 0
            ? Result<AtendimentoEspecializado>.ValidationFailure(recusas)
            : AtendimentoEspecializado.Criar(condicoes.Copias, recursos.Copias, tipos.Copias);
    }

    private static async Task<CopiasDoCadastro<TCopia>> CopiarAsync<TView, TCopia>(
        IReadOnlyList<Guid> ids,
        string lista,
        Func<Guid, CancellationToken, Task<TView?>> obter,
        Func<TView, TCopia> copiar,
        DomainError naoEncontrado,
        CancellationToken cancellationToken)
        where TView : class
    {
        List<TCopia> copias = [];
        List<FieldError> recusas = [];
        for (int posicao = 0; posicao < ids.Count; posicao++)
        {
            TView? cadastrado = await obter(ids[posicao], cancellationToken).ConfigureAwait(false);
            if (cadastrado is null)
            {
                recusas.Add(new($"{lista}[{posicao}]", naoEncontrado));
                continue;
            }
            copias.Add(copiar(cadastrado));
        }
        return new(copias, recusas);
    }

    private sealed record CopiasDoCadastro<T>(IReadOnlyList<T> Copias, IReadOnlyList<FieldError> Recusas);
}

namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Monta o atendimento especializado do processo a partir dos cadastros vivos do módulo Configuração
/// (ADR-0056): lê cada condição, cada recurso e cada tipo de deficiência pelo identificador e congela por valor o
/// código e o nome (ADR-0061). A leitura segue nessa ordem e para na primeira ausência: os recursos só são lidos se
/// as condições foram encontradas, e os tipos de deficiência só se os recursos foram. A regra de que o tipo de
/// deficiência só vale sob a condição de pessoa com deficiência (ADR-0067) é do <see cref="AtendimentoEspecializado.Criar"/>.
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

        Result<IReadOnlyList<CondicaoOfertada>> condicoes = await CondicoesOfertadasAsync(command.CondicaoIds, cancellationToken).ConfigureAwait(false);
        return condicoes.IsFailure
            ? Result<AtendimentoEspecializado>.ValidationFailure(condicoes.Errors)
            : await MontarComRecursosAsync(command, condicoes.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AtendimentoEspecializado>> MontarComRecursosAsync(
        DefinirAtendimentoEspecializadoCommand command, IReadOnlyList<CondicaoOfertada> condicoes, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<RecursoOfertado>> recursos = await RecursosOfertadosAsync(command.RecursoIds, cancellationToken).ConfigureAwait(false);
        return recursos.IsFailure
            ? Result<AtendimentoEspecializado>.ValidationFailure(recursos.Errors)
            : await MontarComDeficienciasAsync(command, condicoes, recursos.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AtendimentoEspecializado>> MontarComDeficienciasAsync(
        DefinirAtendimentoEspecializadoCommand command,
        IReadOnlyList<CondicaoOfertada> condicoes,
        IReadOnlyList<RecursoOfertado> recursos,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<DeficienciaAtendida>> deficiencias = await DeficienciasAtendidasAsync(command.TipoDeficienciaIds, cancellationToken).ConfigureAwait(false);
        return deficiencias.IsFailure
            ? Result<AtendimentoEspecializado>.ValidationFailure(deficiencias.Errors)
            : AtendimentoEspecializado.Criar(condicoes, recursos, deficiencias.Value!);
    }

    private Task<Result<IReadOnlyList<CondicaoOfertada>>> CondicoesOfertadasAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        CopiarAsync<CondicaoAtendimentoView, CondicaoOfertada>(
            ids,
            condicaoAtendimentoReader.ObterPorIdAsync,
            static condicao => CondicaoOfertada.Criar(condicao.Id, condicao.Codigo, condicao.Nome),
            CondicaoNaoEncontrada,
            cancellationToken);

    private Task<Result<IReadOnlyList<RecursoOfertado>>> RecursosOfertadosAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        CopiarAsync<RecursoAcessibilidadeView, RecursoOfertado>(
            ids,
            recursoAcessibilidadeReader.ObterPorIdAsync,
            static recurso => RecursoOfertado.Criar(recurso.Id, recurso.Codigo, recurso.Nome),
            RecursoNaoEncontrado,
            cancellationToken);

    private Task<Result<IReadOnlyList<DeficienciaAtendida>>> DeficienciasAtendidasAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        CopiarAsync<TipoDeficienciaView, DeficienciaAtendida>(
            ids,
            tipoDeficienciaReader.ObterPorIdAsync,
            static tipo => DeficienciaAtendida.Criar(tipo.Id, tipo.Codigo, tipo.Nome),
            TipoDeficienciaNaoEncontrado,
            cancellationToken);

    private static async Task<Result<IReadOnlyList<TCopia>>> CopiarAsync<TView, TCopia>(
        IReadOnlyList<Guid> ids,
        Func<Guid, CancellationToken, Task<TView?>> obter,
        Func<TView, TCopia> copiar,
        Func<Guid, DomainError> naoEncontrado,
        CancellationToken cancellationToken)
        where TView : class
    {
        List<TCopia> copias = [];
        foreach (Guid id in ids)
        {
            TView? cadastrado = await obter(id, cancellationToken).ConfigureAwait(false);
            if (cadastrado is null)
            {
                return Result<IReadOnlyList<TCopia>>.Failure(naoEncontrado(id));
            }

            copias.Add(copiar(cadastrado));
        }

        return Result<IReadOnlyList<TCopia>>.Success(copias);
    }

    private static DomainError CondicaoNaoEncontrada(Guid condicaoId) => new(
        "AtendimentoEspecializado.CondicaoNaoEncontrada",
        $"Condição de atendimento {condicaoId} não encontrada ou não está mais viva.");

    private static DomainError RecursoNaoEncontrado(Guid recursoId) => new(
        "AtendimentoEspecializado.RecursoNaoEncontrado",
        $"Recurso de acessibilidade {recursoId} não encontrado ou não está mais vivo.");

    private static DomainError TipoDeficienciaNaoEncontrado(Guid tipoDeficienciaId) => new(
        "AtendimentoEspecializado.TipoDeficienciaNaoEncontrado",
        $"Tipo de deficiência {tipoDeficienciaId} não encontrado ou não está mais vivo.");
}

namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.ProcessosSeletivos.Recursos;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Monta os procedimentos de recurso que uma fase ou uma etapa declara a partir da entrada: o tipo de decisão do vocabulário, o
/// alvo e a regra resolvida no catálogo, e depois os confere contra o lugar que os declara. As recusas se acumulam por
/// posição de entrada e a referência da regra sai dos valores resolvidos do catálogo, nunca ecoados do payload.
/// </summary>
internal sealed class DeclaracaoDosProcedimentosDeRecurso(
    Func<string, string, CancellationToken, Task<RegraCatalogo?>> lerRegra,
    CodigosDoProcedimentoDeRecurso codigos)
{
    public async Task<Result<ProcedimentosDeRecurso>> DeclararAsync(
        IReadOnlyList<ProcedimentoDeRecursoInput>? entradas,
        ContextoDosProcedimentosDeRecurso contexto,
        string campo,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Result<ProcedimentoDeRecursoDeclarado>> lidos = await DeclararProcedimentosAsync(entradas, campo, cancellationToken).ConfigureAwait(false);
        int[] posicoesDeEntrada = [.. Enumerable.Range(0, lidos.Count).Where(posicao => lidos[posicao].IsSuccess)];
        List<ProcedimentoDeRecursoDeclarado> declarados = [.. posicoesDeEntrada.Select(posicao => lidos[posicao].Value!)];
        List<FieldError> recusas = [.. lidos.SelectMany(static lido => lido.Errors)];
        recusas.AddRange(ConferenciaDosProcedimentosDeRecurso.Faltas(declarados, contexto, codigos, campo, posicoesDeEntrada));

        return recusas.Count > 0
            ? Result<ProcedimentosDeRecurso>.ValidationFailure(recusas)
            : Result<ProcedimentosDeRecurso>.Success(ProcedimentosDeRecurso.De(declarados));
    }

    private async Task<IReadOnlyList<Result<ProcedimentoDeRecursoDeclarado>>> DeclararProcedimentosAsync(
        IReadOnlyList<ProcedimentoDeRecursoInput>? entradas, string campo, CancellationToken cancellationToken)
    {
        List<Result<ProcedimentoDeRecursoDeclarado>> lidos = [];
        for (int posicao = 0; posicao < (entradas?.Count ?? 0); posicao++)
        {
            lidos.Add(await DeclararProcedimentoAsync(entradas![posicao], $"{campo}[{posicao}]", cancellationToken).ConfigureAwait(false));
        }

        return lidos;
    }

    private async Task<Result<ProcedimentoDeRecursoDeclarado>> DeclararProcedimentoAsync(
        ProcedimentoDeRecursoInput entrada, string campo, CancellationToken cancellationToken)
    {
        TipoMotivoDeDecisao tipo = TipoMotivoDeDecisaoCodigo.FromCodigo(entrada.TipoDeDecisao);
        Result<AlvoDoProcedimentoDeRecurso?> alvo = Alvo(entrada.Alvo, campo);
        Result<ReferenciaRegra> regra = await ReferenciaAsync(entrada, campo, cancellationToken).ConfigureAwait(false);

        List<FieldError> recusas = [.. RecusaDoTipo(tipo, campo), .. alvo.Errors, .. regra.Errors];

        return recusas.Count > 0
            ? Result<ProcedimentoDeRecursoDeclarado>.ValidationFailure(recusas)
            : Result<ProcedimentoDeRecursoDeclarado>.Success(ProcedimentoDeRecursoDeclarado.De(tipo, alvo.Value, regra.Value!));
    }

    private static IEnumerable<FieldError> RecusaDoTipo(TipoMotivoDeDecisao tipo, string campo) =>
        tipo == TipoMotivoDeDecisao.Nenhum
            ? [new FieldError($"{campo}.tipoDeDecisao", new DomainError(
                MotivoDeDecisaoErrorCodes.TipoInvalido,
                $"O tipo informado não pertence ao vocabulário de decisões. Tipos aceitos: {TipoMotivoDeDecisaoCodigo.CodigosEmTexto}."))]
            : [];

    private Result<AlvoDoProcedimentoDeRecurso?> Alvo(AlvoDoProcedimentoDeRecursoInput? entrada, string campo) => entrada switch
    {
        null => Result<AlvoDoProcedimentoDeRecurso?>.Success(null),
        { ItemDeAvaliacaoId: { } item, AreaDoEnem: var area } when string.IsNullOrWhiteSpace(area) =>
            Result<AlvoDoProcedimentoDeRecurso?>.Success(AlvoDoProcedimentoDeRecurso.DoItem(item)),
        { ItemDeAvaliacaoId: null, AreaDoEnem: { } area } when !string.IsNullOrWhiteSpace(area) =>
            Result<AlvoDoProcedimentoDeRecurso?>.Success(AlvoDoProcedimentoDeRecurso.DaArea(area)),
        _ => AlvoSemUmUnicoDestino(campo),
    };

    private Result<AlvoDoProcedimentoDeRecurso?> AlvoSemUmUnicoDestino(string campo) =>
        Result<AlvoDoProcedimentoDeRecurso?>.ValidationFailure([new($"{campo}.alvo", new DomainError(
            codigos.AlvoInexistente,
            "Informe o item de avaliação ou a área do ENEM como alvo do procedimento, e só um dos dois."))]);

    // A referência é montada a partir dos valores RESOLVIDOS do catálogo, nunca ecoados do payload, e a recusa é de
    // validação, nunca exceção: catálogo que não respondeu chega aqui com código em branco, e o cliente precisa da
    // mensagem, não de um 500.
    private async Task<Result<ReferenciaRegra>> ReferenciaAsync(
        ProcedimentoDeRecursoInput entrada, string campo, CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = string.IsNullOrWhiteSpace(entrada.RegraCodigo) || string.IsNullOrWhiteSpace(entrada.RegraVersao)
            ? null
            : await lerRegra(entrada.RegraCodigo, entrada.RegraVersao, cancellationToken).ConfigureAwait(false);

        return regra is { Tipo: TipoRegra.RegraProcedimentoRecurso }
            ? Result<ReferenciaRegra>.Success(regra.Referencia())
            : Result<ReferenciaRegra>.ValidationFailure([new($"{campo}.regra", new DomainError(
                codigos.Desconhecido,
                $"O procedimento de recurso informado, ou a versão dele, não consta do catálogo. Procedimentos aceitos: {string.Join(", ", RegraProcedimentoRecursoCodigo.Rol)}."))]);
    }
}

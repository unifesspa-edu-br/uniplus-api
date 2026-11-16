namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.ProcessosSeletivos.Recursos;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Monta os procedimentos de recurso que uma janela declara a partir da entrada: o tipo de decisão do vocabulário, o
/// alvo e a regra resolvida no catálogo, e depois os confere contra o lugar da janela. As recusas se acumulam por
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
        List<FieldError> recusas = [];
        List<ProcedimentoDeRecursoDeclarado> declarados = [];
        List<int> posicoesDeEntrada = [];
        for (int posicao = 0; posicao < (entradas?.Count ?? 0); posicao++)
        {
            Result<ProcedimentoDeRecursoDeclarado> declarado = await DeclararUmAsync(
                entradas![posicao], $"{campo}[{posicao}]", cancellationToken).ConfigureAwait(false);
            if (declarado.IsFailure)
            {
                recusas.AddRange(declarado.Errors);
                continue;
            }

            declarados.Add(declarado.Value!);
            posicoesDeEntrada.Add(posicao);
        }

        recusas.AddRange(ConferenciaDosProcedimentosDeRecurso.Faltas(declarados, contexto, codigos, campo, posicoesDeEntrada));

        return recusas.Count > 0
            ? Result<ProcedimentosDeRecurso>.ValidationFailure(recusas)
            : Result<ProcedimentosDeRecurso>.Success(ProcedimentosDeRecurso.De(declarados));
    }

    private async Task<Result<ProcedimentoDeRecursoDeclarado>> DeclararUmAsync(
        ProcedimentoDeRecursoInput entrada, string campo, CancellationToken cancellationToken)
    {
        TipoMotivoDeDecisao tipo = TipoMotivoDeDecisaoCodigo.FromCodigo(entrada.TipoDeDecisao);
        Result<AlvoDoProcedimentoDeRecurso?> alvo = Alvo(entrada.Alvo, campo);
        Result<ReferenciaRegra> regra = await ReferenciaAsync(entrada, campo, cancellationToken).ConfigureAwait(false);

        List<FieldError> recusas = [];
        if (tipo == TipoMotivoDeDecisao.Nenhum)
        {
            recusas.Add(TipoInvalido(campo));
        }

        recusas.AddRange(alvo.Errors);
        recusas.AddRange(regra.Errors);

        return recusas.Count > 0
            ? Result<ProcedimentoDeRecursoDeclarado>.ValidationFailure(recusas)
            : Result<ProcedimentoDeRecursoDeclarado>.Success(ProcedimentoDeRecursoDeclarado.De(tipo, alvo.Value, regra.Value!));
    }

    private static FieldError TipoInvalido(string campo) => new($"{campo}.tipoDeDecisao", new DomainError(
        MotivoDeDecisaoErrorCodes.TipoInvalido,
        $"O tipo informado não pertence ao vocabulário de decisões. Tipos aceitos: {TipoMotivoDeDecisaoCodigo.CodigosEmTexto}."));

    private Result<AlvoDoProcedimentoDeRecurso?> Alvo(AlvoDoProcedimentoDeRecursoInput? entrada, string campo)
    {
        if (entrada is null)
        {
            return Result<AlvoDoProcedimentoDeRecurso?>.Success(null);
        }

        bool temItem = entrada.ItemDeAvaliacaoId is not null;
        bool temArea = !string.IsNullOrWhiteSpace(entrada.AreaDoEnem);
        if (temItem == temArea)
        {
            return Result<AlvoDoProcedimentoDeRecurso?>.ValidationFailure([new($"{campo}.alvo", new DomainError(
                codigos.AlvoInexistente,
                "Informe o item de avaliação ou a área do ENEM como alvo do procedimento, e só um dos dois."))]);
        }

        return Result<AlvoDoProcedimentoDeRecurso?>.Success(temItem
            ? AlvoDoProcedimentoDeRecurso.DoItem(entrada.ItemDeAvaliacaoId!.Value)
            : AlvoDoProcedimentoDeRecurso.DaArea(entrada.AreaDoEnem!));
    }

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
                $"A regra {entrada.RegraCodigo}/{entrada.RegraVersao} não é um procedimento de recurso do catálogo. Procedimentos aceitos: {string.Join(", ", RegraProcedimentoRecursoCodigo.Rol)}."))]);
    }
}

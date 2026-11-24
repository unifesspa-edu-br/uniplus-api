namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A declaração de procedimentos de recurso conferida contra o lugar da janela: regra do rol, tipo de decisão que o
/// lugar declara, alvo que existe, e a coerência entre as declarações. As recusas se acumulam por posição de entrada
/// (<c>{campo}[i]</c>) e a conferência não altera nada. Quando só parte das entradas virou declaração, as posições de
/// entrada dizem a que posição cada declaração corresponde.
/// </summary>
public static class ConferenciaDosProcedimentosDeRecurso
{
    public const string Campo = "procedimentosDeRecurso";

    public static IReadOnlyList<FieldError> Faltas(
        IReadOnlyList<ProcedimentoDeRecursoDeclarado> declarados,
        ContextoDosProcedimentosDeRecurso contexto,
        CodigosDoProcedimentoDeRecurso codigos,
        string campo = Campo,
        IReadOnlyList<int>? posicoesDeEntrada = null)
    {
        ArgumentNullException.ThrowIfNull(declarados);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(codigos);

        return [.. Enumerable.Range(0, declarados.Count).SelectMany(posicao =>
            FaltasDaPosicao(declarados, posicao, contexto, codigos, $"{campo}[{posicoesDeEntrada?[posicao] ?? posicao}]"))];
    }

    /// <summary>
    /// Só a coerência da declaração, sem o lugar da janela: a mesma matéria repetida e o tipo coberto com e sem alvo.
    /// É o que se pode provar de uma declaração que volta do envelope congelado, em que o lugar já foi conferido na
    /// gravação e não se reinterpreta contra o que a fase ou a etapa declaram hoje.
    /// </summary>
    public static IReadOnlyList<FieldError> FaltasDeCoerencia(
        IReadOnlyList<ProcedimentoDeRecursoDeclarado> declarados, CodigosDoProcedimentoDeRecurso codigos, string campo = Campo)
    {
        ArgumentNullException.ThrowIfNull(declarados);
        ArgumentNullException.ThrowIfNull(codigos);

        return [.. Enumerable.Range(0, declarados.Count)
            .Select(posicao => FaltaDeCoerencia(declarados, posicao, codigos, $"{campo}[{posicao}]"))
            .OfType<FieldError>()];
    }

    private static IEnumerable<FieldError> FaltasDaPosicao(
        IReadOnlyList<ProcedimentoDeRecursoDeclarado> declarados,
        int posicao,
        ContextoDosProcedimentosDeRecurso contexto,
        CodigosDoProcedimentoDeRecurso codigos,
        string campo)
    {
        ProcedimentoDeRecursoDeclarado atual = declarados[posicao];
        FieldError?[] faltas =
        [
            FaltaDoTipo(atual, contexto, codigos, campo),
            FaltaDoAlvo(atual, contexto, codigos, campo),
            FaltaDaRegra(atual, codigos, campo),
            FaltaDeCoerencia(declarados, posicao, codigos, campo),
        ];

        return faltas.OfType<FieldError>();
    }

    private static FieldError? FaltaDaRegra(ProcedimentoDeRecursoDeclarado procedimento, CodigosDoProcedimentoDeRecurso codigos, string campo) =>
        CatalogoDe<RegraDeProcedimentoDoRecurso>.TentarObter(procedimento.Regra.Codigo, out _)
            ? null
            : new FieldError($"{campo}.regra", ProcedimentoForaDoCatalogo(codigos));

    /// <summary>
    /// A recusa da regra que não é um procedimento de recurso do catálogo, a mesma para quem grava a declaração, a confere e a lê
    /// do documento; lista os procedimentos aceitos e não repete o que foi escrito.
    /// </summary>
    public static DomainError ProcedimentoForaDoCatalogo(CodigosDoProcedimentoDeRecurso codigos)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        return new(
            codigos.Desconhecido,
            $"O procedimento de recurso informado, ou a versão dele, não consta do catálogo. Procedimentos aceitos: {string.Join(", ", CatalogoDe<RegraDeProcedimentoDoRecurso>.Todos().Select(static item => item.Codigo))}.");
    }

    private static FieldError? FaltaDoTipo(
        ProcedimentoDeRecursoDeclarado procedimento, ContextoDosProcedimentosDeRecurso contexto, CodigosDoProcedimentoDeRecurso codigos, string campo) =>
        contexto.TiposDeDecisaoDoLugar is not { } tipos || tipos.Contains(procedimento.TipoDeDecisao)
            ? null
            : new FieldError($"{campo}.tipoDeDecisao", new DomainError(
                codigos.ParaDecisaoNaoDeclarada,
                "O tipo de decisão não é declarado por quem produz a decisão recorrida; declare o tipo na fase ou na etapa antes do procedimento."));

    private static FieldError? FaltaDoAlvo(
        ProcedimentoDeRecursoDeclarado procedimento, ContextoDosProcedimentosDeRecurso contexto, CodigosDoProcedimentoDeRecurso codigos, string campo) =>
        procedimento.Alvo is not { } alvo || AlvoExiste(alvo, contexto)
            ? null
            : new FieldError($"{campo}.alvo", AlvoInexistente(codigos));

    /// <summary>A recusa do alvo que não existe, a mesma para quem lê o documento e para quem confere a declaração; não repete o que foi escrito.</summary>
    public static DomainError AlvoInexistente(CodigosDoProcedimentoDeRecurso codigos)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        return new(
            codigos.AlvoInexistente,
            "O alvo do procedimento não existe: o item de avaliação deve ser da etapa que produz a decisão e a área do ENEM deve ser de um processo que importa a nota do ENEM.");
    }

    private static bool AlvoExiste(AlvoDoProcedimentoDeRecurso alvo, ContextoDosProcedimentosDeRecurso contexto) => alvo.Tipo switch
    {
        TipoDeAlvoDoProcedimentoDeRecurso.ItemDeAvaliacao =>
            Guid.TryParseExact(alvo.Valor, "D", out Guid id) && contexto.ItensDeAvaliacao.Contains(id),
        TipoDeAlvoDoProcedimentoDeRecurso.AreaDoEnem =>
            contexto.ProcessoTemNotaDoEnem && AreaDoEnem.Todas.Contains(alvo.Valor, StringComparer.Ordinal),
        _ => false,
    };

    /// <summary>
    /// A coerência da declaração na posição com as anteriores: a mesma matéria repetida é duplicada; o tipo coberto
    /// por declaração sem alvo e por declaração com alvo é conflitante. Só a posição que fecha o par é recusada.
    /// </summary>
    private static FieldError? FaltaDeCoerencia(
        IReadOnlyList<ProcedimentoDeRecursoDeclarado> declarados, int posicao, CodigosDoProcedimentoDeRecurso codigos, string campo)
    {
        ProcedimentoDeRecursoDeclarado atual = declarados[posicao];
        List<ProcedimentoDeRecursoDeclarado> anteriores = [.. declarados.Take(posicao).Where(p => p.TipoDeDecisao == atual.TipoDeDecisao)];
        string tipo = atual.TipoDeDecisao.Codigo();

        if (anteriores.Exists(atual.CobreAMesmaMateria))
        {
            return new FieldError(campo, new DomainError(
                codigos.Duplicado, $"A matéria do tipo '{tipo}' já tem procedimento declarado; cada matéria tem um único procedimento."));
        }

        return anteriores.Exists(p => (p.Alvo is null) != (atual.Alvo is null))
            ? new FieldError(campo, new DomainError(
                codigos.Conflitante, $"O tipo '{tipo}' tem procedimento sem alvo e com alvo ao mesmo tempo; declare um para o tipo inteiro ou um por alvo."))
            : null;
    }
}

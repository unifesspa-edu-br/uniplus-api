namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// As fases de uma edição do cronograma enquanto são montadas a partir do payload, resolvendo no cadastro vivo a
/// fase canônica, a unidade responsável, as bancas, os produtos e a regra de recurso (ADR-0125). Guarda o que a
/// edição acumula, as fases montadas e as recusas, e por isso vive só pela duração dela.
/// </summary>
/// <remarks>
/// Não há passada pura pré-I/O: a data de origem é cópia da fase canônica, o papel de cada produto depende
/// do catálogo de tipos de ato e o recorte de competência da banca depende do cadastro de categorias. As
/// recusas de produto e as da fábrica da fase acumulam, e a recusa do domínio sai como lote, com o campo de
/// cada violação. Os insumos que faltam inteiro interrompem a montagem, levando junto o que já foi
/// acumulado: parar é decidir o que ainda dá para avaliar, não apagar defeito já diagnosticado. A ordem
/// continua só no domínio, coberta pelo FluentValidation, e a janela (fim depois do início) acumula na
/// fábrica da fase, para a resposta trazê-la junto das outras violações da mesma fase.
/// </remarks>
internal sealed class FasesEmMontagem
{
    private readonly CadastrosDasFases _cadastros;
    private readonly MontagemDasBancasDaFase _bancas;
    private readonly MontagemDosProdutosDaFase _produtos;
    private readonly MontagemDaRegraDeRecursoDaFase _regraDeRecurso;
    private readonly List<FieldError> _recusas = [];
    private readonly List<FaseCronograma> _fases = [];

    public FasesEmMontagem(ProcessoSeletivo processo, CadastrosDasFases cadastros, AtosConsultadosNaEdicao atos)
    {
        _cadastros = cadastros;
        _bancas = new MontagemDasBancasDaFase(cadastros);
        _produtos = new MontagemDosProdutosDaFase(atos);
        _regraDeRecurso = new MontagemDaRegraDeRecursoDaFase(cadastros.Regras, atos, AncorasJaDeclaradas(processo));
    }

    public async Task<Result<List<FaseCronograma>>> MontarAsync(
        IReadOnlyList<FaseCronogramaInput> fases, CancellationToken cancellationToken)
    {
        for (int indice = 0; indice < fases.Count; indice++)
        {
            Result interrupcao = await MontarFaseAsync(fases[indice], indice, cancellationToken).ConfigureAwait(false);
            if (interrupcao.IsFailure)
            {
                return Result<List<FaseCronograma>>.ValidationFailure([.. _recusas, .. interrupcao.Errors]);
            }
        }

        return _recusas.Count > 0
            ? Result<List<FaseCronograma>>.ValidationFailure(_recusas)
            : Result<List<FaseCronograma>>.Success(_fases);
    }

    // As âncoras que o cronograma já tinha antes desta gravação: o cliente devolve o que leu, e uma âncora
    // que já estava no agregado não é revista pelo que o catálogo diz hoje do ato.
    private static HashSet<(string Fase, string Ato)> AncorasJaDeclaradas(ProcessoSeletivo processo) =>
        [.. processo.CronogramaFases.SelectMany(fase => fase.Produtos
            .Where(p => fase.RegraRecurso is { } regra && p.Id == regra.ProdutoAncoraId)
            .Select(p => (fase.Codigo, p.AtoCodigo)))];

    private async Task<Result> MontarFaseAsync(FaseCronogramaInput input, int indice, CancellationToken cancellationToken)
    {
        Result<InsumosDaFase> insumos = await ResolverInsumosAsync(input, indice, cancellationToken).ConfigureAwait(false);
        return insumos.IsFailure
            ? Result.ValidationFailure(insumos.Errors)
            : await MontarComInsumosAsync(input, indice, insumos.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> MontarComInsumosAsync(
        FaseCronogramaInput input, int indice, InsumosDaFase insumos, CancellationToken cancellationToken)
    {
        Result<ProdutosDaFaseMontados> produtos = await _produtos
            .MontarAsync(input, indice, insumos.Canonica.Codigo, cancellationToken).ConfigureAwait(false);
        return produtos.IsFailure
            ? Result.ValidationFailure(produtos.Errors)
            : await MontarComProdutosAsync(input, indice, insumos, produtos.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> MontarComProdutosAsync(
        FaseCronogramaInput input, int indice, InsumosDaFase insumos, ProdutosDaFaseMontados produtos, CancellationToken cancellationToken)
    {
        _recusas.AddRange(produtos.Recusas);
        Result<RegraRecursoFase?> regra = await RegraDeRecursoAsync(input, indice, insumos.Canonica.Codigo, produtos.Produtos, cancellationToken)
            .ConfigureAwait(false);
        if (regra.IsFailure)
        {
            return Result.ValidationFailure(regra.Errors);
        }

        if (produtos.Integros)
        {
            CriarFase(input, indice, insumos, produtos.Produtos, regra.Value);
        }

        return Result.Success();
    }

    private async Task<Result<RegraRecursoFase?>> RegraDeRecursoAsync(
        FaseCronogramaInput input, int indice, string faseCodigo, List<ProdutoDaFase> produtos, CancellationToken cancellationToken)
    {
        if (input.RegraRecurso is not { } declarada)
        {
            return Result<RegraRecursoFase?>.Success(null);
        }

        Result<RegraRecursoFase> regra = await _regraDeRecurso
            .MontarAsync(declarada, indice, faseCodigo, produtos, cancellationToken).ConfigureAwait(false);
        return regra.IsFailure
            ? Result<RegraRecursoFase?>.ValidationFailure(regra.Errors)
            : Result<RegraRecursoFase?>.Success(regra.Value);
    }

    private async Task<Result<InsumosDaFase>> ResolverInsumosAsync(
        FaseCronogramaInput input, int indice, CancellationToken cancellationToken)
    {
        FaseCanonicaView? canonica = await _cadastros.FasesCanonicas
            .ObterPorIdAsync(input.FaseCanonicaId, cancellationToken).ConfigureAwait(false);
        return canonica is null
            ? InterrupcaoDaFase.Com<InsumosDaFase>($"fases[{indice}].faseCanonicaId", new DomainError(
                "FaseCronograma.FaseCanonicaNaoEncontrada",
                $"Fase canônica {input.FaseCanonicaId} não encontrada ou não está mais viva."))
            : await ResolverInsumosDaFaseAsync(input, indice, canonica, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<InsumosDaFase>> ResolverInsumosDaFaseAsync(
        FaseCronogramaInput input, int indice, FaseCanonicaView canonica, CancellationToken cancellationToken)
    {
        Result<UnidadeResponsavelSnapshot?> unidade = await UnidadeAsync(input.UnidadeResponsavelId, indice, canonica, cancellationToken).ConfigureAwait(false);
        if (unidade.IsFailure)
        {
            return Result<InsumosDaFase>.ValidationFailure(unidade.Errors);
        }

        Result<List<BancaRequerida>> bancas = await _bancas.MontarAsync(input, indice, cancellationToken).ConfigureAwait(false);
        return bancas.IsFailure
            ? Result<InsumosDaFase>.ValidationFailure(bancas.Errors)
            : Result<InsumosDaFase>.Success(new InsumosDaFase(canonica, unidade.Value, bancas.Value!));
    }

    // A unidade é resolvida no cadastro vivo a cada definição; a fase sem unidade declarada não consulta o cadastro.
    private async Task<Result<UnidadeResponsavelSnapshot?>> UnidadeAsync(
        Guid? unidadeId, int indice, FaseCanonicaView canonica, CancellationToken cancellationToken)
    {
        if (unidadeId is not { } id)
        {
            return Result<UnidadeResponsavelSnapshot?>.Success(null);
        }

        UnidadeResponsavelSnapshot? unidade = await _cadastros.Unidades.ObterAsync(id, cancellationToken).ConfigureAwait(false);
        return unidade is null
            ? InterrupcaoDaFase.Com<UnidadeResponsavelSnapshot?>($"fases[{indice}].unidadeResponsavelId", new DomainError(
                "FaseCronograma.UnidadeResponsavelNaoEncontrada",
                $"Unidade responsável {id} da fase '{canonica.Codigo}' não encontrada ou não está mais viva."))
            : Result<UnidadeResponsavelSnapshot?>.Success(unidade);
    }

    private void CriarFase(
        FaseCronogramaInput input, int indice, InsumosDaFase insumos, List<ProdutoDaFase> produtos, RegraRecursoFase? regraRecurso)
    {
        FaseCanonicaView canonica = insumos.Canonica;
        Result<FaseCronograma> fase = FaseCronograma.Criar(
            input.Ordem, canonica.Id, canonica.Codigo, OrigemDataFaseCodigo.FromCodigo(canonica.OrigemData),
            canonica.AgrupaEtapas, canonica.PermiteComplementacao, canonica.ColetaInscricao, canonica.ColetaSolicitacaoIsencao,
            input.Inicio, input.Fim, produtos, input.FaseConcluinteCodigo, input.EmiteParecerIndividual,
            insumos.Bancas, regraRecurso, insumos.Unidade);
        Acumular(fase, indice);
    }

    private void Acumular(Result<FaseCronograma> fase, int indice)
    {
        if (fase.IsSuccess)
        {
            _fases.Add(fase.Value!);
            return;
        }

        _recusas.AddRange(fase.Errors.Select(erro => erro with
        {
            Field = erro.Field is null ? $"fases[{indice}]" : $"fases[{indice}].{erro.Field}",
        }));
    }
}

/// <summary>O que a fase precisa do cadastro antes de seus produtos: a fase canônica, a unidade e as bancas.</summary>
internal sealed record InsumosDaFase(FaseCanonicaView Canonica, UnidadeResponsavelSnapshot? Unidade, List<BancaRequerida> Bancas);

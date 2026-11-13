namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.ProcessosSeletivos.Bancas;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Monta as bancas requeridas de uma fase. O tipo de banca e o recorte de competência são resolvidos no
/// cadastro vivo e congelados por valor: uma categoria que saiu do cadastro não pode entrar no edital,
/// porque o recorte congelado responde por parecer e recurso do certame inteiro. As violações da banca
/// de uma mesma fase acumulam: o que falta numa não esconde o que falta nas outras.
/// </summary>
internal sealed class MontagemDasBancasDaFase(CadastrosDasFases cadastros)
{
    public async Task<Result<List<BancaRequerida>>> MontarAsync(
        FaseCronogramaInput fase, int indice, CancellationToken cancellationToken)
    {
        RecusasAcumuladas<BancaRequerida> bancas = new();
        for (int posicao = 0; posicao < fase.BancasRequeridas.Count; posicao++)
        {
            string campo = $"fases[{indice}].bancasRequeridas[{posicao}]";
            bancas.Juntar(await BancaAsync(fase.BancasRequeridas[posicao], campo, cancellationToken).ConfigureAwait(false));
        }

        return bancas.Resultado();
    }

    private async Task<Result<BancaRequerida>> BancaAsync(
        BancaRequeridaInput declarada, string campo, CancellationToken cancellationToken)
    {
        TipoBancaView? tipo = await cadastros.TiposDeBanca.ObterPorIdAsync(declarada.TipoBancaId, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return InterrupcaoDaFase.Com<BancaRequerida>($"{campo}.tipoBancaId", new DomainError(
                "FaseCronograma.TipoBancaNaoEncontrado",
                $"Tipo de banca {declarada.TipoBancaId} não encontrado ou não está mais vivo."));
        }

        Result<List<CategoriaJulgada>> recorte = await RecorteAsync(declarada, campo, cancellationToken).ConfigureAwait(false);
        return recorte.IsFailure
            ? Result<BancaRequerida>.ValidationFailure(recorte.Errors)
            : Montada(declarada, tipo, recorte.Value!, campo);
    }

    private static Result<BancaRequerida> Montada(
        BancaRequeridaInput declarada, TipoBancaView tipo, List<CategoriaJulgada> recorte, string campo)
    {
        Result<RegraDeDeliberacao?> regra = MontagemDaRegraDeDeliberacao.Montar(declarada.RegraDeDeliberacao, campo);
        List<FieldError> recusas = [.. regra.Errors];
        if (ExigenciaDeBancaDeRecurso.RecorteNaDeclaracao(tipo.Codigo, recorte.Count) is { } recorteIndevido)
        {
            recusas.Add(new FieldError($"{campo}.categoriasDocumentoIds", recorteIndevido));
        }

        return recusas.Count > 0
            ? Result<BancaRequerida>.ValidationFailure(recusas)
            : Result<BancaRequerida>.Success(BancaRequerida.Criar(tipo.Id, tipo.Codigo, tipo.Nome, recorte, regra.Value));
    }

    private async Task<Result<List<CategoriaJulgada>>> RecorteAsync(
        BancaRequeridaInput declarada, string campo, CancellationToken cancellationToken)
    {
        List<CategoriaJulgada> recorte = [];
        foreach (Guid categoriaId in declarada.CategoriasDocumentoIds)
        {
            CategoriaDocumentoView? categoria = await cadastros.Categorias.ObterAsync(categoriaId, cancellationToken).ConfigureAwait(false);
            if (categoria is null)
            {
                return CategoriaNaoEncontrada(categoriaId, campo);
            }

            recorte.Add(CategoriaJulgada.Criar(categoria.Id, categoria.Codigo));
        }

        return Result<List<CategoriaJulgada>>.Success(recorte);
    }

    private static Result<List<CategoriaJulgada>> CategoriaNaoEncontrada(Guid categoriaId, string campo) =>
        InterrupcaoDaFase.Com<List<CategoriaJulgada>>($"{campo}.categoriasDocumentoIds", new DomainError(
            "FaseCronograma.CategoriaDocumentoNaoEncontrada",
            $"Categoria de documento {categoriaId} não encontrada ou não está mais viva."));
}

namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Monta as bancas requeridas de uma fase. O tipo de banca e o recorte de competência são resolvidos no
/// cadastro vivo e congelados por valor: uma categoria que saiu do cadastro não pode entrar no edital,
/// porque o recorte congelado responde por parecer e recurso do certame inteiro.
/// </summary>
internal sealed class MontagemDasBancasDaFase(CadastrosDasFases cadastros)
{
    public async Task<Result<List<BancaRequerida>>> MontarAsync(
        FaseCronogramaInput fase, int indice, CancellationToken cancellationToken)
    {
        List<BancaRequerida> bancas = [];
        for (int posicao = 0; posicao < fase.BancasRequeridas.Count; posicao++)
        {
            string campo = $"fases[{indice}].bancasRequeridas[{posicao}]";
            Result<BancaRequerida> banca = await BancaAsync(fase.BancasRequeridas[posicao], campo, cancellationToken).ConfigureAwait(false);
            if (banca.IsFailure)
            {
                return Result<List<BancaRequerida>>.ValidationFailure(banca.Errors);
            }

            bancas.Add(banca.Value!);
        }

        return Result<List<BancaRequerida>>.Success(bancas);
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
            : Result<BancaRequerida>.Success(BancaRequerida.Criar(tipo.Id, tipo.Codigo, recorte.Value!));
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
                return InterrupcaoDaFase.Com<List<CategoriaJulgada>>($"{campo}.categoriasDocumentoIds", new DomainError(
                    "FaseCronograma.CategoriaDocumentoNaoEncontrada",
                    $"Categoria de documento {categoriaId} não encontrada ou não está mais viva."));
            }

            recorte.Add(CategoriaJulgada.Criar(categoria.Id, categoria.Codigo));
        }

        return Result<List<CategoriaJulgada>>.Success(recorte);
    }
}

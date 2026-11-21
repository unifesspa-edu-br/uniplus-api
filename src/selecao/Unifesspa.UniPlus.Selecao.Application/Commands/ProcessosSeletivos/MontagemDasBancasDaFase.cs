namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Monta as bancas requeridas de uma fase. O tipo de banca e os candidatos atendidos são resolvidos no
/// cadastro vivo e congelados por valor: uma categoria que saiu do cadastro não pode entrar no edital,
/// porque os candidatos atendidos congelados respondem por parecer e recurso do certame inteiro. As violações da banca
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
            return InterrupcaoDaEdicao.Com<BancaRequerida>($"{campo}.tipoBancaId", new DomainError(
                "FaseCronograma.TipoBancaNaoEncontrado",
                $"Tipo de banca {declarada.TipoBancaId} não encontrado ou não está mais vivo."));
        }

        Result<List<CategoriaJulgada>> atendidos = await CandidatosAtendidosAsync(declarada, campo, cancellationToken).ConfigureAwait(false);
        return atendidos.IsFailure
            ? Result<BancaRequerida>.ValidationFailure(atendidos.Errors)
            : Montada(declarada, tipo, atendidos.Value!, campo);
    }

    private static Result<BancaRequerida> Montada(
        BancaRequeridaInput declarada, TipoBancaView tipo, List<CategoriaJulgada> atendidos, string campo)
    {
        Result<RegraDeDeliberacao?> regra = MontagemDaRegraDeDeliberacao.Montar(declarada.RegraDeDeliberacao, campo);

        return regra.Errors.Count > 0
            ? Result<BancaRequerida>.ValidationFailure(regra.Errors)
            : Result<BancaRequerida>.Success(BancaRequerida.Criar(tipo.Id, tipo.Codigo.Codigo(), tipo.Nome, atendidos, regra.Value, declarada.Id));
    }

    private async Task<Result<List<CategoriaJulgada>>> CandidatosAtendidosAsync(
        BancaRequeridaInput declarada, string campo, CancellationToken cancellationToken)
    {
        List<CategoriaJulgada> atendidos = [];
        foreach ((int posicao, Guid categoriaId) in declarada.CategoriasDocumentoIds.Index())
        {
            CategoriaDocumentoView? categoria = await cadastros.Categorias.ObterAsync(categoriaId, cancellationToken).ConfigureAwait(false);
            if (categoria is null)
            {
                return CategoriaNaoEncontrada($"{campo}.categoriasDocumentoIds[{posicao}]");
            }

            atendidos.Add(CategoriaJulgada.Criar(categoria.Id, categoria.Codigo));
        }

        return Result<List<CategoriaJulgada>>.Success(atendidos);
    }

    private static Result<List<CategoriaJulgada>> CategoriaNaoEncontrada(string campoDaCategoria) =>
        InterrupcaoDaEdicao.Com<List<CategoriaJulgada>>(campoDaCategoria, new DomainError(
            "FaseCronograma.CategoriaDocumentoNaoEncontrada",
            "Uma categoria de documento dos candidatos atendidos pela banca não existe ou está desativada no cadastro."));
}

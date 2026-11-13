namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ProcessosSeletivos.Bancas;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Declara as bancas de uma etapa, copiando por valor o tipo de banca do cadastro e acumulando as
/// violações da regra de deliberação de todas elas.
/// </summary>
internal sealed class DeclaracaoDasBancasDaEtapa(LeituraMemoizada<Guid, TipoBancaView> tiposDeBanca)
{
    public async Task<Result> DeclararAsync(EtapaProcesso etapa, EtapaProcessoInput input, CancellationToken cancellationToken)
    {
        RecusasAcumuladas<BancaDaEtapa> bancas = new();
        for (int posicao = 0; posicao < input.Bancas.Count; posicao++)
        {
            Result<BancaDaEtapa> banca = await BancaAsync(input.Bancas[posicao], posicao, cancellationToken).ConfigureAwait(false);
            if (banca.Errors.Any(static erro => erro.Field is null))
            {
                return Result.Failure(banca.Error!);
            }

            bancas.Juntar(banca);
        }

        Result<List<BancaDaEtapa>> declaradas = bancas.Resultado();
        return declaradas.IsFailure ? Result.ValidationFailure(declaradas.Errors) : AlinhamentoDasBancasDaEtapa.Definir(etapa, declaradas.Value!);
    }

    private async Task<Result<BancaDaEtapa>> BancaAsync(BancaDaEtapaInput declarada, int posicao, CancellationToken cancellationToken)
    {
        TipoBancaView? tipo = await tiposDeBanca.ObterAsync(declarada.TipoBancaId, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return Result<BancaDaEtapa>.Failure(new DomainError(
                "EtapaProcesso.TipoBancaNaoEncontradoOuInativo",
                $"O tipo de banca {declarada.TipoBancaId} não existe ou está inativo no cadastro."));
        }

        Result<RegraDeDeliberacao?> regra = MontagemDaRegraDeDeliberacao.Montar(declarada.RegraDeDeliberacao, $"bancas[{posicao}]");
        return regra.IsFailure
            ? Result<BancaDaEtapa>.ValidationFailure(regra.Errors)
            : Result<BancaDaEtapa>.Success(BancaDaEtapa.Criar(tipo.Id, tipo.Codigo, tipo.Nome, regra.Value));
    }
}

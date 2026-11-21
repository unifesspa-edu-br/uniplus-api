namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ProcessosSeletivos.Bancas;
using Domain.ProcessosSeletivos.Etapas;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Declara as bancas de uma etapa, copiando por valor o tipo de banca do cadastro e acumulando as
/// violações da regra de deliberação de todas elas.
/// </summary>
internal sealed class DeclaracaoDasBancasDaEtapa(LeituraMemoizada<Guid, TipoBancaView> tiposDeBanca)
{
    public async Task<Result> DeclararAsync(EtapaProcesso etapa, EtapaProcessoInput input, CancellationToken cancellationToken)
    {
        Result<List<BancaDaEtapa>> declaradas = await DeclaradasAsync(input, cancellationToken).ConfigureAwait(false);
        if (declaradas.IsFailure)
        {
            return Result.ValidationFailure(declaradas.Errors);
        }

        etapa.DefinirBancas(declaradas.Value!);

        return Result.Success();
    }

    private async Task<Result<List<BancaDaEtapa>>> DeclaradasAsync(EtapaProcessoInput input, CancellationToken cancellationToken)
    {
        RecusasAcumuladas<BancaDaEtapa> bancas = new();
        DomainError? interrupcao = await JuntarAsync(bancas, input, cancellationToken).ConfigureAwait(false);
        if (interrupcao is not null)
        {
            return Result<List<BancaDaEtapa>>.Failure(interrupcao);
        }

        bancas.Acrescentar(IdentificadorDasBancas.Repetidos(input.Bancas.Select(static banca => banca.Id), EtapaDeclarada.ListaDasBancas, IdentificadorDasBancas.DaEtapa));

        return bancas.Resultado();
    }

    // A recusa sem campo é a do catálogo ilegível: interrompe a declaração e devolve só ela.
    private async Task<DomainError?> JuntarAsync(RecusasAcumuladas<BancaDaEtapa> bancas, EtapaProcessoInput input, CancellationToken cancellationToken)
    {
        for (int posicao = 0; posicao < input.Bancas.Count; posicao++)
        {
            Result<BancaDaEtapa> banca = await BancaAsync(input.Bancas[posicao], posicao, cancellationToken).ConfigureAwait(false);
            if (banca.Errors.Any(static erro => erro.Field is null))
            {
                return banca.Error;
            }

            bancas.Juntar(banca);
        }

        return null;
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

        Result<RegraDeDeliberacao?> regra = MontagemDaRegraDeDeliberacao.Montar(declarada.RegraDeDeliberacao, $"{EtapaDeclarada.ListaDasBancas}[{posicao}]");
        return regra.IsFailure
            ? Result<BancaDaEtapa>.ValidationFailure(regra.Errors)
            : Result<BancaDaEtapa>.Success(BancaDaEtapa.Criar(tipo.Id, tipo.Codigo.Codigo(), tipo.Nome, regra.Value, declarada.Id));
    }
}

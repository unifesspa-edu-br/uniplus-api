namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>Declara as bancas de uma etapa, copiando por valor o tipo de banca do cadastro.</summary>
internal sealed class DeclaracaoDasBancasDaEtapa(LeituraMemoizada<Guid, TipoBancaView> tiposDeBanca)
{
    public async Task<Result> DeclararAsync(EtapaProcesso etapa, EtapaProcessoInput input, CancellationToken cancellationToken)
    {
        List<BancaDaEtapa> bancas = [];
        foreach (BancaDaEtapaInput declarada in input.Bancas)
        {
            TipoBancaView? tipo = await tiposDeBanca.ObterAsync(declarada.TipoBancaId, cancellationToken).ConfigureAwait(false);
            if (tipo is null)
            {
                return Result.Failure(new DomainError(
                    "EtapaProcesso.TipoBancaNaoEncontradoOuInativo",
                    $"O tipo de banca {declarada.TipoBancaId} não existe ou está inativo no cadastro."));
            }

            bancas.Add(BancaDaEtapa.Criar(tipo.Id, tipo.Codigo));
        }

        return etapa.DefinirBancas(bancas);
    }
}

namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;

using Kernel.Results;

/// <summary>
/// Define as etapas do processo em duas passadas (ADR-0125). A primeira confere o payload inteiro —
/// forma, caráter, avaliação — sem mutar nada; só se ela vier limpa a segunda reconcilia as etapas, declara
/// os filhos de cada uma e entrega a coleção ao agregado. A segunda passada muta instâncias rastreadas, e
/// por isso a falha dela descarta as alterações: sem o descarte o <c>SaveChanges</c> automático do Wolverine
/// persistiria a mutação parcial mesmo com a coleção recusada.
/// </summary>
internal sealed class DefinicaoDasEtapas(
    DefinirEtapasCommand command,
    ProcessoSeletivo processo,
    CadastrosDasEtapas cadastros,
    AtosConsultadosNaEdicao atos,
    ISelecaoUnitOfWork unitOfWork)
{
    public async Task<Result> DefinirAsync(CancellationToken cancellationToken)
    {
        Result payload = ConferenciaDoPayloadDasEtapas.Conferir(command.Etapas);
        if (payload.IsFailure)
        {
            return payload;
        }

        Dictionary<Guid, EtapaProcesso> existentes = processo.Etapas.ToDictionary(e => e.Id);
        Result<MontagemDasEtapas> montagem = await ConferirContraOsCadastrosAsync(existentes, cancellationToken).ConfigureAwait(false);
        if (montagem.IsFailure)
        {
            return Result.ValidationFailure(montagem.Errors);
        }

        Result aplicada = await AplicarAsync(montagem.Value!, cancellationToken).ConfigureAwait(false);
        if (aplicada.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
        }

        return aplicada;
    }

    private async Task<Result<MontagemDasEtapas>> ConferirContraOsCadastrosAsync(
        IReadOnlyDictionary<Guid, EtapaProcesso> existentes, CancellationToken cancellationToken)
    {
        Result<TiposDasEtapasResolvidos> tipos = await new ConferenciaDoCaraterDasEtapas(cadastros.TiposDeEtapa, existentes)
            .ConferirAsync(command.Etapas, cancellationToken).ConfigureAwait(false);
        if (tipos.IsFailure)
        {
            return Result<MontagemDasEtapas>.ValidationFailure(tipos.Errors);
        }

        Result<AvaliacaoDasEtapasResolvida> avaliacao = await new ConferenciaDaAvaliacaoDasEtapas(existentes, tipos.Value!, cadastros.Regras)
            .ConferirAsync(command.Etapas, cancellationToken).ConfigureAwait(false);
        return avaliacao.IsFailure
            ? Result<MontagemDasEtapas>.ValidationFailure(avaliacao.Errors)
            : Result<MontagemDasEtapas>.Success(new MontagemDasEtapas(existentes, tipos.Value!, avaliacao.Value!));
    }

    private async Task<Result> AplicarAsync(MontagemDasEtapas montagem, CancellationToken cancellationToken)
    {
        Result<List<EtapaProcesso>> etapas = montagem.Montar(command.Etapas);
        if (etapas.IsFailure)
        {
            return Result.ValidationFailure(etapas.Errors);
        }

        Result declaradas = await DeclararFilhosAsync(etapas.Value!, cancellationToken).ConfigureAwait(false);
        if (declaradas.IsFailure)
        {
            return declaradas;
        }

        // O fechamento do ciclo acumula uma violação por matéria aberta, cada uma com o campo que a
        // localiza; propagar o lote preserva esses campos.
        Result definidas = processo.DefinirEtapas(etapas.Value!, command.Precondicao);
        return definidas.IsFailure ? Result.ValidationFailure(definidas.Errors) : definidas;
    }

    private async Task<Result> DeclararFilhosAsync(List<EtapaProcesso> etapas, CancellationToken cancellationToken)
    {
        DeclaracaoDosFilhosDasEtapas declaracao = new(
            new DeclaracaoDosProdutosDaEtapa(atos),
            new DeclaracaoDasBancasDaEtapa(cadastros.TiposDeBanca),
            new DeclaracaoDosRecursosDaEtapa(atos, cadastros.Regras));
        for (int indice = 0; indice < etapas.Count; indice++)
        {
            Result declarada = await declaracao.DeclararAsync(etapas[indice], command.Etapas[indice], cancellationToken).ConfigureAwait(false);
            if (declarada.IsFailure)
            {
                return declarada;
            }
        }

        return Result.Success();
    }
}

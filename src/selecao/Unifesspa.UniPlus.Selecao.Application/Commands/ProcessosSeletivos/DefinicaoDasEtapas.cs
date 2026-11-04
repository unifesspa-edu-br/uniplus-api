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
        Result payload = EscritaDasEtapasDoProcesso.Conferir(command.Etapas);
        if (payload.IsFailure)
        {
            return payload;
        }

        Result<List<EtapaConferida>> conferidas = await ConferirContraOsCadastrosAsync(cancellationToken).ConfigureAwait(false);
        if (conferidas.IsFailure)
        {
            return Result.ValidationFailure(conferidas.Errors);
        }

        Result aplicada = await AplicarAsync(conferidas.Value!, cancellationToken).ConfigureAwait(false);
        if (aplicada.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
        }

        return aplicada;
    }

    private async Task<Result<List<EtapaConferida>>> ConferirContraOsCadastrosAsync(CancellationToken cancellationToken)
    {
        Dictionary<Guid, EtapaProcesso> existentes = processo.Etapas.ToDictionary(e => e.Id);
        List<EtapaConferida> declaradas = [.. command.Etapas.Select(etapa => EtapaConferida.De(etapa, existentes))];
        Result<List<EtapaConferida>> comCarater = await new ConferenciaDoCaraterDasEtapas(cadastros.TiposDeEtapa)
            .ConferirAsync(declaradas, cancellationToken).ConfigureAwait(false);
        return comCarater.IsFailure
            ? comCarater
            : await new ConferenciaDaAvaliacaoDasEtapas(cadastros.Regras)
                .ConferirAsync(comCarater.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> AplicarAsync(IReadOnlyList<EtapaConferida> conferidas, CancellationToken cancellationToken)
    {
        Result<List<EtapaProcesso>> etapas = MontagemDasEtapas.Montar(conferidas);
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

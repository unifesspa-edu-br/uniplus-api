namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// A segunda passada da definição das etapas (ADR-0125): reconcilia as etapas já conferidas, declara os filhos
/// de cada uma e entrega a coleção ao agregado com as etapas que a versão publicada já iniciou, que ele não
/// deixa sair (UNI-REQ-0364). A passada muta instâncias rastreadas, e por isso a falha dela descarta as
/// alterações: sem o descarte o <c>SaveChanges</c> automático do Wolverine persistiria a mutação parcial mesmo
/// com a coleção recusada.
/// </summary>
public sealed class SubstituicaoDasEtapas(
    ITipoAtoPublicadoReader tipoAtoPublicadoReader,
    IProcessoSeletivoRepository processoSeletivoRepository,
    IRegistroCodecsEnvelope registroCodecs,
    LeituraDasFasesIniciadasNaEdicao fasesIniciadas,
    ISelecaoUnitOfWork unitOfWork)
{
    internal async Task<Result> SubstituirAsync(
        ProcessoSeletivo processo,
        DefinirEtapasCommand command,
        IReadOnlyList<EtapaConferida> conferidas,
        CadastrosDasEtapas cadastros,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        Result substituida = await AplicarAsync(processo, command, conferidas, cadastros, agora, cancellationToken).ConfigureAwait(false);
        if (substituida.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
        }

        return substituida;
    }

    private async Task<Result> AplicarAsync(
        ProcessoSeletivo processo,
        DefinirEtapasCommand command,
        IReadOnlyList<EtapaConferida> conferidas,
        CadastrosDasEtapas cadastros,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        Result<List<EtapaProcesso>> etapas = MontagemDasEtapas.Montar(conferidas);
        if (etapas.IsFailure)
        {
            return Result.ValidationFailure(etapas.Errors);
        }

        AtosConsultadosNaEdicao atos = new(tipoAtoPublicadoReader, processoSeletivoRepository, registroCodecs, processo.Id, agora);
        Result declaradas = await DeclararFilhosAsync(etapas.Value!, command.Etapas, cadastros, atos, cancellationToken).ConfigureAwait(false);
        return declaradas.IsFailure
            ? declaradas
            : await EntregarAoAgregadoAsync(processo, command, etapas.Value!, agora, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> EntregarAoAgregadoAsync(
        ProcessoSeletivo processo,
        DefinirEtapasCommand command,
        List<EtapaProcesso> etapas,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        FasesIniciadas iniciadas = await fasesIniciadas.DoProcessoAsync(processo, agora, cancellationToken).ConfigureAwait(false);

        // O fechamento do ciclo acumula uma violação por matéria aberta, cada uma com o campo que a
        // localiza; propagar o lote preserva esses campos.
        Result definidas = processo.DefinirEtapas(etapas, command.Precondicao, iniciadas);
        return definidas.IsFailure ? Result.ValidationFailure(definidas.Errors) : definidas;
    }

    private static async Task<Result> DeclararFilhosAsync(
        List<EtapaProcesso> etapas,
        IReadOnlyList<EtapaProcessoInput> declaradas,
        CadastrosDasEtapas cadastros,
        AtosConsultadosNaEdicao atos,
        CancellationToken cancellationToken)
    {
        DeclaracaoDosFilhosDasEtapas declaracao = new(
            new DeclaracaoDosProdutosDaEtapa(atos),
            new DeclaracaoDasBancasDaEtapa(cadastros.TiposDeBanca),
            new DeclaracaoDosRecursosDaEtapa(atos, cadastros.Regras));
        for (int indice = 0; indice < etapas.Count; indice++)
        {
            Result declarada = await declaracao.DeclararAsync(etapas[indice], declaradas[indice], cancellationToken).ConfigureAwait(false);
            if (declarada.IsFailure)
            {
                return ComPosicaoDaEtapa(declarada, indice);
            }
        }

        return Result.Success();
    }

    // A recusa com campo ganha a posição da etapa no payload; a sem campo segue como veio.
    private static Result ComPosicaoDaEtapa(Result declarada, int indice) =>
        declarada.Errors.Any(static erro => erro.Field is not null)
            ? Result.ValidationFailure(EscritaDasEtapasDoProcesso.ComIndice(declarada.Errors, indice))
            : declarada;
}

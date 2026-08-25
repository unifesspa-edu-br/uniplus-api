namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Handler do <see cref="DefinirDistribuicaoVagasCommand"/> (Story #773):
/// resolve cada oferta e regra de distribuição nos cadastros vivos do módulo
/// Configuração (<see cref="IOfertaCursoReader"/>/<see cref="IModalidadeReader"/>/
/// <see cref="IReferenciaReservaDemograficaReader"/>, ADR-0056) e no catálogo
/// <c>rol_de_regras</c> (<see cref="IRegraCatalogoReader"/>, Story #772),
/// congela cada peça por valor (snapshot-copy, ADR-0061) e monta a
/// distribuição — as invariantes (PR, referência demográfica, modalidades
/// federais, coerência de cada modalidade) são garantidas pelas factories do
/// domínio.
/// </summary>
public static class DefinirDistribuicaoVagasCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirDistribuicaoVagasCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegraCatalogoReader regraCatalogoReader,
        IOfertaCursoReader ofertaCursoReader,
        IModalidadeReader modalidadeReader,
        IReferenciaReservaDemograficaReader referenciaReservaDemograficaReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(ofertaCursoReader);
        ArgumentNullException.ThrowIfNull(modalidadeReader);
        ArgumentNullException.ThrowIfNull(referenciaReservaDemograficaReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // A precondição é conferida AQUI, logo depois do 404 e antes das regras de negócio
        // que este handler avalia (existência de cadastros, coerência de referências): ela
        // as precede na ordem da ADR-0110 D9. Um cliente com If-Match defasado tem de saber
        // disso antes de sair caçando um cadastro que ele não errou.
        //
        // O que ela NÃO precede é a validação de SCHEMA do payload: o FluentValidation roda
        // como middleware do Wolverine, antes deste handler, e um command malformado morre
        // ali com 422 sem que o guard chegue a rodar. É desvio consciente da D9 — corrigi-lo
        // exigiria carregar o agregado no middleware, o que é pior. O custo é uma rodada
        // extra para quem erra as DUAS coisas ao mesmo tempo; nenhum estado é corrompido.
        //
        // O mesmo guard continua dentro do Definir* do domínio: esta antecipação dá a ordem,
        // não a garantia.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        // Acumula (ADR-0125) a forma de VoBase/PR de TODAS as distribuições do payload —
        // as únicas checagens de ConfiguracaoDistribuicaoVagas.Criar que não dependem de
        // nenhum cadastro cross-módulo nem do catálogo de regras. Roda antes de qualquer
        // I/O (validação vence I/O dentro do bucket 422, PR #1211).
        List<FieldError> formaErros = [];
        for (int indice = 0; indice < command.DistribuicaoVagas.Count; indice++)
        {
            ConfiguracaoDistribuicaoVagasInput itemForma = command.DistribuicaoVagas[indice];
            formaErros.AddRange(ConfiguracaoDistribuicaoVagas
                .ValidarFormaBasica(itemForma.VoBase, itemForma.Pr, itemForma.ModalidadeIds.Count)
                .Select(erro => erro.Field is null ? erro : erro with { Field = $"distribuicaoVagas[{indice}].{ConfiguracaoDistribuicaoVagasResolver.TraduzirFieldDoDominio(erro.Field)}" }));
        }

        if (formaErros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(formaErros);
        }

        List<ConfiguracaoDistribuicaoVagas> distribuicoes = [];
        for (int indice = 0; indice < command.DistribuicaoVagas.Count; indice++)
        {
            Result<ConfiguracaoDistribuicaoVagas> resultado = await ConfiguracaoDistribuicaoVagasResolver.ResolverDistribuicaoAsync(
                command.DistribuicaoVagas[indice],
                regraCatalogoReader,
                ofertaCursoReader,
                modalidadeReader,
                referenciaReservaDemograficaReader,
                cancellationToken).ConfigureAwait(false);

            if (resultado.IsFailure)
            {
                IReadOnlyList<FieldError> errosComIndice = [.. resultado.Errors
                    .Select(erro => erro.Field is null ? erro : erro with { Field = $"distribuicaoVagas[{indice}].{ConfiguracaoDistribuicaoVagasResolver.TraduzirFieldDoDominio(erro.Field)}" })];
                return Result<MutacaoAceita>.ValidationFailure(errosComIndice);
            }

            distribuicoes.Add(resultado.Value!);
        }

        Result result = processo.DefinirDistribuicaoVagas(distribuicoes, command.Precondicao);
        if (result.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(result.Error!);
        }

        // Agregado tracked (ObterParaMutacaoAsync): a nova coleção e suas
        // filhas (Guid v7 já preenchido) são persistidas por change detection.
        // NÃO chamar DbSet.Update — marcaria os filhos novos como Modified,
        // emitindo UPDATE de linhas nunca inseridas.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}

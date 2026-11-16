namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Handler do <see cref="DefinirDistribuicaoVagasCommand"/> (Story #773):
/// resolve cada oferta e regra de distribuição nos cadastros vivos do módulo
/// Configuração (<see cref="IOfertaCursoReader"/>/<see cref="IModalidadeReader"/>/
/// <see cref="IReferenciaReservaDemograficaReader"/>, ADR-0056) e no catálogo
/// <c>regras_do_catalogo</c> (<see cref="IRegraCatalogoReader"/>, Story #772),
/// congela cada peça por valor (snapshot-copy, ADR-0061) e monta a
/// distribuição — as invariantes (PR, referência demográfica, modalidades
/// federais, coerência de cada modalidade) são garantidas pelas factories do
/// domínio.
/// </summary>
public static class DefinirDistribuicaoVagasCommandHandler
{
    public static async Task<Result<MutacaoComDistribuicaoVagasDto>> Handle(
        DefinirDistribuicaoVagasCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegraCatalogoReader regraCatalogoReader,
        IOfertaCursoReader ofertaCursoReader,
        IModalidadeReader modalidadeReader,
        IReferenciaReservaDemograficaReader referenciaReservaDemograficaReader,
        IFatoCandidatoReader fatoCandidatoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(ofertaCursoReader);
        ArgumentNullException.ThrowIfNull(modalidadeReader);
        ArgumentNullException.ThrowIfNull(referenciaReservaDemograficaReader);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoComDistribuicaoVagasDto>.Failure(new DomainError(
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
            return Result<MutacaoComDistribuicaoVagasDto>.Failure(bloqueio);
        }

        // A forma de todas as distribuições do payload não depende de nenhum cadastro nem do catálogo de
        // regras, e roda antes de qualquer I/O (validação vence I/O dentro do bucket 422, ADR-0125).
        IReadOnlyList<FieldError> formaErros = ConfiguracaoDistribuicaoVagasResolver.ConferirForma(command.DistribuicaoVagas);
        if (formaErros.Count > 0)
        {
            return Result<MutacaoComDistribuicaoVagasDto>.ValidationFailure(formaErros);
        }

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagas>> resolvidas = await ConfiguracaoDistribuicaoVagasResolver.ResolverTodasAsync(
            command.DistribuicaoVagas,
            regraCatalogoReader,
            ofertaCursoReader,
            modalidadeReader,
            referenciaReservaDemograficaReader,
            cancellationToken).ConfigureAwait(false);
        if (resolvidas.IsFailure)
        {
            return Result<MutacaoComDistribuicaoVagasDto>.ValidationFailure(resolvidas.Errors);
        }

        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicoes = resolvidas.Value!;

        // Uma modalidade retirada da oferta não pode deixar sem valor válido a condição viva sobre
        // qualquer fato cujos valores são modalidades — a fonte vem do catálogo, não do código.
        FatosDeModalidade fatosDeModalidade = VocabularioDeFatos.ComValoresDeModalidade(
            await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false));

        Result result = processo.DefinirDistribuicaoVagas(distribuicoes, fatosDeModalidade, command.Precondicao);
        if (result.IsFailure)
        {
            return Result<MutacaoComDistribuicaoVagasDto>.Failure(result.Error!);
        }

        // Agregado tracked (ObterParaMutacaoAsync): a nova coleção e suas
        // filhas (Guid v7 já preenchido) são persistidas por change detection.
        // NÃO chamar DbSet.Update — marcaria os filhos novos como Modified,
        // emitindo UPDATE de linhas nunca inseridas.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        // Reprojeta do agregado (não da lista local `distribuicoes`): DefinirDistribuicaoVagas
        // substitui a coleção inteira, então processo.DistribuicaoVagas já reflete exatamente
        // o que foi persistido — mesma projeção do GET (ProjecaoDoProcessoSeletivo),
        // para o corpo da resposta nunca divergir do que um GET mostraria em seguida.
        IReadOnlyList<ConfiguracaoDistribuicaoVagasDto> distribuicaoPersistida =
            [.. processo.DistribuicaoVagas.Select(ProjecaoDasVagas.ProjetarDistribuicao)];

        return Result<MutacaoComDistribuicaoVagasDto>.Success(
            new MutacaoComDistribuicaoVagasDto(processo.ETagDaSessaoEditorial, distribuicaoPersistida));
    }
}

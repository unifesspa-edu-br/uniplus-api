namespace Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Wolverine.Attributes;

/// <summary>
/// A listagem de quem opera o processo, projetada pela permissão da classe de cada campo (ADR-0153). O acesso ao
/// processo já foi decidido na borda; aqui a decisão de cada classe de dado é uma só, sobre o processo, e dela saem os
/// registros: um por inscrição exibida, gravados numa só mensagem com a decisão de classe mais alta exercida. A página
/// só sai depois de gravados.
/// </summary>
public static class ListarInscricoesDoProcessoQueryHandler
{
    // Da classe mais alta para a mais baixa: a ordem em que a linha exerce a decisão que fica registrada.
    private static readonly (NaturezaDoItem, ClassificacaoDeProtecao)[] ClassesDaLinha =
    [
        (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal),
        (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Interno),
        (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Publico),
    ];

    /// <summary>
    /// <c>[NonTransactional]</c>: o verificador de acesso lê as concessões do <c>AutorizacaoDbContext</c> e o repositório
    /// lê o <c>SelecaoDbContext</c>; o detector de transação do Wolverine recusa dois contextos. A consulta só lê.
    /// </summary>
    [NonTransactional]
    public static async Task<ListarInscricoesDoProcessoResult> Handle(
        ListarInscricoesDoProcessoQuery query,
        IVerificadorDeAcesso verificador,
        ProjetorDeItens projetor,
        IProcessoSeletivoRepository processoRepository,
        IInscricaoRepository inscricaoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(projetor);
        ArgumentNullException.ThrowIfNull(processoRepository);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        Guid? unidade = await processoRepository
            .ObterUnidadeAdministradoraAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        ResourceContext? resource = AcessoAoProcesso.ResourceDaInscricao(unidade, query.ProcessoSeletivoId, inscricaoId: null);
        if (resource is null)
        {
            return new ListarInscricoesDoProcessoResult([], null, null);
        }

        ProtecaoDoNome nome = await ClassificacaoDoNomeNoProcesso
            .ResolverAsync(query.ProcessoSeletivoId, processoRepository, registroCodecs, cancellationToken).ConfigureAwait(false);
        (NaturezaDoItem, ClassificacaoDeProtecao)[] classes =
        [
            .. (nome.SaiNaListagem ? ClassesDaLinha.Prepend((NaturezaDoItem.Dado, nome.Classificacao)) : ClassesDaLinha)
                .Distinct()
                .OrderByDescending(static par => par.Item2),
        ];

        DecisoesDaProjecao decisoes = await projetor
            .DecidirAsync(PortaDeProjecaoDaSelecao.Instancia, resource, classes, cancellationToken).ConfigureAwait(false);
        ClasseDaLinha permitidas = new(
            Publicos: decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Publico),
            Internos: decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Interno),
            Pessoais: decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal),
            Nome: nome.SaiNaListagem && decisoes.Permite(NaturezaDoItem.Dado, nome.Classificacao));

        (IReadOnlyList<ResumoDaInscricaoDoProcesso> inscricoes, Guid? anteriorAfterId, Guid? proximoAfterId) = await inscricaoRepository
            .ListarEnviadasDoProcessoAsync(
                query.ProcessoSeletivoId, query.AfterId, query.Limit, query.Direction, permitidas.Nome, permitidas.Pessoais, cancellationToken)
            .ConfigureAwait(false);

        await RegistrarUmPorInscricaoAsync(verificador, decisoes, classes, resource, inscricoes, cancellationToken).ConfigureAwait(false);

        InscricaoDoProcessoDto[] items = [.. inscricoes.Select(i => Projetar(i, permitidas))];

        return new ListarInscricoesDoProcessoResult(items, anteriorAfterId, proximoAfterId);
    }

    /// <summary>
    /// Um registro por inscrição exibida, com a decisão de classe mais alta exercida na linha: a primeira permitida entre
    /// as classes de dado, da mais alta para a mais baixa, e só sem nenhuma delas o acesso ao resource.
    /// </summary>
    private static async Task RegistrarUmPorInscricaoAsync(
        IVerificadorDeAcesso verificador,
        DecisoesDaProjecao decisoes,
        IEnumerable<(NaturezaDoItem, ClassificacaoDeProtecao)> classes,
        ResourceContext resource,
        IReadOnlyList<ResumoDaInscricaoDoProcesso> inscricoes,
        CancellationToken cancellationToken)
    {
        DecisaoDeAcesso? exercida = classes
            .Select(par => decisoes.DecisaoDo(par.Item1, par.Item2))
            .FirstOrDefault(static d => d is not null);

        exercida ??= await verificador
            .AvaliarAsync(PermissaoSelecao.InscricoesConsultar.Requisito, resource, cancellationToken)
            .ConfigureAwait(false);

        if (exercida is not DecisaoPermitida permitida)
        {
            return;
        }

        await verificador
            .RegistrarParaCadaAsync(
                permitida, ResourceTiposSelecao.Inscricao, [.. inscricoes.Select(static i => i.Id)],
                resourceEhInscricao: true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>As classes de dado que a pessoa pode ver na linha, decididas uma vez para a página inteira.</summary>
    private sealed record ClasseDaLinha(bool Publicos, bool Internos, bool Pessoais, bool Nome);

    private static InscricaoDoProcessoDto Projetar(ResumoDaInscricaoDoProcesso i, ClasseDaLinha permitidas) =>
        new(
            permitidas.Publicos ? i.Id : null,
            permitidas.Internos ? i.NumeroInscricao : null,
            permitidas.Publicos ? i.Situacao : null,
            permitidas.Publicos ? i.OfertaCursoId : null,
            permitidas.Publicos ? i.OfertaCursoSegundaOpcaoId : null,
            permitidas.Nome ? i.Nome : null,
            permitidas.Pessoais && i.Cpf is { } cpf ? new CpfMascaradoDto(cpf.Mascarado, PodeRevelar: false) : null,
            permitidas.Internos ? i.SubmetidaEm : null);
}

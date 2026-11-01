namespace Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(projetor);
        ArgumentNullException.ThrowIfNull(processoRepository);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);

        (IReadOnlyList<ResumoDaInscricaoDoProcesso> inscricoes, Guid? anteriorAfterId, Guid? proximoAfterId) = await inscricaoRepository
            .ListarEnviadasDoProcessoAsync(query.ProcessoSeletivoId, query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        Guid? unidade = await processoRepository
            .ObterUnidadeAdministradoraAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        ResourceContext? resource = AcessoAoProcesso.ResourceDaInscricao(unidade, query.ProcessoSeletivoId, inscricaoId: null);
        if (resource is null)
        {
            return new ListarInscricoesDoProcessoResult([], null, null);
        }

        DecisoesDaProjecao decisoes = await projetor
            .DecidirAsync(PortaDeProjecaoDaSelecao.Instancia, resource, ClassesDaLinha, cancellationToken).ConfigureAwait(false);

        await RegistrarUmPorInscricaoAsync(verificador, decisoes, resource, inscricoes, cancellationToken).ConfigureAwait(false);

        InscricaoDoProcessoDto[] items = [.. inscricoes.Select(i => Projetar(i, decisoes))];

        return new ListarInscricoesDoProcessoResult(items, anteriorAfterId, proximoAfterId);
    }

    /// <summary>
    /// Um registro por inscrição exibida, com a decisão de classe mais alta exercida na linha que a regra de
    /// sensibilidade manda registrar: dados pessoais, senão o acesso ao resource, que cobre a linha que só exerceu
    /// classes de dado interno ou público.
    /// </summary>
    private static async Task RegistrarUmPorInscricaoAsync(
        IVerificadorDeAcesso verificador,
        DecisoesDaProjecao decisoes,
        ResourceContext resource,
        IReadOnlyList<ResumoDaInscricaoDoProcesso> inscricoes,
        CancellationToken cancellationToken)
    {
        DecisaoDeAcesso? exercida = ClassesDaLinha
            .Select(par => decisoes.DecisaoDo(par.Item1, par.Item2))
            .FirstOrDefault(static d => d is { Registro.Exigido: true });

        exercida ??= await verificador
            .DecidirSemRegistrarAsync(PermissaoSelecao.InscricoesConsultar.Requisito, resource, cancellationToken)
            .ConfigureAwait(false);

        if (exercida is not { Resultado: ResultadoDoAcesso.Permitido, Registro.Exigido: true })
        {
            return;
        }

        await verificador
            .RegistrarParaCadaAsync(
                exercida, ResourceTiposSelecao.Inscricao, [.. inscricoes.Select(static i => i.Id)],
                resourceEhInscricao: true, cancellationToken)
            .ConfigureAwait(false);
    }

    private static InscricaoDoProcessoDto Projetar(ResumoDaInscricaoDoProcesso i, DecisoesDaProjecao decisoes)
    {
        bool publicos = decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Publico);
        bool internos = decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Interno);
        bool pessoais = decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal);

        return new InscricaoDoProcessoDto(
            publicos ? i.Id : null,
            internos ? i.NumeroInscricao : null,
            publicos ? i.Situacao : null,
            publicos ? i.OfertaCursoId : null,
            publicos ? i.OfertaCursoSegundaOpcaoId : null,
            pessoais ? i.Nome : null,
            pessoais ? new CpfMascaradoDto(i.Cpf.Mascarado, PodeRevelar: false) : null,
            internos ? i.SubmetidaEm : null);
    }
}

namespace Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
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
    /// <summary>
    /// <c>[NonTransactional]</c>: a consulta não grava no banco, e o registro do acesso é publicado de forma síncrona no
    /// Kafka. A transação ambiente do Wolverine seguraria a conexão aberta até o broker confirmar.
    /// </summary>
    [NonTransactional]
    public static async Task<ListarInscricoesDoProcessoResult> Handle(
        ListarInscricoesDoProcessoQuery query,
        IVerificadorDeAcesso verificador,
        ProjetorDeItens projetor,
        IPortaDePermissaoPorPar porta,
        IProcessoSeletivoRepository processoRepository,
        IInscricaoRepository inscricaoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(projetor);
        ArgumentNullException.ThrowIfNull(porta);
        ArgumentNullException.ThrowIfNull(processoRepository);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        UnidadeENomeDoProcesso? processo = await processoRepository
            .ObterUnidadeENomeAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (processo is null)
        {
            return new ListarInscricoesDoProcessoResult([], null, null);
        }

        ResourceContext resource = AcessoAoProcesso.ResourceDaInscricao(processo.UnidadeId, query.ProcessoSeletivoId, inscricaoId: null);

        LeituraDasVersoes versoes = await VersoesDoProcessoNaListagem
            .LerAsync(query.ProcessoSeletivoId, processoRepository, registroCodecs, cancellationToken).ConfigureAwait(false);
        (ClassificacaoDeProtecao classeDoNome, string? baseLegalDoNome) = (versoes.ClassificacaoDoNome, versoes.BaseLegalDoNome);
        bool nomeEntra = VersoesDoProcessoNaListagem.EntraNosPares(classeDoNome, baseLegalDoNome);

        // Da classe mais alta para a mais baixa: a ordem em que a linha exerce a decisão que fica registrada. Os pares
        // vêm do que o DTO declara, mais a classe do nome, que é a congelada nas versões do processo.
        (NaturezaDoItem, ClassificacaoDeProtecao)[] classes =
        [
            .. ProtecaoDeclaradaNoDto.Pares<InscricaoDoProcessoDto>()
                .Select(static par => (par.Natureza, par.Classificacao))
                .Concat(nomeEntra ? [(NaturezaDoItem.Dado, classeDoNome)] : [])
                .Distinct()
                .OrderByDescending(static par => par.Item2),
        ];

        DecisoesDaProjecao decisoes = await projetor
            .DecidirAsync(porta, resource, classes, cancellationToken).ConfigureAwait(false);
        NomeClassificadoDto modeloDoNome = new(classeDoNome, baseLegalDoNome) { Valor = string.Empty };
        bool saiONome = nomeEntra && decisoes.ItemSai(modeloDoNome);
        bool saiOCpf = decisoes.PermitePropriedade<InscricaoDoProcessoDto>(nameof(InscricaoDoProcessoDto.Cpf));

        (IReadOnlyList<ResumoDaInscricaoDoProcesso> inscricoes, Guid? anteriorAfterId, Guid? proximoAfterId) = await inscricaoRepository
            .ListarEnviadasDoProcessoAsync(
                query.ProcessoSeletivoId, query.AfterId, query.Limit, query.Direction, saiONome, saiOCpf, cancellationToken)
            .ConfigureAwait(false);

        await RegistrarUmPorInscricaoAsync(verificador, decisoes, classes, resource, inscricoes, cancellationToken).ConfigureAwait(false);

        ProcessoDaInscricaoDto daListagem = new(query.ProcessoSeletivoId, processo.Nome);
        InscricaoDoProcessoDto[] items = [.. inscricoes.Select(i => Projetar(i, daListagem, versoes, decisoes, modeloDoNome, saiONome))];

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

    private static InscricaoDoProcessoDto Projetar(
        ResumoDaInscricaoDoProcesso i,
        ProcessoDaInscricaoDto processo,
        LeituraDasVersoes versoes,
        DecisoesDaProjecao decisoes,
        NomeClassificadoDto modeloDoNome,
        bool saiONome)
    {
        T? Fixo<T>(string campo, T? valor)
            where T : struct => decisoes.PermitePropriedade<InscricaoDoProcessoDto>(campo) ? valor : null;

        return new(
            i.Id,
            processo,
            decisoes.PermitePropriedade<InscricaoDoProcessoDto>(nameof(InscricaoDoProcessoDto.NumeroInscricao)) ? i.NumeroInscricao : null,
            Fixo<SituacaoInscricao>(nameof(InscricaoDoProcessoDto.Situacao), i.Situacao),
            decisoes.PermitePropriedade<InscricaoDoProcessoDto>(nameof(InscricaoDoProcessoDto.Ofertas))
                ? OfertasEscolhidas.Montar(
                    i.OfertaCursoId, i.OfertaCursoSegundaOpcaoId,
                    versoes.NomesDasOfertasPorVersao.GetValueOrDefault(i.VersaoFormularioId) ?? new Dictionary<Guid, string>())
                : null,
            saiONome && i.Nome is { } nome ? modeloDoNome with { Valor = nome } : null,
            decisoes.PermitePropriedade<InscricaoDoProcessoDto>(nameof(InscricaoDoProcessoDto.Cpf)) && i.Cpf is { } cpf
                ? new CpfMascaradoDto(cpf.Mascarado, PodeRevelar: false)
                : null,
            Fixo<DateTimeOffset>(nameof(InscricaoDoProcessoDto.SubmetidaEm), i.SubmetidaEm));
    }
}

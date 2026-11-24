namespace Unifesspa.UniPlus.Selecao.Application.Services.ListagemDeInscricoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Decide e registra a visibilidade das inscrições listadas.</summary>
public sealed class ProjetorDaListagemDeInscricoes(
    IVerificadorDeAcesso verificador, ProjetorDeItens projetor, IPortaDePermissaoPorPar porta,
    IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs)
{
    public async Task<ProjecaoDaListagemDeInscricoes> DecidirAsync(Guid processoId, ResourceContext resource, CancellationToken cancellationToken)
    {
        LeituraDasVersoes versoes = await VersoesDoProcessoNaListagem.LerAsync(processoId, processos, codecs, cancellationToken).ConfigureAwait(false);
        (NaturezaDoItem, ClassificacaoDeProtecao)[] classes = ClassesPedidas(versoes);
        DecisoesDaProjecao decisoes = await projetor.DecidirAsync(porta, resource, classes, cancellationToken).ConfigureAwait(false);
        return new ProjecaoDaListagemDeInscricoes(decisoes, classes, versoes);
    }

    public async Task RegistrarCadaInscricaoAsync(ProjecaoDaListagemDeInscricoes projecao, ResourceContext resource,
        IReadOnlyList<ResumoDaInscricaoDoProcesso> inscricoes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projecao);
        DecisaoDeAcesso exercida = projecao.DecisaoExercida ?? await verificador
            .AvaliarAsync(PermissaoSelecao.InscricoesConsultar.Requisito, resource, cancellationToken).ConfigureAwait(false);
        if (exercida is DecisaoPermitida permitida)
        {
            await verificador.RegistrarParaCadaAsync(permitida, ResourceTiposSelecao.Inscricao,
                [.. inscricoes.Select(static i => i.Id)], resourceEhInscricao: true, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Ordena as classes da mais alta para a mais baixa, para registrar a decisão exercida pela linha.</summary>
    private static (NaturezaDoItem, ClassificacaoDeProtecao)[] ClassesPedidas(LeituraDasVersoes versoes) =>
        [.. ProtecaoDeclaradaNoDto.Pares<InscricaoDoProcessoDto>()
            .Select(static par => (par.Natureza, par.Classificacao))
            .Concat(versoes.NomeEntraNosPares ? [(NaturezaDoItem.Dado, versoes.ClassificacaoDoNome)] : [])
            .Distinct().OrderByDescending(static par => par.Item2)];
}

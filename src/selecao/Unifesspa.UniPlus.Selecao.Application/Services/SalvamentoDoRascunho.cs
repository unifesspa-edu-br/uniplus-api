namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Confere o pedido do candidato contra o formulário e o endereço e, sem recusa, grava o rascunho. A validação é estrutural:
/// só recusa por campo a resposta que viola uma restrição do formulário; nunca por obrigatoriedade, que é exigida somente na
/// submissão.
/// </summary>
public sealed class SalvamentoDoRascunho(ConferenciaDoEnderecoDaInscricao conferenciaDoEndereco, IInscricaoRepository inscricoes, IUnitOfWork unitOfWork)
{
    public async Task<Result> SalvarAsync(
        PedidoDoRascunho pedido, InscricaoRequest request, PrecondicaoIfMatch precondicao, IUserContext userContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(userContext);

        IReadOnlyList<FieldError> recusas = await RecusasAsync(pedido, cancellationToken).ConfigureAwait(false);
        if (recusas.Count > 0)
        {
            return Result.ValidationFailure(recusas);
        }

        Result salvar = pedido.Inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(request.OfertaCursoId, request.OfertaCursoSegundaOpcaoId), pedido.Corpo.Respostas, pedido.Corpo.Grupos, pedido.Corpo.Aceites), pedido.Contexto.Admitidas with { NomeCivilDoLogin = userContext.NomeCompleto, NomeSocialDoLogin = userContext.NomeSocial }, precondicao);
        return salvar.IsFailure ? Result.ValidationFailure(salvar.Errors) : await GravarAsync(pedido.Inscricao, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> GravarAsync(Inscricao inscricao, CancellationToken cancellationToken)
    {
        await inscricoes.AtualizarAsync(inscricao, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>
    /// As recusas por campo do rascunho: primeiro as do formulário avaliado e, só se não houver, as do endereço. O CEP
    /// existir no Geo e ser da cidade declarada vale já no rascunho; o Geo fora do ar adia a conferência para o envio, e o
    /// rascunho continua salvável.
    /// </summary>
    private async Task<IReadOnlyList<FieldError>> RecusasAsync(PedidoDoRascunho pedido, CancellationToken cancellationToken)
    {
        IReadOnlyList<FieldError> doFormulario = RecusasDoFormulario(pedido);
        if (doFormulario.Count > 0)
        {
            return doFormulario;
        }

        ConferenciaDosEnderecos doEndereco = await conferenciaDoEndereco.ConferirAsync(pedido.Corpo.Respostas, pedido.Contexto.Admitidas, aoEnviar: false, cancellationToken)
            .ConfigureAwait(false);
        return doEndereco.Recusas;
    }

    /// <summary>
    /// A mesma avaliação do envio e da consulta (ADR-0143), sem etapa concluída: o rascunho ainda está sendo preenchido, e só
    /// o que o corpo traz é conferido. As restrições vêm do formulário, que é um só para as duas opções; as modalidades por
    /// opção ficam para a avaliação devolvida.
    /// </summary>
    private static IReadOnlyList<FieldError> RecusasDoFormulario(PedidoDoRascunho pedido)
    {
        Result<ResultadoDaAvaliacaoDaInscricao> avaliacao = AvaliacaoDaInscricao.Avaliar(pedido.Contexto.Configuracao, EntradaDaAvaliacaoDaInscricao.Montar(
            DerivadosDoSistemaInscricao.ComOsDerivados(pedido.Contexto.Admitidas.Definicoes), new EntradaDaAvaliacaoDaInscricao(
            pedido.Corpo.Respostas,
            InscricaoRequestMapper.ParaAvaliacao(pedido.Corpo.Grupos),
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            pedido.Envio,
            EntradaDaAvaliacaoDaInscricao.SemApresentacoes,
            NaFaseDaInscricao: true)));
        return avaliacao.IsFailure ? avaliacao.Errors : RecusasEstruturais(AvaliacaoDaInscricao.DaInscricao(avaliacao.Value!.Formulario), pedido.Envio.Dia);
    }

    /// <summary>
    /// Só a resposta que viola uma restrição do formulário é recusada — a ausência de resposta a um item obrigatório não é,
    /// porque o rascunho não exige obrigatórios. Cobre os itens de nível superior e os de cada ocorrência de grupo repetível: o
    /// membro do grupo responde um item como qualquer outro, e uma restrição violada ali é tão inválida quanto no nível superior.
    /// </summary>
    private static IReadOnlyList<FieldError> RecusasEstruturais(AvaliacaoFormulario avaliacao, DateOnly diaDoEnvio) =>
    [
        .. avaliacao.Itens
            .Where(static item => item.RestricoesVioladas.Count > 0)
            .Select(item => RecusaDaResposta.De(item.FatoCodigo, item, diaDoEnvio)),
        .. avaliacao.Grupos
            .SelectMany(grupo => grupo.Ocorrencias.SelectMany(ocorrencia => ocorrencia.Itens
                .Where(static item => item.RestricoesVioladas.Count > 0)
                .Select(item => RecusaDaResposta.De($"{grupo.Codigo}.{ocorrencia.Id}.{item.FatoCodigo}", item, diaDoEnvio)))),
    ];
}

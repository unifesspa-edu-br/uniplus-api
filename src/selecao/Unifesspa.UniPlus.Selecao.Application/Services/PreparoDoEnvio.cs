namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;
using DTOs.Comprovantes;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Prepara o envio: leva o rascunho à versão divulgada, lê o corpo, mescla ao gravado e separa as recusas da forma e do endereço
/// que não dependem do Geo. A identidade da ocorrência de grupo é o que a avaliação exige: o grupo com id repetido ou em branco
/// não se avalia, e a recusa de forma já diz o que corrigir.
/// </summary>
public sealed class PreparoDoEnvio(VersaoDaInscricao versao)
{
    public async Task<Result<PedidoDoEnvio>> PrepararAsync(
        Inscricao inscricao, InscricaoRequest request, IUserContext userContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(userContext);

        Result<ContextoDaVersaoFixada> contexto = await ContextoAsync(inscricao, request, cancellationToken).ConfigureAwait(false);
        Result<CorpoDoEnvio> corpo = contexto.IsFailure ? Result<CorpoDoEnvio>.ValidationFailure(contexto.Errors) : CorpoDoEnvio.De(request);
        return corpo.IsFailure
            ? Result<PedidoDoEnvio>.ValidationFailure(corpo.Errors)
            : PedidoDe(inscricao, request, corpo.Value!, contexto.Value!, userContext);
    }

    private async Task<Result<ContextoDaVersaoFixada>> ContextoAsync(Inscricao inscricao, InscricaoRequest request, CancellationToken cancellationToken)
    {
        Result adotada = await versao.AdotarAsync(inscricao, cancellationToken).ConfigureAwait(false);
        return adotada.IsFailure
            ? Result<ContextoDaVersaoFixada>.ValidationFailure(adotada.Errors)
            : await versao.ContextoAsync(inscricao, request.OfertaCursoId, request.OfertaCursoSegundaOpcaoId, cancellationToken).ConfigureAwait(false);
    }

    private static Result<PedidoDoEnvio> PedidoDe(
        Inscricao inscricao, InscricaoRequest request, CorpoDoEnvio corpo, ContextoDaVersaoFixada contexto, IUserContext userContext)
    {
        // O nome social que não confere com a conta substitui a obrigatoriedade genérica do campo: a recusa que orienta é a que
        // resolve, e a genérica não diz ao candidato onde cadastrar.
        RespostasAdmitidas admitidas = contexto.Admitidas with { NomeCivilDoLogin = userContext.NomeCompleto, NomeSocialDoLogin = userContext.NomeSocial };
        RespostasDoEnvio respostas = RespostasDe(inscricao, corpo, contexto.Admitidas);
        return respostas.TemGrupoComIdRepetido
            ? Result<PedidoDoEnvio>.ValidationFailure(respostas.Recusas)
            : Result<PedidoDoEnvio>.Success(new PedidoDoEnvio(inscricao, request, corpo, contexto, respostas, admitidas));
    }

    private static RespostasDoEnvio RespostasDe(Inscricao inscricao, CorpoDoEnvio corpo, RespostasAdmitidas admitidas)
    {
        // O veredito avalia o estado que SERÁ persistido: um campo respondido numa chamada anterior de Salvar e não reenviado aqui
        // continua preenchido, nunca indeterminado; o mesmo vale para o grupo repetível omitido do corpo.
        IReadOnlyDictionary<string, JsonElement> mescladas = inscricao.RespostasAposMesclar(corpo.Respostas);
        List<FieldError> recusas =
        [
            .. Inscricao.RecusasDeForma(corpo.Respostas, corpo.Grupos, admitidas),
            .. Inscricao.RecusasDoEndereco(mescladas, admitidas),
        ];
        return new RespostasDoEnvio(
            mescladas, inscricao.GruposAposMesclar(corpo.Grupos), recusas, [.. recusas.Select(static r => r.Field).OfType<string>()]);
    }
}

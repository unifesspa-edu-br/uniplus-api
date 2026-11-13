namespace Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A leitura do valor completo de UM campo identificador de uma inscrição, depois de decidido o acesso. O campo é o que a
/// versão fixada pela inscrição congelou como identificador; só então a resposta é procurada e decifrada. O campo que não
/// existe, a resposta ausente e o valor vazio respondem o mesmo erro.
/// </summary>
public sealed class LeituraDoIdentificador
{
    private readonly LeituraDaInscricaoAdministrativa _leitura;
    private readonly IInscricaoRepository _inscricoes;

    public LeituraDoIdentificador(LeituraDaInscricaoAdministrativa leitura, IInscricaoRepository inscricoes)
    {
        ArgumentNullException.ThrowIfNull(leitura);
        ArgumentNullException.ThrowIfNull(inscricoes);

        _leitura = leitura;
        _inscricoes = inscricoes;
    }

    /// <summary>O valor do campo, formatado como CPF quando o fato tem esse formato; falha com o erro do campo não encontrado.</summary>
    public async Task<Result<CampoReveladoDto>> RevelarAsync(
        VinculoDaInscricao vinculo, string fatoCodigo, string? ocorrencia, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vinculo);

        Result<EnvelopeReidratado> envelope = await _leitura.EnvelopeFixadoAsync(vinculo, cancellationToken).ConfigureAwait(false);
        if (envelope.IsFailure)
        {
            return Result<CampoReveladoDto>.Failure(envelope.Error!);
        }

        IdentificadorLocalizado? localizado = await LocalizarAsync(vinculo.Id, envelope.Value!, fatoCodigo, ocorrencia, cancellationToken)
            .ConfigureAwait(false);

        return localizado is null
            ? Result<CampoReveladoDto>.Failure(ErrosDoCampoDaInscricao.NaoEncontrado)
            : await LerAsync(localizado, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IdentificadorLocalizado?> LocalizarAsync(
        Guid inscricaoId, EnvelopeReidratado envelope, string fatoCodigo, string? ocorrencia, CancellationToken cancellationToken)
    {
        foreach (CampoIdentificador candidato in FatosDaVersao.De(envelope).Identificadores(fatoCodigo, emGrupo: ocorrencia is not null))
        {
            RespostaIdentificadora? resposta = await _inscricoes
                .LocalizarIdentificadorAsync(inscricaoId, candidato.FatoCodigo, candidato.GrupoCodigo, ocorrencia, cancellationToken)
                .ConfigureAwait(false);
            if (resposta is not null)
            {
                return new IdentificadorLocalizado(candidato, resposta);
            }
        }

        return null;
    }

    private async Task<Result<CampoReveladoDto>> LerAsync(IdentificadorLocalizado localizado, CancellationToken cancellationToken)
    {
        string? bruto = await _inscricoes.LerIdentificadorAsync(localizado.Resposta.RespostaId, cancellationToken).ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(bruto)
            ? Result<CampoReveladoDto>.Failure(ErrosDoCampoDaInscricao.NaoEncontrado)
            : Result<CampoReveladoDto>.Success(new CampoReveladoDto(localizado.Campo.FatoCodigo, Formatado(localizado.Campo, bruto)));
    }

    private static string Formatado(CampoIdentificador campo, string bruto) =>
        campo.EhCpf && Cpf.Criar(bruto) is { IsSuccess: true } cpf ? cpf.Value!.Formatado : bruto;

    private sealed record IdentificadorLocalizado(CampoIdentificador Campo, RespostaIdentificadora Resposta);
}

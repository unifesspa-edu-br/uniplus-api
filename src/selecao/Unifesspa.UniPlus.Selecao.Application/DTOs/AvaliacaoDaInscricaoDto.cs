namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json;

using Domain.Enums;
using Domain.ValueObjects;

/// <summary>
/// O que o candidato consulta enquanto preenche, sem gravar nada (ADR-0143): as respostas como estão
/// na tela e as ocorrências dos grupos. O campo ou grupo ausente vale o que a inscrição já gravou.
/// </summary>
public sealed record AvaliacaoDaInscricaoRequest(
    IReadOnlyDictionary<string, JsonElement>? Respostas,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>? Grupos);

/// <summary>A avaliação da inscrição diante das respostas do candidato: por ora, as modalidades em que ele concorre.</summary>
public sealed record AvaliacaoDaInscricaoDto(ModalidadesDaInscricaoDto Modalidades)
{
    public static AvaliacaoDaInscricaoDto De(ResultadoDaAvaliacaoDaInscricao resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ModalidadesDeConcorrencia modalidades = resultado.Modalidades;
        return new AvaliacaoDaInscricaoDto(new ModalidadesDaInscricaoDto(
            modalidades.Derivada,
            modalidades.Situacao.ToCodigo(),
            [.. modalidades.Itens.Select(static m => new ModalidadeDaInscricaoDto(m.Codigo, m.Nome, m.Ordem))],
            [.. modalidades.Pendencias.Select(static p => new PendenciaDaAvaliacaoDto(p.FatoCodigo, p.Rotulo, p.SecaoCodigo))]));
    }
}

/// <summary>
/// As modalidades em que o candidato concorre (UNI-REQ-0076). Sem derivação da modalidade na versão,
/// <see cref="Derivada"/> é falso e <see cref="Situacao"/> é nula. A situação é <c>CONCORRE</c>,
/// <c>NAO_CONCORRE</c> ou <c>FALTA_RESPONDER</c>; nesta, <see cref="Pendencias"/> diz o que falta.
/// </summary>
public sealed record ModalidadesDaInscricaoDto(
    bool Derivada,
    string? Situacao,
    IReadOnlyList<ModalidadeDaInscricaoDto> Itens,
    IReadOnlyList<PendenciaDaAvaliacaoDto> Pendencias);

/// <summary>Uma modalidade em que o candidato concorre, com o nome congelado na publicação e a ordem das ofertadas.</summary>
public sealed record ModalidadeDaInscricaoDto(string Codigo, string Nome, int Ordem);

/// <summary>Um campo ou grupo que falta responder, com o rótulo e a seção do formulário em que está.</summary>
public sealed record PendenciaDaAvaliacaoDto(string FatoCodigo, string Rotulo, string? SecaoCodigo);

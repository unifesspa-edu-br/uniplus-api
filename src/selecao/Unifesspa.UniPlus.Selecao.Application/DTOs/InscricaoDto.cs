namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;

/// <summary>
/// Contrato provisório da inscrição do candidato (issue #1858, primeiro commit): DTOs de entrada e
/// saída dos endpoints do candidato, publicados antes dos endpoints existirem (a2 de #1859), para o
/// web desenvolver contra mocks versionados. Depois que #1859 entregar os endpoints reais, este
/// contrato é reconciliado com o OpenAPI gerado — a forma aqui é a melhor previsão de hoje, não um
/// compromisso definitivo.
/// </summary>
/// <remarks>
/// As respostas por fato seguem a mesma forma de <see cref="PreVisualizacaoDoProcessoInput"/>
/// (código do fato → valor bruto) para que o avaliador de formulário leia as duas do mesmo jeito. O
/// CPF nunca aparece em <see cref="Respostas"/>: ele vem do claim do token, nunca do corpo.
/// </remarks>
public sealed record InscricaoRequest(
    Guid? OfertaCursoId,
    IReadOnlyDictionary<string, JsonElement>? Respostas,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>? Grupos,
    IReadOnlyList<AceiteDeTermoInput>? Aceites);

/// <summary>
/// Uma ocorrência de grupo repetível da inscrição, pelo id estável do membro — nunca pela posição,
/// porque reordenar a lista no cliente não pode trocar a identidade de uma ocorrência já gravada.
/// </summary>
public sealed record OcorrenciaDeGrupoInscricaoInput(string Id, IReadOnlyDictionary<string, JsonElement>? Respostas);

/// <summary>
/// O aceite de um termo exigido pelo formulário: a versão e o hash são os que a inscrição carrega da
/// versão do formulário fixada, nunca os do catálogo vivo — um termo ausente desta lista é um termo
/// não aceito.
/// </summary>
public sealed record AceiteDeTermoInput(string Codigo, Guid VersaoId, string HashVersao);

/// <summary>
/// A inscrição do dono do token: respostas, aceites, oferta escolhida e situação, na versão do
/// formulário fixada ao iniciar (UNI-REQ-0084). Os números de documento <c>Identificador</c>
/// declarados (RG, documento do estrangeiro) voltam dentro de <see cref="Respostas"/>, decifrados
/// pelo repositório — só para o próprio dono; o CPF nunca aparece aqui porque já está no token, e o
/// comprovante o devolve mascarado.
/// </summary>
public sealed record InscricaoDto(
    Guid Id,
    Guid ProcessoSeletivoId,
    SituacaoInscricao Situacao,
    string? NumeroInscricao,
    Guid VersaoFormularioId,
    Guid? OfertaCursoId,
    IReadOnlyDictionary<string, JsonElement> Respostas,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> Grupos,
    IReadOnlyList<AceiteDeTermoInput> Aceites,
    string ETag)
{
    /// <summary>Projeta o agregado no contrato — <c>Grupos</c>/<c>Aceites</c> do domínio têm a mesma forma, em tipos próprios do contrato (não acopla um ao outro).</summary>
    public static InscricaoDto De(Inscricao inscricao)
    {
        ArgumentNullException.ThrowIfNull(inscricao);

        return new InscricaoDto(
            inscricao.Id,
            inscricao.ProcessoSeletivoId,
            inscricao.Situacao,
            inscricao.NumeroInscricao,
            inscricao.VersaoFormularioId,
            inscricao.OfertaCursoId,
            inscricao.Respostas,
            inscricao.Grupos.ToDictionary(
                static g => g.Key,
                g => (IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>)
                    [.. g.Value.Select(static o => new OcorrenciaDeGrupoInscricaoInput(o.Id, o.Respostas))],
                StringComparer.Ordinal),
            [.. inscricao.Aceites.Select(static a => new AceiteDeTermoInput(a.Codigo, a.VersaoId, a.HashVersao))],
            inscricao.ETag);
    }
}

/// <summary>
/// O comprovante de uma submissão: cópia do que foi enviado, sem nenhum número de documento (§4.1
/// do plano da #1855), com o hash de integridade (SHA-256 do conteúdo) para provar que não mudou.
/// </summary>
public sealed record ComprovanteInscricaoDto(
    string NumeroInscricao,
    DateTimeOffset Instante,
    string HashIntegridade,
    Guid? OfertaCursoId,
    IReadOnlyDictionary<string, JsonElement> Respostas,
    IReadOnlyList<AceiteDeTermoInput> Aceites);

/// <summary>Um item de "Minhas inscrições": o nome preferido é o social, quando o candidato o declarou.</summary>
public sealed record MinhaInscricaoDto(
    Guid ProcessoSeletivoId,
    string? NumeroInscricao,
    SituacaoInscricao Situacao,
    string NomePreferido);

namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

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
/// comprovante o devolve mascarado. <see cref="Derivados"/> é o que o sistema calculou das
/// respostas na última submissão. <see cref="Avaliacao"/> é a avaliação das respostas gravadas, a
/// mesma que a consulta do candidato devolve — calculada a cada leitura, nunca gravada (ADR-0143).
/// </summary>
public sealed record InscricaoDto(
    Guid Id,
    Guid ProcessoSeletivoId,
    SituacaoInscricao Situacao,
    string? NumeroInscricao,
    Guid VersaoFormularioId,
    Guid? OfertaCursoId,
    IReadOnlyDictionary<string, JsonElement> Respostas,
    IReadOnlyDictionary<string, JsonElement> Derivados,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> Grupos,
    IReadOnlyList<AceiteDeTermoInput> Aceites,
    string ETag,
    AvaliacaoDaInscricaoDto Avaliacao)
{
    /// <summary>Projeta o agregado no contrato — <c>Grupos</c>/<c>Aceites</c> do domínio têm a mesma forma, em tipos próprios do contrato (não acopla um ao outro).</summary>
    public static InscricaoDto De(Inscricao inscricao, AvaliacaoDaInscricaoDto avaliacao)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(avaliacao);

        return new InscricaoDto(
            inscricao.Id,
            inscricao.ProcessoSeletivoId,
            inscricao.Situacao,
            inscricao.NumeroInscricao,
            inscricao.VersaoFormularioId,
            inscricao.OfertaCursoId,
            inscricao.Respostas,
            inscricao.Derivados,
            inscricao.Grupos.ToDictionary(
                static g => g.Key,
                g => (IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>)
                    [.. g.Value.Select(static o => new OcorrenciaDeGrupoInscricaoInput(o.Id, o.Respostas))],
                StringComparer.Ordinal),
            [.. inscricao.Aceites.Select(static a => new AceiteDeTermoInput(a.Codigo, a.VersaoId, a.HashVersao))],
            inscricao.ETag,
            avaliacao);
    }
}

/// <summary>
/// O comprovante de uma submissão: cópia do que foi enviado, sem nenhum número de documento (§4.1
/// do plano da #1855), com o hash de integridade (SHA-256 do conteúdo) para provar que não mudou.
/// <see cref="Respostas"/> é o que o candidato informou; <see cref="Derivados"/>, o que o sistema
/// calculou delas.
/// O CPF do titular vem mascarado (issue #1860) — o comprovante nunca o devolve em claro, mesmo
/// para o próprio dono.
/// <see cref="FusoHorario"/> é a zona em que o <see cref="Instante"/> se lê: o comprovante se
/// apresenta sem depender de o certame estar disponível.
/// <see cref="Modalidades"/> são as modalidades em que o candidato concorre, na ordem das ofertadas;
/// nulas quando o processo não define pela inscrição as modalidades de concorrência.
/// </summary>
public sealed record ComprovanteInscricaoDto(
    string CpfMascarado,
    string NumeroInscricao,
    DateTimeOffset Instante,
    FusoHorarioDto FusoHorario,
    string HashIntegridade,
    Guid? OfertaCursoId,
    IReadOnlyDictionary<string, JsonElement> Respostas,
    IReadOnlyDictionary<string, JsonElement> Derivados,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> Grupos,
    IReadOnlyList<AceiteDeTermoInput> Aceites,
    IReadOnlyList<ModalidadeDaInscricaoDto>? Modalidades)
{
    public static ComprovanteInscricaoDto De(ComprovanteInscricao comprovante, Cpf cpf)
    {
        ArgumentNullException.ThrowIfNull(comprovante);
        ArgumentNullException.ThrowIfNull(cpf);

        return new ComprovanteInscricaoDto(
            cpf.Mascarado,
            comprovante.NumeroInscricao,
            comprovante.Instante,
            FusoHorarioDto.Institucional,
            comprovante.HashIntegridade,
            comprovante.OfertaCursoId,
            comprovante.Respostas.Concat(comprovante.RespostasSensiveis).ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal),
            comprovante.Derivados,
            comprovante.Grupos.ToDictionary(
                static g => g.Key,
                g => (IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>)
                    [.. g.Value.Select(static o => new OcorrenciaDeGrupoInscricaoInput(o.Id, o.Respostas))],
                StringComparer.Ordinal),
            [.. comprovante.Aceites.Select(static a => new AceiteDeTermoInput(a.Codigo, a.VersaoId, a.HashVersao))],
            comprovante.Modalidades?.Select(static m => new ModalidadeDaInscricaoDto(m.Codigo, m.Nome, m.Ordem)).ToList());
    }
}

/// <summary>
/// Um item de "Minhas inscrições" (issue #1860): o nome preferido é o social, quando o candidato
/// o declarou, e o CPF vem mascarado — nenhum outro número de documento aparece na lista.
/// </summary>
/// <param name="NomeDoProcesso">O nome do processo como está divulgado; nulo se ele deixou de ser público.</param>
/// <param name="Comprovantes">Os comprovantes emitidos, em ordem de emissão; vazio em rascunho.</param>
public sealed record MinhaInscricaoDto(
    Guid ProcessoSeletivoId,
    string? NomeDoProcesso,
    string? NumeroInscricao,
    SituacaoInscricao Situacao,
    string NomePreferido,
    string CpfMascarado,
    IReadOnlyList<ComprovanteEmitidoDto> Comprovantes)
{
    /// <summary>
    /// O nome social só prevalece quando o candidato declarou DESEJA_NOME_SOCIAL e preencheu
    /// NOME_SOCIAL (UNI-REQ-0032) — a ausência de qualquer um dos dois cai no nome civil, nunca
    /// em branco.
    /// </summary>
    /// <param name="comprovantes">Os da inscrição, em ordem de emissão.</param>
    public static MinhaInscricaoDto De(Inscricao inscricao, string? nomeDoProcesso, IReadOnlyList<ComprovanteInscricao> comprovantes)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(comprovantes);
        return new MinhaInscricaoDto(
            inscricao.ProcessoSeletivoId, nomeDoProcesso, inscricao.NumeroInscricao, inscricao.Situacao,
            ResolverNomePreferido(inscricao.Respostas), inscricao.Cpf.Mascarado,
            [.. comprovantes.Select(static (c, i) => new ComprovanteEmitidoDto(i + 1, c.Instante))]);
    }

    private static string ResolverNomePreferido(IReadOnlyDictionary<string, JsonElement> respostas)
    {
        bool desejaNomeSocial = respostas.TryGetValue("DESEJA_NOME_SOCIAL", out JsonElement deseja)
            && deseja.ValueKind == JsonValueKind.True;
        if (desejaNomeSocial
            && respostas.TryGetValue("NOME_SOCIAL", out JsonElement nomeSocial)
            && nomeSocial.ValueKind == JsonValueKind.String
            && nomeSocial.GetString() is { Length: > 0 } preferido)
        {
            return preferido;
        }

        return respostas.TryGetValue("NOME", out JsonElement nome) && nome.ValueKind == JsonValueKind.String
            ? nome.GetString() ?? string.Empty
            : string.Empty;
    }
}

/// <summary>Um comprovante emitido, pelo número com que se lê em <c>GET .../comprovantes/{numero}</c>.</summary>
public sealed record ComprovanteEmitidoDto(int Numero, DateTimeOffset Instante);

namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.ComponentModel;
using System.Text.Json;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

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
/// <para>
/// <see cref="OfertaCursoId"/> é a 1ª opção de curso e <see cref="OfertaCursoSegundaOpcaoId"/> a 2ª, que
/// é opcional. Os dois campos substituem o que está gravado: omitidos ou nulos, a opção fica sem oferta,
/// e é assim que o candidato retira a 2ª opção.
/// </para>
/// </remarks>
public sealed record InscricaoRequest(
    Guid? OfertaCursoId,
    IReadOnlyDictionary<string, JsonElement>? Respostas,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>? Grupos,
    IReadOnlyList<AceiteDeTermoInput>? Aceites,
    Guid? OfertaCursoSegundaOpcaoId = null);

/// <summary>
/// Uma ocorrência de grupo repetível da inscrição, pelo id estável do membro — nunca pela posição,
/// porque reordenar a lista no cliente não pode trocar a identidade de uma ocorrência já gravada.
/// </summary>
public sealed record OcorrenciaDeGrupoInscricaoInput(string Id, IReadOnlyDictionary<string, JsonElement>? Respostas);

/// <summary>
/// O aceite de um termo exigido pelo formulário: a versão e o hash são os que a inscrição carrega da
/// versão do formulário da inscrição, nunca os do catálogo vivo — um termo ausente desta lista é um termo
/// não aceito.
/// </summary>
public sealed record AceiteDeTermoInput(string Codigo, Guid VersaoId, string HashVersao);

/// <summary>
/// A inscrição do dono do token: respostas, aceites, oferta escolhida e situação, na versão do
/// formulário em que está a inscrição (UNI-REQ-0084). Os números de documento <c>Identificador</c>
/// declarados (RG, documento do estrangeiro) voltam dentro de <see cref="Respostas"/>, decifrados
/// pelo repositório — só para o próprio dono; o CPF nunca aparece aqui porque já está no token, e o
/// comprovante o devolve mascarado. <see cref="Derivados"/> é o que o sistema calculou das
/// respostas na última submissão. <see cref="Avaliacao"/> é a avaliação das respostas gravadas, a
/// mesma que a consulta do candidato devolve — calculada a cada leitura, nunca gravada (ADR-0143).
/// <see cref="OfertaCursoId"/> e <see cref="OfertaCursoSegundaOpcaoId"/> são a 1ª e a 2ª opção de curso.
/// <see cref="NumeroDeOpcoesDeCurso"/> e <see cref="Ofertas"/> são os da versão em que está a inscrição, e não os do
/// processo vivo: no rascunho, uma retificação divulgada muda as opções; depois do primeiro envio, não muda.
/// </summary>
public sealed record InscricaoDto(
    Guid Id,
    Guid ProcessoSeletivoId,
    SituacaoInscricao Situacao,
    string? NumeroInscricao,
    Guid VersaoFormularioId,
    Guid? OfertaCursoId,
    Guid? OfertaCursoSegundaOpcaoId,
    int NumeroDeOpcoesDeCurso,
    IReadOnlyList<OfertaDeCursoCertameDto> Ofertas,
    [property: Description("As respostas por código do fato. O município vem como texto, o código IBGE, no rascunho, e como objeto { \"codigo\", \"rotulo\" } na inscrição enviada, com o nome que o Geo conferiu no envio.")]
    IReadOnlyDictionary<string, JsonElement> Respostas,
    [property: Description("O que o sistema calculou das respostas. O município de residência vem na mesma forma do município das respostas: { \"codigo\", \"rotulo\" } na inscrição enviada.")]
    IReadOnlyDictionary<string, JsonElement> Derivados,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> Grupos,
    IReadOnlyList<AceiteDeTermoInput> Aceites,
    string ETag,
    AvaliacaoDaInscricaoDto Avaliacao)
{
    /// <summary>Projeta o agregado no contrato — <c>Grupos</c>/<c>Aceites</c> do domínio têm a mesma forma, em tipos próprios do contrato (não acopla um ao outro).</summary>
    /// <param name="numeroDeOpcoesDeCurso">O número de opções da versão em que está a inscrição.</param>
    /// <param name="ofertas">As ofertas da versão em que está a inscrição.</param>
    public static InscricaoDto De(
        Inscricao inscricao, AvaliacaoDaInscricaoDto avaliacao, int numeroDeOpcoesDeCurso, IReadOnlyList<OfertaDeCursoCertameDto> ofertas)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(avaliacao);
        ArgumentNullException.ThrowIfNull(ofertas);

        return new InscricaoDto(
            inscricao.Id,
            inscricao.ProcessoSeletivoId,
            inscricao.Situacao,
            inscricao.NumeroInscricao,
            inscricao.VersaoFormularioId,
            inscricao.OfertaCursoId,
            inscricao.OfertaCursoSegundaOpcaoId,
            numeroDeOpcoesDeCurso,
            ofertas,
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
/// Um item de "Minhas inscrições" (issue #1860). O titular da conta é o único dono das inscrições que
/// lista, então a lista não repete o nome nem o CPF dele: o que distingue uma inscrição da outra é o
/// processo e o curso escolhido.
/// </summary>
/// <param name="InscricaoId">A identidade da inscrição: o processo pode ter mais de uma, uma cancelada e a ativa, e o comprovante se lê por ela.</param>
/// <param name="NomeDoProcesso">O nome do processo como está divulgado; nulo se ele deixou de ser público.</param>
/// <param name="OpcoesDeCurso">Os cursos escolhidos, em ordem, como a versão da inscrição os nomeia; vazio enquanto o rascunho não escolheu nenhum.</param>
/// <param name="CanceladaEm">O instante do cancelamento, quando cancelada.</param>
/// <param name="MotivoDoCancelamento">O motivo do cancelamento, quando cancelada.</param>
/// <param name="Acoes">O que o candidato pode fazer agora com a inscrição, pelo período e pela situação.</param>
/// <param name="Comprovantes">Os comprovantes emitidos, em ordem de emissão; vazio em rascunho e na inscrição cancelada, que não tem comprovante para exibir.</param>
/// <param name="NumeroDoUltimoComprovante">O número do comprovante que vale, o último emitido, para abrir o comprovante da inscrição; nulo sem comprovantes.</param>
public sealed record MinhaInscricaoDto(
    Guid InscricaoId,
    Guid ProcessoSeletivoId,
    string? NomeDoProcesso,
    string? NumeroInscricao,
    SituacaoInscricao Situacao,
    IReadOnlyList<OpcaoDeCursoDaInscricaoDto> OpcoesDeCurso,
    DateTimeOffset? CanceladaEm,
    string? MotivoDoCancelamento,
    AcoesDaInscricaoDto Acoes,
    IReadOnlyList<ComprovanteEmitidoDto> Comprovantes,
    int? NumeroDoUltimoComprovante)
{
    /// <param name="inscricoesAbertas">Se o processo está no período de inscrição neste instante.</param>
    /// <param name="emissoes">Os instantes de emissão dos comprovantes da inscrição, em ordem de emissão.</param>
    /// <param name="ofertasDaVersao">As ofertas de curso da versão em que a inscrição está, com o nome congelado na publicação.</param>
    public static MinhaInscricaoDto De(
        Inscricao inscricao, string? nomeDoProcesso, bool inscricoesAbertas, IReadOnlyList<DateTimeOffset> emissoes, IReadOnlyList<OfertaDeCursoCertameDto> ofertasDaVersao)
    {
        ArgumentNullException.ThrowIfNull(inscricao);

        List<ComprovanteEmitidoDto> comprovantes = ComprovantesParaExibir(inscricao, emissoes);
        return new MinhaInscricaoDto(
            inscricao.Id, inscricao.ProcessoSeletivoId, nomeDoProcesso, inscricao.NumeroInscricao, inscricao.Situacao,
            OpcoesDe(inscricao, ofertasDaVersao), inscricao.CanceladaEm, inscricao.MotivoDoCancelamento,
            AcoesDa(inscricao, inscricoesAbertas), comprovantes, comprovantes.Count > 0 ? comprovantes[^1].Numero : null);
    }

    private static AcoesDaInscricaoDto AcoesDa(Inscricao inscricao, bool inscricoesAbertas)
    {
        AcoesDaInscricao acoes = inscricao.AcoesPermitidas(inscricoesAbertas);

        return new AcoesDaInscricaoDto(acoes.PodeContinuar, acoes.PodeCorrigir, acoes.PodeCancelar, acoes.PodeDescartar);
    }

    private static List<ComprovanteEmitidoDto> ComprovantesParaExibir(Inscricao inscricao, IReadOnlyList<DateTimeOffset> emissoes) =>
        inscricao.ConferirComprovanteEmitivel() is null
            ? [.. emissoes.Select(static (instante, i) => new ComprovanteEmitidoDto(i + 1, instante))]
            : [];

    /// <summary>A 2ª opção só conta com a 1ª escolhida, como no agregado: sem a 1ª nomeada pela versão, não há opções a listar.</summary>
    private static List<OpcaoDeCursoDaInscricaoDto> OpcoesDe(Inscricao inscricao, IReadOnlyList<OfertaDeCursoCertameDto> ofertas)
    {
        List<OpcaoDeCursoDaInscricaoDto> opcoes = [];
        foreach (Guid? escolhida in new[] { inscricao.OfertaCursoId, inscricao.OfertaCursoSegundaOpcaoId })
        {
            OfertaDeCursoCertameDto? oferta = escolhida is { } id
                ? ofertas.FirstOrDefault(o => o.OfertaCursoOrigemId == id)
                : null;
            if (oferta is null)
            {
                break;
            }

            opcoes.Add(new OpcaoDeCursoDaInscricaoDto(opcoes.Count + 1, oferta.CursoNome, oferta.CursoGrau));
        }

        return opcoes;
    }
}

/// <summary>Um curso escolhido na inscrição: a ordem da opção (1ª ou 2ª), o curso e o grau como a versão da inscrição os nomeia.</summary>
public sealed record OpcaoDeCursoDaInscricaoDto(int Ordem, string CursoNome, string CursoGrau);

/// <summary>O que o candidato pode fazer agora com a inscrição (<see cref="AcoesDaInscricao"/>).</summary>
public sealed record AcoesDaInscricaoDto(bool PodeContinuar, bool PodeCorrigir, bool PodeCancelar, bool PodeDescartar);

/// <summary>O corpo do cancelamento da inscrição: o motivo é opcional.</summary>
public sealed record CancelamentoDaInscricaoRequest(string? Motivo);

/// <summary>Um comprovante emitido, pelo número com que se lê em <c>GET /inscricoes/{inscricaoId}/comprovantes/{numero}</c>.</summary>
public sealed record ComprovanteEmitidoDto(int Numero, DateTimeOffset Instante);

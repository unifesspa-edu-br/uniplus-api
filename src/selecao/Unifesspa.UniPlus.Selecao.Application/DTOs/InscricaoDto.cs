namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// Contrato da inscrição do candidato: DTOs de entrada e saída dos endpoints do candidato.
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
/// A inscrição do dono do token, com formulário e escolhas da versão correspondente.
/// A avaliação é calculada a cada leitura e o CPF não é incluído (ADR-0143).
/// </summary>
public sealed record InscricaoDto
{
    public required Guid Id { get; init; }
    public required Guid ProcessoSeletivoId { get; init; }
    public required CodigoComRotulo<SituacaoInscricao> Situacao { get; init; }
    public required string? NumeroInscricao { get; init; }
    public required FormularioDaInscricaoDto Formulario { get; init; }
    public required EscolhasDeCursoDaInscricaoDto EscolhasDeCurso { get; init; }
    public required string ETag { get; init; }
    public required AvaliacaoDaInscricaoDto Avaliacao { get; init; }

    /// <summary>Projeta o agregado e os dados da versão no contrato da inscrição.</summary>
    public static InscricaoDto De(
        Inscricao inscricao, AvaliacaoDaInscricaoDto avaliacao, int numeroDeOpcoesDeCurso, IReadOnlyList<OfertaDeCursoCertameDto> ofertas)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(avaliacao);

        return new InscricaoDto
        {
            Id = inscricao.Id,
            ProcessoSeletivoId = inscricao.ProcessoSeletivoId,
            Situacao = CodigoComRotulo<SituacaoInscricao>.De(inscricao.Estado.Situacao),
            NumeroInscricao = inscricao.Estado.NumeroInscricao,
            Formulario = FormularioDe(inscricao),
            EscolhasDeCurso = EscolhasDe(inscricao, numeroDeOpcoesDeCurso, ofertas),
            ETag = inscricao.ETag,
            Avaliacao = avaliacao,
        };
    }

    private static FormularioDaInscricaoDto FormularioDe(Inscricao inscricao) => new(
        inscricao.Formulario.VersaoFormularioId,
        inscricao.Formulario.Preenchimento.Respostas,
        inscricao.Formulario.Derivados,
        inscricao.Formulario.Preenchimento.Grupos.ToDictionary(
            static g => g.Key,
            g => (IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>)
                [.. g.Value.Select(static o => new OcorrenciaDeGrupoInscricaoInput(o.Id, o.Respostas))],
            StringComparer.Ordinal),
        [.. inscricao.Formulario.Preenchimento.Aceites.Select(static a => new AceiteDeTermoInput(a.Codigo, a.VersaoId, a.HashVersao))]);

    private static EscolhasDeCursoDaInscricaoDto EscolhasDe(
        Inscricao inscricao, int numeroDeOpcoesDeCurso, IReadOnlyList<OfertaDeCursoCertameDto> ofertas)
    {
        ArgumentNullException.ThrowIfNull(ofertas);
        return new EscolhasDeCursoDaInscricaoDto(
            inscricao.Formulario.Preenchimento.Opcoes.Primeira,
            inscricao.Formulario.Preenchimento.Opcoes.Segunda,
            numeroDeOpcoesDeCurso,
            ofertas);
    }
}

/// <summary>
/// Um item de "Minhas inscrições". O titular da conta é o único dono das inscrições que
/// lista, então a lista não repete o nome nem o CPF dele: o que distingue uma inscrição da outra é o
/// processo e o curso escolhido.
/// </summary>
/// <remarks>
/// <para>InscricaoId: A identidade da inscrição: o processo pode ter mais de uma, uma cancelada e a ativa, e o comprovante se lê por ela.</para>
/// <para>NomeDoProcesso: O nome do processo como está divulgado; nulo se ele deixou de ser público.</para>
/// <para>OpcoesDeCurso: Os cursos escolhidos, em ordem, como a versão da inscrição os nomeia; vazio enquanto o rascunho não escolheu nenhum.</para>
/// <para>CanceladaEm: O instante do cancelamento, quando cancelada.</para>
/// <para>MotivoDoCancelamento: O motivo do cancelamento, quando cancelada.</para>
/// <para>Acoes: O que o candidato pode fazer agora com a inscrição, pelo período e pela situação.</para>
/// <para>MotivoDasInscricoesFechadas: Por que as inscrições do processo não estão abertas agora, com o código e o rótulo do motivo; nulo com as inscrições abertas. A tela escreve o texto que explica o motivo ao candidato.</para>
/// <para>NumeroDoUltimoComprovante: O número do comprovante que vale, o último emitido, para abrir o comprovante da inscrição; nulo sem comprovantes e na inscrição cancelada, cujo comprovante a rota recusa.</para>
/// </remarks>
public sealed record MinhaInscricaoDto
{
    public required Guid InscricaoId { get; init; }
    public required Guid ProcessoSeletivoId { get; init; }
    public required string? NomeDoProcesso { get; init; }
    public required string? NumeroInscricao { get; init; }
    public required CodigoComRotulo<SituacaoInscricao> Situacao { get; init; }
    public required IReadOnlyList<OpcaoDeCursoDaInscricaoDto> OpcoesDeCurso { get; init; }
    public required DateTimeOffset? CanceladaEm { get; init; }
    public required string? MotivoDoCancelamento { get; init; }
    public required AcoesDaInscricaoDto Acoes { get; init; }
    public required CodigoComRotulo<MotivoDasInscricoesFechadas>? MotivoDasInscricoesFechadas { get; init; }
    public required int? NumeroDoUltimoComprovante { get; init; }

    /// <param name="motivoDoFechamento">Por que as inscrições do processo estão fechadas neste instante; nulo com as inscrições abertas.</param>
    /// <param name="emissoes">Os instantes de emissão dos comprovantes da inscrição, em ordem de emissão.</param>
    /// <param name="ofertasDaVersao">As ofertas de curso da versão em que a inscrição está, com o nome congelado na publicação.</param>
    public static MinhaInscricaoDto De(
        Inscricao inscricao, string? nomeDoProcesso, MotivoDasInscricoesFechadas? motivoDoFechamento, IReadOnlyList<DateTimeOffset> emissoes, IReadOnlyList<OfertaDeCursoCertameDto> ofertasDaVersao)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(emissoes);

        return MontarItemDaLista(inscricao, nomeDoProcesso, motivoDoFechamento, emissoes, ofertasDaVersao);
    }

    private static MinhaInscricaoDto MontarItemDaLista(
        Inscricao inscricao, string? nomeDoProcesso, MotivoDasInscricoesFechadas? motivoDoFechamento, IReadOnlyList<DateTimeOffset> emissoes, IReadOnlyList<OfertaDeCursoCertameDto> ofertasDaVersao) =>
        new()
        {
            InscricaoId = inscricao.Id,
            ProcessoSeletivoId = inscricao.ProcessoSeletivoId,
            NomeDoProcesso = nomeDoProcesso,
            NumeroInscricao = inscricao.Estado.NumeroInscricao,
            Situacao = CodigoComRotulo<SituacaoInscricao>.De(inscricao.Estado.Situacao),
            OpcoesDeCurso = OpcoesDe(inscricao, ofertasDaVersao),
            CanceladaEm = inscricao.Estado.Cancelamento?.CanceladaEm,
            MotivoDoCancelamento = inscricao.Estado.Cancelamento?.MotivoDoCancelamento,
            Acoes = AcoesDa(inscricao, motivoDoFechamento),
            MotivoDasInscricoesFechadas = motivoDoFechamento is { } motivo ? CodigoComRotulo<MotivoDasInscricoesFechadas>.De(motivo) : null,
            NumeroDoUltimoComprovante = UltimoComprovanteLegivel(inscricao, emissoes),
        };

    private static AcoesDaInscricaoDto AcoesDa(Inscricao inscricao, MotivoDasInscricoesFechadas? motivoDoFechamento)
    {
        AcoesDaInscricao acoes = inscricao.AcoesPermitidas(motivoDoFechamento is null);

        return new AcoesDaInscricaoDto(acoes.PodeContinuar, acoes.PodeCorrigir, acoes.PodeCancelar, acoes.PodeDescartar);
    }

    private static int? UltimoComprovanteLegivel(Inscricao inscricao, IReadOnlyList<DateTimeOffset> emissoes) =>
        inscricao.ConferirComprovanteEmitivel() is null ? ComprovanteNaPosicao.NumeroDoUltimo(emissoes.Count) : null;

    /// <summary>A 2ª opção só conta com a 1ª escolhida, como no agregado: sem a 1ª nomeada pela versão, não há opções a listar.</summary>
    private static List<OpcaoDeCursoDaInscricaoDto> OpcoesDe(Inscricao inscricao, IReadOnlyList<OfertaDeCursoCertameDto> ofertas)
    {
        List<OpcaoDeCursoDaInscricaoDto> opcoes = [];
        foreach (Guid? escolhida in new[] { inscricao.Formulario.Preenchimento.Opcoes.Primeira, inscricao.Formulario.Preenchimento.Opcoes.Segunda })
        {
            OfertaDeCursoCertameDto? oferta = ofertas.FirstOrDefault(o => o.OfertaCursoOrigemId == escolhida);
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

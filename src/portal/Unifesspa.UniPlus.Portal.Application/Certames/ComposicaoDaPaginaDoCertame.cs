namespace Unifesspa.UniPlus.Portal.Application.Certames;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Portal.Application.Certames.Origens;

/// <summary>
/// Compõe a página pública do certame a partir do detalhe publicado em Seleção e da linha do
/// tempo de atos em Publicações (ADR-0131).
/// </summary>
/// <remarks>
/// <para>
/// A indisponibilidade de cada origem tem tratamento próprio. Sem Seleção não há página: a Portal
/// responde indisponibilidade em vez de servir conteúdo vencido. Sem Publicações, ou com uma linha
/// do tempo que não alcança o detalhe servido, a página sai com a linha do tempo degradada e o
/// motivo — o certame já é público, e o que falta é só a lista de atos.
/// </para>
/// <para>
/// Nenhuma regra de visibilidade é reaplicada aqui: o "não encontrado" de Seleção é repassado como
/// tal.
/// </para>
/// </remarks>
public sealed class ComposicaoDaPaginaDoCertame
{
    private readonly IContratoPublicoDoCertame _contrato;
    private readonly IRegistroDeAtosDoCertame _registro;

    public ComposicaoDaPaginaDoCertame(IContratoPublicoDoCertame contrato, IRegistroDeAtosDoCertame registro)
    {
        ArgumentNullException.ThrowIfNull(contrato);
        ArgumentNullException.ThrowIfNull(registro);

        _contrato = contrato;
        _registro = registro;
    }

    /// <summary>Compõe a página do certame identificado pelo identificador legível.</summary>
    /// <remarks>
    /// Um valor fora do formato do identificador legível não identifica certame nenhum: responde
    /// o mesmo "não encontrado" dos demais casos, sem consultar a origem — o endereço é público, e
    /// qualquer texto pode chegar até aqui.
    /// </remarks>
    public async Task<Result<PaginaDoCertame>> ComporPorIdentificadorAsync(
        string identificador,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identificador);

        if (!FormatoKebab.TemComprimentoValido(identificador) || !FormatoKebab.TemFormatoValido(identificador))
        {
            return NaoEncontrado();
        }

        ConsultaDoCertame consulta = await _contrato
            .ObterPorIdentificadorAsync(identificador, cancellationToken)
            .ConfigureAwait(false);

        return await ComporAsync(consulta, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<PaginaDoCertame>> ComporAsync(
        ConsultaDoCertame consulta,
        CancellationToken cancellationToken)
    {
        switch (consulta)
        {
            case ConsultaDoCertame.Encontrado encontrado:
                return Result<PaginaDoCertame>.Success(
                    await ComporEncontradoAsync(encontrado.Certame, cancellationToken).ConfigureAwait(false));

            case ConsultaDoCertame.NaoEncontrado:
                return NaoEncontrado();

            default:
                return Result<PaginaDoCertame>.Failure(new DomainError(
                    PaginaDoCertameErrorCodes.OrigemIndisponivel,
                    "Os dados do certame estão temporariamente indisponíveis."));
        }
    }

    private static Result<PaginaDoCertame> NaoEncontrado() =>
        Result<PaginaDoCertame>.Failure(new DomainError(
            PaginaDoCertameErrorCodes.CertameNaoEncontrado,
            "Certame não encontrado."));

    private async Task<PaginaDoCertame> ComporEncontradoAsync(CertameDaOrigem certame, CancellationToken cancellationToken)
    {
        LeituraDosAtos leitura = await _registro
            .LerAtosAsync(certame.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        return new PaginaDoCertame(
            certame.ProcessoSeletivoId,
            certame.Nome,
            certame.NumeroEdital,
            certame.Situacao,
            certame.DocumentoEditalUrl,
            DataDoAtoCriador(leitura, certame),
            LinhaDoTempoDe(leitura, certame),
            AvaliarDegradacao(certame, leitura));
    }

    private static DateOnly? DataDoAtoCriador(LeituraDosAtos leitura, CertameDaOrigem certame)
    {
        int posicao = PosicaoDoCriador(leitura.Atos, certame.AtoCriadorId);

        return posicao < 0 ? null : leitura.Atos[posicao].DataDoAto;
    }

    /// <summary>
    /// A origem devolve do mais antigo para o mais recente, com o identificador de desempate; a
    /// página mostra o inverso, e inverter preserva o mesmo desempate.
    /// </summary>
    private static EventoDaLinhaDoTempo[] LinhaDoTempoDe(LeituraDosAtos leitura, CertameDaOrigem certame) =>
        [.. leitura.Atos.Reverse().Select(ato => ParaEvento(ato, DocumentosDo(ato, certame)))];

    private static MotivoDaDegradacao? AvaliarDegradacao(CertameDaOrigem certame, LeituraDosAtos leitura) =>
        MotivoDaFalha(leitura) ?? MotivoDaLinhaDoTempo(certame, leitura.Atos);

    private static MotivoDaDegradacao? MotivoDaFalha(LeituraDosAtos leitura) => leitura.Falha switch
    {
        FalhaNaLeituraDosAtos.OrigemIndisponivel => MotivoDaDegradacao.OrigemIndisponivel,
        FalhaNaLeituraDosAtos.FalhaNaPaginacao => MotivoDaDegradacao.FalhaNaPaginacao,
        FalhaNaLeituraDosAtos.LimiteDePaginas => MotivoDaDegradacao.LimiteDePaginas,
        null => null,
        _ => throw new ArgumentOutOfRangeException(nameof(leitura), leitura.Falha, "Falha de leitura desconhecida."),
    };

    /// <summary>
    /// "Mais recente" é a posição na ordem da própria origem, que já desempata atos da mesma data
    /// pelo identificador — comparar datas aqui empataria a retificação publicada no mesmo dia do
    /// ato que ela emenda.
    /// </summary>
    private static MotivoDaDegradacao? MotivoDaLinhaDoTempo(CertameDaOrigem certame, IReadOnlyList<AtoDaOrigem> atos)
    {
        int posicaoDoCriador = PosicaoDoCriador(atos, certame.AtoCriadorId);

        if (posicaoDoCriador < 0)
        {
            return MotivoDaDegradacao.CriadorAusente;
        }

        return atos.Skip(posicaoDoCriador + 1).Any(static ato => ato.CongelaConfiguracao)
            ? MotivoDaDegradacao.DetalheAtrasado
            : null;
    }

    private static int PosicaoDoCriador(IReadOnlyList<AtoDaOrigem> atos, Guid atoCriadorId)
    {
        for (int i = 0; i < atos.Count; i++)
        {
            if (atos[i].Id == atoCriadorId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Os documentos do ato no acervo, o principal e os anexos, tanto do edital de cada versão quanto
    /// do ato que não cria versão. Ato cujos documentos a Seleção ainda não divulgou — a cópia ao
    /// acervo em andamento — sai com lista vazia. O <c>documentoUrl</c> do
    /// evento sai desta mesma lista, para nunca divergir do documento de papel principal.
    /// </summary>
    private static IReadOnlyList<DocumentoDoEvento> DocumentosDo(AtoDaOrigem ato, CertameDaOrigem certame) =>
        certame.DocumentosPorAto.TryGetValue(ato.Id, out IReadOnlyList<DocumentoDoEvento>? documentos)
            ? documentos
            : [];

    private static EventoDaLinhaDoTempo ParaEvento(AtoDaOrigem ato, IReadOnlyList<DocumentoDoEvento> documentos)
    {
        string nomeDoTipo = string.IsNullOrWhiteSpace(ato.TipoNome) ? ato.TipoCodigo : ato.TipoNome;
        string? numero = string.IsNullOrWhiteSpace(ato.Numero) ? null : ato.Numero.Trim();

        return new EventoDaLinhaDoTempo(
            ato.Id,
            ato.DataDoAto,
            Titulo(nomeDoTipo, numero),
            numero,
            ato.Ano,
            new TipoDoEvento(ato.TipoCodigo, nomeDoTipo),
            ato.MotivoRetificacao,
            documentos.FirstOrDefault(static documento => documento.Papel == PapelDoDocumento.Principal)?.Url,
            documentos);
    }

    /// <summary>
    /// Nome do tipo seguido do número do ato, exatamente como declarado.
    /// </summary>
    /// <remarks>
    /// O ano não é acrescentado: número e ano são campos distintos da numeração, e o número é texto
    /// livre que às vezes já traz o ano ("012/2026", "012/2026-R1"). Compor o ano por fora repetiria
    /// o que o órgão declarou; o ano segue no próprio evento, para quem renderiza decidir a forma.
    /// </remarks>
    private static string Titulo(string nomeDoTipo, string? numero) =>
        numero is null ? nomeDoTipo : $"{nomeDoTipo} nº {numero}";
}

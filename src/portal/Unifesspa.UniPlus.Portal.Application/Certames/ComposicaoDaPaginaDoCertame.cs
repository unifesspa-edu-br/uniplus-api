namespace Unifesspa.UniPlus.Portal.Application.Certames;

using System.Globalization;

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

    /// <summary>Compõe a página do certame identificado pelo processo seletivo.</summary>
    public async Task<Result<PaginaDoCertame>> ComporPorIdAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken)
    {
        ConsultaDoCertame consulta = await _contrato
            .ObterPorIdAsync(processoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        return await ComporAsync(consulta, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Compõe a página do certame identificado pelo identificador legível.</summary>
    public async Task<Result<PaginaDoCertame>> ComporPorIdentificadorAsync(
        string identificador,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identificador);

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
                return Result<PaginaDoCertame>.Failure(new DomainError(
                    PaginaDoCertameErrorCodes.CertameNaoEncontrado,
                    "Certame não encontrado."));

            default:
                return Result<PaginaDoCertame>.Failure(new DomainError(
                    PaginaDoCertameErrorCodes.OrigemIndisponivel,
                    "Os dados do certame estão temporariamente indisponíveis."));
        }
    }

    private async Task<PaginaDoCertame> ComporEncontradoAsync(
        CertameDaOrigem certame,
        CancellationToken cancellationToken)
    {
        LeituraDosAtos leitura = await _registro
            .LerAtosAsync(certame.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        // A origem devolve do mais antigo para o mais recente, com o identificador de desempate;
        // a página mostra o inverso, e inverter preserva o mesmo desempate.
        EventoDaLinhaDoTempo[] linhaDoTempo = [.. leitura.Atos.Reverse().Select(ParaEvento)];

        return new PaginaDoCertame(
            certame.ProcessoSeletivoId,
            certame.Nome,
            certame.NumeroEdital,
            linhaDoTempo,
            AvaliarDegradacao(certame, leitura),
            certame.Selo);
    }

    private static MotivoDaDegradacao? AvaliarDegradacao(CertameDaOrigem certame, LeituraDosAtos leitura)
    {
        switch (leitura.Falha)
        {
            case FalhaNaLeituraDosAtos.OrigemIndisponivel:
                return MotivoDaDegradacao.OrigemIndisponivel;
            case FalhaNaLeituraDosAtos.FalhaNaPaginacao:
                return MotivoDaDegradacao.FalhaNaPaginacao;
            case FalhaNaLeituraDosAtos.TetoDePaginas:
                return MotivoDaDegradacao.TetoDePaginas;
            case null:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(leitura), leitura.Falha, "Falha de leitura desconhecida.");
        }

        IReadOnlyList<AtoDaOrigem> atos = leitura.Atos;
        int posicaoDoCriador = -1;
        for (int i = 0; i < atos.Count; i++)
        {
            if (atos[i].Id == certame.AtoCriadorId)
            {
                posicaoDoCriador = i;
                break;
            }
        }

        if (posicaoDoCriador < 0)
        {
            return MotivoDaDegradacao.CriadorAusente;
        }

        // "Mais recente" é a posição na ordem da própria origem, que já desempata atos da mesma
        // data pelo identificador — comparar datas aqui empataria a retificação publicada no mesmo
        // dia do ato que ela emenda.
        for (int i = posicaoDoCriador + 1; i < atos.Count; i++)
        {
            if (atos[i].CongelaConfiguracao)
            {
                return MotivoDaDegradacao.DetalheAtrasado;
            }
        }

        return null;
    }

    private static EventoDaLinhaDoTempo ParaEvento(AtoDaOrigem ato)
    {
        string nomeDoTipo = string.IsNullOrWhiteSpace(ato.TipoNome) ? ato.TipoCodigo : ato.TipoNome;

        return new EventoDaLinhaDoTempo(
            ato.Id,
            ato.DataPublicacao,
            Titulo(nomeDoTipo, ato),
            new TipoDoEvento(ato.TipoCodigo, nomeDoTipo),
            ato.MotivoRetificacao);
    }

    /// <summary>
    /// Nome do tipo seguido do número do ato. O número é texto livre declarado na publicação e às
    /// vezes já traz o ano ("012/2026"); o ano só é acrescentado quando falta, para não sair
    /// repetido.
    /// </summary>
    private static string Titulo(string nomeDoTipo, AtoDaOrigem ato)
    {
        if (string.IsNullOrWhiteSpace(ato.Numero))
        {
            return nomeDoTipo;
        }

        string numero = ato.Numero.Trim();
        string sufixoDoAno = "/" + ato.Ano.ToString(CultureInfo.InvariantCulture);

        return numero.EndsWith(sufixoDoAno, StringComparison.Ordinal)
            ? $"{nomeDoTipo} nº {numero}"
            : $"{nomeDoTipo} nº {numero}{sufixoDoAno}";
    }
}

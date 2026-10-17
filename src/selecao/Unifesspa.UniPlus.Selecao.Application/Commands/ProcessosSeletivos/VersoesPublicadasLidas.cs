namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// As versões publicadas do processo, reidratadas e reduzidas ao que a retificação precisa delas:
/// o que cada formulário coletou e o que cada versão congelou (UNI-REQ-0144). É a única montagem de
/// <see cref="FatosDasVersoesPublicadas"/> a partir das versões: toda versão tem de ser reidratável,
/// no atalho e na abertura da sessão.
/// </summary>
internal sealed record VersoesPublicadasLidas(FatosDasVersoesPublicadas Fatos, IdentificadorLegivel? IdentificadorLegivelDaBase)
{
    /// <summary>
    /// Lê as versões da primeira à base, que é a última. Recusa a versão que o sistema não conhece ou
    /// não sabe reidratar: sem lê-la, não há como saber que fatos cada formulário coletou.
    /// </summary>
    public static Result<VersoesPublicadasLidas> Ler(IRegistroCodecsEnvelope registroCodecs, IReadOnlyList<VersaoConfiguracao> versoes, bool abrindoSessao)
    {
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(versoes);
        // Só se retifica processo publicado: sem versão, quem chama tem defeito, não o pedido.
        ArgumentOutOfRangeException.ThrowIfZero(versoes.Count);

        VersaoConfiguracao versaoAtual = versoes[^1];
        List<GrafoConfiguracao> grafos = new(versoes.Count);
        List<EixosDeVersaoPublicada> congelado = new(versoes.Count);
        foreach (VersaoConfiguracao versao in versoes)
        {
            if (PendenciaDeReidratacao(registroCodecs, versao, ehBase: abrindoSessao && versao.Id == versaoAtual.Id) is { } pendencia)
            {
                return Result<VersoesPublicadasLidas>.Failure(pendencia);
            }

            Result<EnvelopeReidratado> reidratada = registroCodecs.Reidratar(versao);
            if (reidratada.IsFailure)
            {
                return Result<VersoesPublicadasLidas>.Failure(reidratada.Error!);
            }

            grafos.Add(reidratada.Value!.Grafo);
            congelado.Add(ConferenciaDosEixosDosFatosColetados.DaVersao(reidratada.Value!));
        }

        return Result<VersoesPublicadasLidas>.Success(new(FatosDasVersoesPublicadas.De(grafos, congelado), grafos[^1].IdentificadorLegivel));
    }

    /// <summary>
    /// A versão do envelope é conhecida <b>e</b> reidratável? A recusa é <b>nomeada</b>: quem abre
    /// precisa distinguir uma versão que o sistema não conhece de uma que ele conhece e não sabe
    /// reconstruir; e a mensagem diz por que aquela versão precisa ser lida.
    /// </summary>
    private static DomainError? PendenciaDeReidratacao(IRegistroCodecsEnvelope registroCodecs, VersaoConfiguracao versao, bool ehBase)
    {
        string schemaVersion = versao.SchemaVersion;
        CapacidadeCodec? capacidade = registroCodecs.Capacidades
            .FirstOrDefault(c => string.Equals(c.SchemaVersion, schemaVersion, StringComparison.Ordinal));

        // A base da sessão é o que o descarte repõe; fora da sessão, toda versão vale pelo que coletou.
        string consequencia = ehBase
            ? "a retificação não poderia ser descartada"
            : $"sem ler a versão {versao.NumeroVersao}, não há como saber que fatos cada formulário coletou em todas as versões publicadas";

        if (capacidade is null)
        {
            return new DomainError(
                "EnvelopeCodec.VersaoDesconhecida",
                $"A versão {schemaVersion} do envelope congelado não está no registro de codecs — não há como reconstruir esta configuração, e {consequencia}.");
        }

        if (!capacidade.Reidratavel)
        {
            return new DomainError(
                "EnvelopeCodec.VersaoNaoReidratavel",
                $"A versão {schemaVersion} do envelope é conhecida, mas não pode ser reidratada ({capacidade.MotivoDaRecusa}) — {consequencia}.");
        }

        return null;
    }
}

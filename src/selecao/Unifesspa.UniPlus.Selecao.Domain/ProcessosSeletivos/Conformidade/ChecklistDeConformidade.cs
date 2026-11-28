namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class ChecklistDeConformidade
{
    /// <summary>
    /// Os avisos da conformidade: o que o administrador deve confirmar e que nunca impede a
    /// publicação. Lista própria, separada de <see cref="AvaliarConformidade"/>, porque aquela coincide
    /// com a conferência da configuração e um aviso nunca recusa uma publicação.
    /// </summary>
    public static IReadOnlyList<AvisoConformidade> AvaliarAvisos(ConfiguracaoDeclarada configuracao, NomesDosTiposDeAto nomes)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(nomes);

        return [
            .. CicloRecursal.DefinitivosEmMaisDeUmLugar(TiposDeAtoDoCronograma.DeclaracoesDeAtos(configuracao))
                .Select(repetido => new AvisoConformidade(
                    CodigosDeAvisoDeConformidade.CronogramaTipoDefinitivoEmMaisDeUmLugar,
                    DimensaoConformidade.Cronograma,
                    MensagemDoDefinitivoEmMaisDeUmLugar(nomes.NomeDe(repetido.AtoCodigo), repetido.Lugares))),
            .. AvisosDaHomologacao.Da(CronogramaDoProcesso.Do(configuracao.Cronograma.CronogramaFases, configuracao.Origem)),
        ];
    }

    private static string MensagemDoDefinitivoEmMaisDeUmLugar(string? nomeDoTipo, IReadOnlyList<string> lugares)
    {
        string tipo = nomeDoTipo is null ? "Um tipo de ato" : $"O tipo de ato '{nomeDoTipo}'";

        return $"{tipo} está declarado como resultado definitivo em mais de uma fase ou etapa ({JuntarLugares(lugares)}). Confirme que o edital quer fechar mais de um ciclo com o mesmo tipo.";
    }

    internal static string JuntarLugares(IReadOnlyList<string> lugares) =>
        lugares.Count == 1
            ? lugares[0]
            : $"{string.Join(", ", lugares.Take(lugares.Count - 1))} e {lugares[^1]}";

    /// <summary>
    /// Os itens do painel de conformidade coincidem com a conferência da configuração
    /// (<see cref="ConfiguracaoAptaACongelar.Conferir"/>): todos ficam <see langword="true"/> se e somente se a conferência não tem
    /// causa, então não existe estado em que o painel declare tudo <c>Ok</c> e o congelamento recuse por razão estrutural.
    /// </summary>
    /// <remarks>
    /// "Estrutural" não é "publicável": com a conferência sem causas, o congelamento ainda pode recusar por conformidade
    /// legal (<c>GET /conformidade-legal</c>), documento confirmado e outras leituras que só ele faz (ADR-0109).
    /// <para>
    /// Não há segunda lista de predicados: cada item vem do predicado nomeado que a conferência também chama. A conferência
    /// acumula as causas e o painel projeta todos os vereditos, e uma razão nova de um predicado compartilhado aparece nos
    /// dois ao mesmo tempo.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<ItemConformidade> AvaliarConformidade(ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(cadastros);

        return
        [
            .. CompletudeDaIdentificacao.Itens(configuracao),
            .. ItensDaContagemDePrazos.Avaliar(configuracao, cadastros.ContagemDePrazos),
            .. ItensEstruturaisDeConformidade.Avaliar(configuracao),
            .. ItensDoCronograma.Avaliar(configuracao, cadastros),
            .. ItensDaCascata.Avaliar(configuracao),
            .. ItensDasReferenciasDeRegra.Avaliar(configuracao, cadastros),
            .. ItensDasCondicoesParaRegistrarConfiguracao.Avaliar(configuracao, cadastros),
            .. ItensDasEscolhasDoProcesso(configuracao, cadastros),
        ];
    }

    private static IEnumerable<ItemConformidade> ItensDasEscolhasDoProcesso(ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros) =>
    [
        .. ItensDosMotivosDeDecisao.Avaliar(configuracao, cadastros.MotivosEscolhidos),
        ItensDaProvaPresencial.Avaliar(configuracao, cadastros),
        ItensDaProvaPresencial.CidadeDeProva(configuracao),
    ];

    /// <summary>
    /// Uma causa por item estrutural pendente, no campo do bloco do documento da dimensão do item, com a mensagem do item. O
    /// código do item identifica a causa por dentro, para distinguir duas pendências da mesma dimensão.
    /// </summary>
    /// <remarks>
    /// As duas transições que congelam abrem uma <c>VersaoConfiguracao</c> append-only e juridicamente vinculante: uma
    /// versão congelada a partir de configuração incompleta é irreparável, porque o passado não se muta.
    /// </remarks>
    internal static IReadOnlyList<FieldError> CausasDeConformidade(ConfiguracaoDeclarada configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return
        [
            .. ItensEstruturaisDeConformidade.Avaliar(configuracao)
                .Where(static item => !item.Ok)
                .Select(static item => new FieldError(
                    DimensaoConformidade.BlocoDoDocumento(item.Dimensao),
                    new DomainError("ProcessoSeletivo.ConformidadeInsuficiente", $"Item de conformidade pendente: {item.Mensagem}."),
                    item.Codigo)),
        ];
    }
}
